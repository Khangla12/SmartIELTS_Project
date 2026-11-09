using SmartIELTS.Application.DTOs;
using SmartIELTS.Application.Interfaces;
using SmartIELTS.Domain.Common;
using SmartIELTS.Domain.Entities;
using SmartIELTS.Domain.Enums;
using SmartIELTS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace SmartIELTS.Infrastructure.Services;

/// <summary>
/// Cài đặt use case giám thị / giảng viên dùng chung cho Web API và WPF Dashboard.
/// </summary>
public class TeachingDashboardService : ITeachingDashboardService
{
    private readonly SmartIeltsDbContext _context;
    private readonly IProctoringNotificationBus _notificationBus;

    public TeachingDashboardService(SmartIeltsDbContext context, IProctoringNotificationBus notificationBus)
    {
        _context = context;
        _notificationBus = notificationBus;
    }

    public async Task<IReadOnlyList<PendingReviewItemDto>> GetPendingReviewsAsync(CancellationToken ct = default)
    {
        var reports = await _context.EvaluationReports
            .AsNoTracking()
            .Include(r => r.Submission)
            .ThenInclude(s => s!.Session)
            .ThenInclude(sess => sess!.User)
            .Where(r => !r.IsApproved)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(ct);

        return reports.Select(r => new PendingReviewItemDto
        {
            ReportId = r.Id,
            SubmissionId = r.SubmissionId,
            CandidateName = r.Submission?.Session?.User?.FullName ?? "Thí sinh",
            SkillType = r.Submission?.SkillType ?? SkillType.Writing,
            ProposedBand = r.Submission?.BandScore ?? 0,
            Criterion1 = r.CriterionScore1 ?? 0,
            Criterion2 = r.CriterionScore2 ?? 0,
            Criterion3 = r.CriterionScore3 ?? 0,
            Criterion4 = r.CriterionScore4 ?? 0,
            AiFeedback = r.FeedbackComments ?? string.Empty,
            DetailedJson = r.DetailedJsonAnalysis ?? string.Empty,
            EssayContent = r.Submission?.EssayContent ?? string.Empty,
            SubmittedAt = r.Submission?.SubmittedAt ?? r.CreatedAt
        }).ToList();
    }

    public async Task<ScoreApprovalResultDto?> ApproveScoreAsync(TeacherApproveScoreRequestDto request, CancellationToken ct = default)
    {
        var report = await _context.EvaluationReports
            .Include(r => r.Submission)
            .ThenInclude(s => s!.Session)
            .ThenInclude(sess => sess!.Submissions)
            .FirstOrDefaultAsync(r => r.Id == request.ReportId, ct);

        if (report?.Submission?.Session == null) return null;

        // Điểm tiêu chí không được vượt quá 9.0 và phải là bội số 0.5 (chuẩn IELTS).
        double[] criteria =
        {
            ClampBand(request.CriterionScore1),
            ClampBand(request.CriterionScore2),
            ClampBand(request.CriterionScore3),
            ClampBand(request.CriterionScore4)
        };

        report.CriterionScore1 = criteria[0];
        report.CriterionScore2 = criteria[1];
        report.CriterionScore3 = criteria[2];
        report.CriterionScore4 = criteria[3];
        report.FeedbackComments = $"[Giáo viên thẩm định]: {request.TeacherComments}\n[Đánh giá gốc của AI]: {report.FeedbackComments}";
        report.EvaluatedBy = EvaluatorType.Teacher;
        report.IsApproved = true;

        // Điểm chốt tính lại từ 4 tiêu chí giáo viên vừa nhập (không tin số ApprovedBandScore client gửi).
        double officialBand = BandScoreCalculator.RoundToHalfBand(criteria.Average());

        report.Submission.BandScore = officialBand;

        // Trước đây Overall Band không được cập nhật lại sau khi giáo viên duyệt.
        var session = report.Submission.Session;
        double? BandOf(SkillType skill) => session.Submissions
            .FirstOrDefault(s => s.SkillType == skill)?.BandScore;

        var l = BandOf(SkillType.Listening);
        var r = BandOf(SkillType.Reading);
        var w = BandOf(SkillType.Writing);
        var s = BandOf(SkillType.Speaking);

        if (l.HasValue && r.HasValue && w.HasValue && s.HasValue)
        {
            session.OverallBandScore = BandScoreCalculator.CalculateOverallBand(l.Value, r.Value, w.Value, s.Value);
            session.Status = SessionStatus.Evaluated;
        }

        await _context.SaveChangesAsync(ct);

        return new ScoreApprovalResultDto
        {
            ReportId = report.Id,
            OfficialBandScore = officialBand,
            OverallBandScore = session.OverallBandScore
        };
    }

    public async Task<SessionTerminationResultDto?> TerminateSessionAsync(
        Guid sessionId, string reason, string? proctorUsername = null, CancellationToken ct = default)
    {
        var session = await _context.ExamSessions
            .Include(s => s.User)
            .Include(s => s.Submissions)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session == null) return null;

        // Đình chỉ lặp lại không gây lỗi: chỉ ghi nhận thời điểm đình chỉ đầu tiên.
        bool alreadyTerminated = session.Status == SessionStatus.Terminated;
        if (!alreadyTerminated)
        {
            session.Status = SessionStatus.Terminated;
            session.EndTime ??= DateTime.UtcNow;
        }

        string description = alreadyTerminated
            ? $"[Giám thị] Ca thi đã bị đình chỉ trước đó. Ghi nhận thêm: {reason}"
            : $"[Giám thị {proctorUsername ?? "N/A"}] ĐÌNH CHỈ CA THI: {reason}";

        await _context.ProctoringLogs.AddAsync(new ProctoringLog
        {
            SessionId = session.Id,
            Timestamp = DateTime.UtcNow,
            ViolationType = ViolationType.SessionTerminated,
            Description = description
        }, ct);

        await _context.SaveChangesAsync(ct);

        var bands = session.Submissions
            .Where(s => s.BandScore.HasValue)
            .Select(s => s.BandScore!.Value)
            .ToList();

        _notificationBus.Publish(new ProctoringNotification
        {
            SessionId = session.Id,
            CandidateName = session.User?.FullName ?? "Thí sinh",
            ViolationType = ViolationType.SessionTerminated,
            Description = description,
            Timestamp = session.EndTime ?? DateTime.UtcNow
        });

        return new SessionTerminationResultDto
        {
            SessionId = session.Id,
            CandidateName = session.User?.FullName ?? "Thí sinh",
            TerminatedAt = session.EndTime ?? DateTime.UtcNow,
            Reason = reason,
            // Điểm tạm tính: trung bình các kỹ năng đã nộp, KHÔNG dùng làm điểm chính thức.
            PartialOverallBand = bands.Count > 0 ? Math.Round(bands.Average(), 2) : null,
            SubmittedSkillCount = session.Submissions.Count
        };
    }

    private static double ClampBand(double score)
    {
        double clamped = Math.Clamp(score, 1.0, 9.0);
        return Math.Clamp(BandScoreCalculator.RoundToHalfBand(clamped), 1.0, 9.0);
    }

    public async Task<IReadOnlyList<ExamSessionItemDto>> GetActiveSessionsAsync(int take = 50, CancellationToken ct = default)
    {
        var sessions = await _context.ExamSessions
            .AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.Exam)
            .Include(s => s.ProctoringLogs)
            .OrderByDescending(s => s.StartTime)
            .Take(take)
            .ToListAsync(ct);

        return sessions.Select(s => new ExamSessionItemDto
        {
            SessionId = s.Id,
            CandidateName = s.User?.FullName ?? "Thí sinh",
            Username = s.User?.Username ?? "N/A",
            ExamTitle = s.Exam?.Title ?? "Kỳ thi IELTS",
            StartTime = s.StartTime,
            Status = s.Status.ToString(),
            OverallBand = s.OverallBandScore,
            ViolationCount = s.ProctoringLogs.Count
        }).ToList();
    }

    public async Task<IReadOnlyList<ProctoringLogItemDto>> GetProctoringLogsAsync(int take = 100, CancellationToken ct = default)
    {
        var logs = await _context.ProctoringLogs
            .AsNoTracking()
            .Include(l => l.Session)
            .ThenInclude(s => s!.User)
            .OrderByDescending(l => l.Timestamp)
            .Take(take)
            .ToListAsync(ct);

        return logs.Select(l => new ProctoringLogItemDto
        {
            Id = l.Id,
            SessionId = l.SessionId,
            CandidateName = l.Session?.User?.FullName ?? "Thí sinh",
            Username = l.Session?.User?.Username ?? "N/A",
            Timestamp = l.Timestamp,
            ViolationType = l.ViolationType.ToString(),
            Description = l.Description
        }).ToList();
    }

    public async Task<bool> LogViolationAsync(ProctoringViolationDto request, CancellationToken ct = default)
    {
        var session = await _context.ExamSessions
            .AsNoTracking()
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, ct);

        if (session == null) return false;

        var log = new ProctoringLog
        {
            SessionId = request.SessionId,
            Timestamp = DateTime.UtcNow,
            ViolationType = request.ViolationType,
            Description = request.Description,
            EvidenceSnapshotUrl = request.EvidenceSnapshotUrl
        };

        await _context.ProctoringLogs.AddAsync(log, ct);
        await _context.SaveChangesAsync(ct);

        // Phát cho mọi bảng giám sát: dashboard cùng tiến trình (WPF) và SignalR (Web API).
        _notificationBus.Publish(new ProctoringNotification
        {
            SessionId = log.SessionId,
            CandidateName = session.User?.FullName ?? "Thí sinh",
            ViolationType = log.ViolationType,
            Description = log.Description,
            Timestamp = log.Timestamp
        });

        return true;
    }
}
