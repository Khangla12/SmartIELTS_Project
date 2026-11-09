using System.Text.Json;
using SmartIELTS.Application.DTOs;
using SmartIELTS.Application.GradingStrategies;
using SmartIELTS.Application.Interfaces;
using SmartIELTS.Domain.Common;
using SmartIELTS.Domain.Entities;
using SmartIELTS.Domain.Enums;
using SmartIELTS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace SmartIELTS.Infrastructure.Services;

/// <summary>
/// Cài đặt luồng thi dùng chung cho Web API và WPF Kiosk.
/// Mọi thao tác ghi đều add thẳng vào DbSet (không add qua navigation của entity đã tracked)
/// để EF đánh dấu Added thay vì Modified - nguyên nhân gây DbUpdateConcurrencyException trước đây.
/// </summary>
public class ExamWorkflowService : IExamWorkflowService
{
    private readonly SmartIeltsDbContext _context;
    private readonly IGradingStrategyFactory _gradingFactory;

    public ExamWorkflowService(SmartIeltsDbContext context, IGradingStrategyFactory gradingFactory)
    {
        _context = context;
        _gradingFactory = gradingFactory;
    }

    public async Task<StartSessionResultDto?> StartSessionAsync(Guid userId, Guid examId, CancellationToken ct = default)
    {
        bool userExists = await _context.Users.AnyAsync(u => u.Id == userId, ct);
        if (!userExists) return null;

        var exam = await _context.Exams
            .AsNoTracking()
            .Include(e => e.Sections)
            .ThenInclude(s => s.Questions)
            .FirstOrDefaultAsync(e => e.Id == examId, ct);

        if (exam == null) return null;

        var session = new ExamSession
        {
            UserId = userId,
            ExamId = examId,
            StartTime = DateTime.UtcNow,
            Status = SessionStatus.InProgress
        };

        await _context.ExamSessions.AddAsync(session, ct);
        await _context.SaveChangesAsync(ct);

        return new StartSessionResultDto
        {
            SessionId = session.Id,
            StartTime = session.StartTime,
            Exam = DtoMapper.ToExamDto(exam)
        };
    }

    public async Task<SubmitSkillResultDto> SubmitSkillAsync(SubmitSkillRequestDto request, CancellationToken ct = default)
    {
        var session = await _context.ExamSessions
            .Include(s => s.Exam)
            .ThenInclude(e => e!.Sections)
            .ThenInclude(sec => sec.Questions)
            .Include(s => s.Submissions)
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, ct)
            ?? throw new InvalidOperationException("Phiên thi không tồn tại!");

        var section = session.Exam?.Sections.FirstOrDefault(s => s.SkillType == request.SkillType)
            ?? throw new InvalidOperationException("Phần thi không tồn tại trong đề thi!");

        // Ca thi đã bị đình chỉ thì không nhận thêm bài nộp.
        if (session.Status == SessionStatus.Terminated)
            throw new InvalidOperationException("Ca thi đã bị giám thị đình chỉ, không thể nộp bài.");

        string answersJson = request.Answers != null
            ? JsonSerializer.Serialize(request.Answers.ToDictionary(k => k.Key.ToString(), v => v.Value))
            : string.Empty;

        var submission = new CandidateSubmission
        {
            SessionId = session.Id,
            SkillType = request.SkillType,
            SubmittedAt = DateTime.UtcNow,
            EssayContent = request.EssayContent,
            AudioRecordUrl = request.AudioRecordUrl,
            AnswersJson = answersJson
        };

        var grader = _gradingFactory.GetGrader(request.SkillType);
        var gradeResult = await grader.GradeAsync(submission, section);

        submission.RawScore = gradeResult.RawScore;
        submission.BandScore = gradeResult.BandScore;

        var report = new EvaluationReport
        {
            SubmissionId = submission.Id,
            EvaluatedBy = EvaluatorType.AiEngine,
            CriterionScore1 = gradeResult.CriterionScore1,
            CriterionScore2 = gradeResult.CriterionScore2,
            CriterionScore3 = gradeResult.CriterionScore3,
            CriterionScore4 = gradeResult.CriterionScore4,
            FeedbackComments = gradeResult.FeedbackComments,
            DetailedJsonAnalysis = gradeResult.DetailedJsonAnalysis,
            // Listening / Reading là bài khách quan -> AI chấm là chốt.
            IsApproved = request.SkillType is SkillType.Listening or SkillType.Reading
        };

        // Add thẳng vào DbSet: EF sẽ đánh dấu Added cho cả graph submission + report.
        await _context.CandidateSubmissions.AddAsync(submission, ct);
        await _context.EvaluationReports.AddAsync(report, ct);

        // Nộp lại cùng một kỹ năng sẽ thay thế bài cũ (tránh nhiều submission trùng lặp).
        var previous = session.Submissions
            .Where(s => s.SkillType == request.SkillType && s.Id != submission.Id)
            .ToList();
        if (previous.Count > 0)
        {
            _context.CandidateSubmissions.RemoveRange(previous);
            foreach (var old in previous) session.Submissions.Remove(old);
        }

        session.Submissions.Add(submission);
        RecalculateSessionScore(session, request.SkillType);

        await _context.SaveChangesAsync(ct);

        return new SubmitSkillResultDto
        {
            SkillType = request.SkillType,
            RawScore = submission.RawScore ?? 0,
            TotalQuestions = gradeResult.TotalQuestions,
            BandScore = submission.BandScore ?? 0,
            ReportId = report.Id,
            OverallBandScore = session.OverallBandScore,
            CriterionScore1 = report.CriterionScore1,
            CriterionScore2 = report.CriterionScore2,
            CriterionScore3 = report.CriterionScore3,
            CriterionScore4 = report.CriterionScore4,
            FeedbackComments = report.FeedbackComments,
            DetailedJsonAnalysis = report.DetailedJsonAnalysis
        };
    }

    public async Task<SessionResultDto?> GetSessionResultAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await _context.ExamSessions
            .AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.Exam)
            .Include(s => s.Submissions)
            .ThenInclude(sub => sub.EvaluationReports)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

        if (session == null) return null;

        return DtoMapper.ToSessionResultDto(session);
    }

    public async Task<bool> SaveDraftAsync(AutosaveDraftRequestDto request, CancellationToken ct = default)
    {
        bool sessionExists = await _context.ExamSessions.AnyAsync(s => s.Id == request.SessionId, ct);
        if (!sessionExists) return false;

        var draft = await _context.ExamDrafts
            .FirstOrDefaultAsync(d => d.SessionId == request.SessionId && d.SkillType == request.SkillType, ct);

        string? answersJson = request.DraftAnswers != null
            ? JsonSerializer.Serialize(request.DraftAnswers.ToDictionary(k => k.Key.ToString(), v => v.Value))
            : null;

        if (draft == null)
        {
            draft = new ExamDraft
            {
                SessionId = request.SessionId,
                SkillType = request.SkillType
            };
            await _context.ExamDrafts.AddAsync(draft, ct);
        }

        draft.DraftContent = request.DraftContent;
        draft.DraftAnswersJson = answersJson;
        draft.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<AutosaveDraftRequestDto?> GetDraftAsync(Guid sessionId, SkillType skillType, CancellationToken ct = default)
    {
        var draft = await _context.ExamDrafts
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.SessionId == sessionId && d.SkillType == skillType, ct);

        if (draft == null) return null;

        Dictionary<Guid, string>? answers = null;
        if (!string.IsNullOrWhiteSpace(draft.DraftAnswersJson))
        {
            try
            {
                var raw = JsonSerializer.Deserialize<Dictionary<string, string>>(draft.DraftAnswersJson);
                if (raw != null)
                {
                    answers = new Dictionary<Guid, string>();
                    foreach (var pair in raw)
                    {
                        if (Guid.TryParse(pair.Key, out var id)) answers[id] = pair.Value;
                    }
                }
            }
            catch (JsonException)
            {
                answers = null;
            }
        }

        return new AutosaveDraftRequestDto
        {
            SessionId = draft.SessionId,
            SkillType = draft.SkillType,
            DraftContent = draft.DraftContent,
            DraftAnswers = answers
        };
    }

    /// <summary>
    /// Cập nhật Overall Band sau mỗi lần nộp. Chỉ chốt ca thi khi đã đủ 4 kỹ năng.
    /// Ca thi đã bị đình chỉ giữ nguyên trạng thái Terminated.
    /// </summary>
    private static void RecalculateSessionScore(ExamSession session, SkillType justSubmitted)
    {
        double? BandOf(SkillType skill) => session.Submissions
            .FirstOrDefault(s => s.SkillType == skill)?.BandScore;

        var l = BandOf(SkillType.Listening);
        var r = BandOf(SkillType.Reading);
        var w = BandOf(SkillType.Writing);
        var s = BandOf(SkillType.Speaking);

        if (l.HasValue && r.HasValue && w.HasValue && s.HasValue)
        {
            session.OverallBandScore = BandScoreCalculator.CalculateOverallBand(l.Value, r.Value, w.Value, s.Value);
            // Không "hồi sinh" ca thi đã bị đình chỉ.
            if (session.Status != SessionStatus.Terminated) session.Status = SessionStatus.Evaluated;
            session.EndTime ??= DateTime.UtcNow;
        }
        else if (session.Status != SessionStatus.Terminated)
        {
            session.Status = SessionStatus.InProgress;
        }
    }
}
