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

    public async Task<IReadOnlyList<PendingReviewItemDto>> GetPendingReviewsAsync(
        string? teacherUsername = null, CancellationToken ct = default)
    {
        var query = _context.EvaluationReports
            .AsNoTracking()
            .Include(r => r.Submission)
            .ThenInclude(s => s!.Session)
            .ThenInclude(sess => sess!.User)
            .Include(r => r.Submission)
            .ThenInclude(s => s!.Session)
            .ThenInclude(sess => sess!.Exam)
            .Where(r => !r.IsApproved);

        // Lọc theo phân công: giảng viên chỉ thấy bài của đề mình phụ trách.
        // Admin (không truyền username hoặc role Admin) thấy tất cả.
        if (!string.IsNullOrWhiteSpace(teacherUsername))
        {
            var assignment = await GetTeacherAssignmentFilterAsync(teacherUsername, ct);

            // null = Admin hoặc không tìm thấy user -> xem tất cả.
            if (assignment != null)
            {
                var (examIds, allowedSkills) = assignment.Value;

                // Nếu giảng viên chưa được phân công đề nào -> không thấy bài nào.
                query = query.Where(r => examIds.Contains(r.Submission!.Session!.ExamId));

                if (allowedSkills.Count > 0)
                {
                    query = query.Where(r => allowedSkills.Contains(r.Submission!.SkillType));
                }
            }
        }

        var reports = await query.OrderByDescending(r => r.CreatedAt).ToListAsync(ct);

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

    /// <summary>
    /// Trả về (danh sách ExamId được phân công, danh sách kỹ năng được chấm).
    /// null = không giới hạn (Admin, hoặc user không phải Teacher).
    /// <para>
    /// CHẾ ĐỘ MỞ: nếu toàn hệ thống CHƯA có phân công nào → không lọc.
    /// Lý do: tránh trường hợp giảng viên không thấy bài nào khi Admin chưa kịp gán
    /// (đặc biệt trong demo). Khi Admin đã gán ít nhất 1 phân công thì lọc chặt.
    /// </para>
    /// </summary>
    private async Task<(List<Guid> ExamIds, List<SkillType> AllowedSkills)?> GetTeacherAssignmentFilterAsync(
        string username, CancellationToken ct)
    {
        var teacher = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == username, ct);

        if (teacher == null) return null;

        // Admin xem tất cả.
        if (teacher.Role == UserRole.Admin) return null;

        // Chế độ mở: chưa ai được phân công -> không lọc.
        bool anyAssignmentInSystem = await _context.ExamAssignments
            .AsNoTracking()
            .AnyAsync(a => a.IsActive, ct);

        if (!anyAssignmentInSystem)
        {
            Console.WriteLine("[SmartIELTS] Chưa có phân công nào trong hệ thống -> giảng viên xem tất cả (chế độ mở).");
            return null;
        }

        var assignments = await _context.ExamAssignments
            .AsNoTracking()
            .Where(a => a.TeacherId == teacher.Id && a.IsActive)
            .ToListAsync(ct);

        var examIds = assignments.Select(a => a.ExamId).Distinct().ToList();

        // Kỹ năng: hợp của các giá trị AllowedSkills; rỗng = tất cả.
        var skills = new List<SkillType>();
        foreach (var a in assignments)
        {
            if (string.IsNullOrWhiteSpace(a.AllowedSkills)) { skills.Clear(); break; }

            foreach (var part in a.AllowedSkills.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                if (Enum.TryParse<SkillType>(part.Trim(), true, out var skill) && !skills.Contains(skill))
                    skills.Add(skill);
            }
        }

        return (examIds, skills);
    }

    public async Task<ScoreApprovalResultDto?> ApproveScoreAsync(
        TeacherApproveScoreRequestDto request, string? proctorUsername = null, CancellationToken ct = default)
    {
        var report = await _context.EvaluationReports
            .Include(r => r.Submission)
            .ThenInclude(s => s!.Session)
            .ThenInclude(sess => sess!.Submissions)
            .FirstOrDefaultAsync(r => r.Id == request.ReportId, ct);

        if (report?.Submission?.Session == null) return null;

        var session = report.Submission.Session;

        // ---------------------------------------------------------------------
        // Kiểm tra phân công: giảng viên chỉ được duyệt đề mình phụ trách.
        // ---------------------------------------------------------------------
        if (!string.IsNullOrWhiteSpace(proctorUsername))
        {
            var filter = await GetTeacherAssignmentFilterAsync(proctorUsername, ct);

            if (filter != null)
            {
                var (examIds, allowedSkills) = filter.Value;

                if (!examIds.Contains(session.ExamId))
                    throw new InvalidOperationException(
                        "Bạn không được phân công chấm đề thi này. Liên hệ Admin để được gán.");

                if (allowedSkills.Count > 0 && !allowedSkills.Contains(report.Submission.SkillType))
                    throw new InvalidOperationException(
                        $"Bạn không được phân công chấm kỹ năng {report.Submission.SkillType} của đề này.");
            }
        }

        // ---------------------------------------------------------------------
        // Ràng buộc nghiệp vụ trước khi ghi điểm
        // ---------------------------------------------------------------------
        // Cho phép SỬA điểm đã duyệt (giảng viên đổi ý / phát hiện sai sót).
        // Điểm cũ được giữ trong AuditNote để còn dấu vết.
        bool isRegrade = report.IsApproved;
        string? previousNote = isRegrade
            ? $"[Điểm cũ: {report.CriterionScore1}/{report.CriterionScore2}/{report.CriterionScore3}/{report.CriterionScore4} " +
              $"bởi {report.ApprovedBy ?? "?"} lúc {report.ApprovedAt:dd/MM/yyyy HH:mm}] "
            : null;

        if (session.Status == SessionStatus.Terminated)
            throw new InvalidOperationException("Ca thi đã bị giám thị đình chỉ. Không thể duyệt và công bố điểm.");

        // Listening/Reading là bài khách quan, AI chấm là chốt — giáo viên không sửa.
        if (report.Submission.SkillType is SkillType.Listening or SkillType.Reading)
            throw new InvalidOperationException(
                $"Phần {report.Submission.SkillType} là bài khách quan, được chấm tự động. Không cần duyệt lại.");

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

        // Nhận xét của AI giữ nguyên trong FeedbackComments (chỉ giảng viên xem).
        // Nhận xét chính thức của giảng viên lưu riêng — đây là phần thí sinh được xem.
        report.TeacherFeedback = string.IsNullOrWhiteSpace(request.TeacherComments)
            ? "Giảng viên đã chấm và xác nhận điểm."
            : request.TeacherComments.Trim();

        // Nếu sửa điểm: ghi lại điểm cũ vào nhận xét để còn audit trail.
        if (isRegrade && previousNote != null)
        {
            report.TeacherFeedback = previousNote + report.TeacherFeedback;
        }

        report.EvaluatedBy = EvaluatorType.Teacher;
        report.IsApproved = true;
        report.ApprovedAt = DateTime.UtcNow;
        report.ApprovedBy = proctorUsername;

        // Điểm chốt tính lại từ 4 tiêu chí giáo viên vừa nhập (không tin số ApprovedBandScore client gửi).
        double officialBand = BandScoreCalculator.RoundToHalfBand(criteria.Average());

        report.Submission.BandScore = officialBand;

        // Chỉ công bố Overall Band khi CẢ 4 KỸ NĂNG đã được duyệt (Writing/Speaking cần duyệt,
        // Listening/Reading đã auto-approve khi nộp).
        double? BandOf(SkillType skill) => session.Submissions
            .FirstOrDefault(s => s.SkillType == skill)?.BandScore;

        bool AllDuelyApproved() => session.Submissions.Count == 4
            && session.Submissions.All(sub => sub.EvaluationReports.Any(r => r.IsApproved));

        var l = BandOf(SkillType.Listening);
        var r = BandOf(SkillType.Reading);
        var w = BandOf(SkillType.Writing);
        var s = BandOf(SkillType.Speaking);

        if (AllDuelyApproved() && l.HasValue && r.HasValue && w.HasValue && s.HasValue)
        {
            session.OverallBandScore = BandScoreCalculator.CalculateOverallBand(l.Value, r.Value, w.Value, s.Value);
            // Không "hồi sinh" ca thi đã bị đình chỉ.
            if (session.Status != SessionStatus.Terminated) session.Status = SessionStatus.Evaluated;
            session.EndTime ??= DateTime.UtcNow;
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

        // Chỉ đình chỉ được ca thi ĐANG DIỄN RA.
        if (session.Status != SessionStatus.InProgress)
            throw new InvalidOperationException(
                $"Chỉ có thể đình chỉ ca thi đang diễn ra. Ca thi này đang ở trạng thái {session.Status}.");

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

    // =====================================================================
    // PHÂN CÔNG GIẢNG VIÊN
    // =====================================================================

    public async Task<ExamAssignmentDto?> AssignTeacherAsync(
        Guid examId, Guid teacherId, string? allowedSkills = null, CancellationToken ct = default)
    {
        var exam = await _context.Exams
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == examId, ct);

        if (exam == null) return null;

        var teacher = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == teacherId, ct);

        if (teacher == null || teacher.Role != UserRole.Teacher) return null;

        // Chuẩn hoá AllowedSkills: chỉ nhận giá trị hợp lệ, bỏ trùng.
        string? normalizedSkills = NormalizeSkills(allowedSkills);

        var existing = await _context.ExamAssignments
            .FirstOrDefaultAsync(a => a.ExamId == examId && a.TeacherId == teacherId, ct);

        if (existing != null)
        {
            existing.AllowedSkills = normalizedSkills;
            existing.IsActive = true;
        }
        else
        {
            existing = new ExamAssignment
            {
                ExamId = examId,
                TeacherId = teacherId,
                AllowedSkills = normalizedSkills,
                IsActive = true
            };
            await _context.ExamAssignments.AddAsync(existing, ct);
        }

        await _context.SaveChangesAsync(ct);

        return new ExamAssignmentDto
        {
            Id = existing.Id,
            ExamId = examId,
            ExamTitle = exam.Title,
            TeacherId = teacherId,
            TeacherName = teacher.FullName,
            TeacherUsername = teacher.Username,
            AllowedSkills = existing.AllowedSkills,
            AssignedAt = existing.AssignedAt,
            IsActive = true
        };
    }

    public async Task<bool> UnassignTeacherAsync(Guid assignmentId, CancellationToken ct = default)
    {
        var assignment = await _context.ExamAssignments
            .FirstOrDefaultAsync(a => a.Id == assignmentId, ct);

        if (assignment == null) return false;

        _context.ExamAssignments.Remove(assignment);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<ExamAssignmentDto>> GetAssignmentsAsync(Guid examId, CancellationToken ct = default)
    {
        var rows = await _context.ExamAssignments
            .AsNoTracking()
            .Include(a => a.Exam)
            .Include(a => a.Teacher)
            .Where(a => a.ExamId == examId)
            .OrderBy(a => a.Teacher!.FullName)
            .ToListAsync(ct);

        return rows.Select(a => new ExamAssignmentDto
        {
            Id = a.Id,
            ExamId = a.ExamId,
            ExamTitle = a.Exam?.Title ?? string.Empty,
            TeacherId = a.TeacherId,
            TeacherName = a.Teacher?.FullName ?? string.Empty,
            TeacherUsername = a.Teacher?.Username ?? string.Empty,
            AllowedSkills = a.AllowedSkills,
            AssignedAt = a.AssignedAt,
            IsActive = a.IsActive
        }).ToList();
    }

    public async Task<IReadOnlyList<TeacherOptionDto>> GetTeachersAsync(CancellationToken ct = default)
    {
        return await _context.Users
            .AsNoTracking()
            .Where(u => u.Role == UserRole.Teacher)
            .OrderBy(u => u.FullName)
            .Select(u => new TeacherOptionDto
            {
                Id = u.Id,
                Username = u.Username,
                FullName = u.FullName
            })
            .ToListAsync(ct);
    }

    /// <summary>
    /// Chuẩn hoá chuỗi kỹ năng: chỉ nhận Writing/Speaking (2 kỹ năng cần duyệt),
    /// bỏ giá trị lạ và trùng lặp. Chuỗi rỗng -> null (= tất cả kỹ năng).
    /// </summary>
    private static string? NormalizeSkills(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var valid = raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => Enum.TryParse<SkillType>(s, true, out var skill)
                     && skill is SkillType.Writing or SkillType.Speaking)
            .Select(s => Enum.Parse<SkillType>(s, true).ToString())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        return valid.Count == 0 ? null : string.Join(",", valid);
    }

    // =====================================================================
    // LỊCH SỬ ĐÃ CHẤM + BỘ LỌC
    // =====================================================================

    public async Task<IReadOnlyList<GradedHistoryItemDto>> GetGradedHistoryAsync(
        HistoryFilterDto filter, string? teacherUsername = null, CancellationToken ct = default)
    {
        var query = _context.EvaluationReports
            .AsNoTracking()
            .Include(r => r.Submission)
            .ThenInclude(s => s!.Session)
            .ThenInclude(sess => sess!.User)
            .Include(r => r.Submission)
            .ThenInclude(s => s!.Session)
            .ThenInclude(sess => sess!.Exam)
            .Where(r => r.IsApproved);

        // Lọc theo phân công (giảng viên chỉ thấy đề mình phụ trách).
        if (!string.IsNullOrWhiteSpace(teacherUsername))
        {
            var scope = await GetTeacherAssignmentFilterAsync(teacherUsername, ct);
            if (scope != null)
            {
                var (examIds, allowedSkills) = scope.Value;
                query = query.Where(r => examIds.Contains(r.Submission!.Session!.ExamId));

                if (allowedSkills.Count > 0)
                    query = query.Where(r => allowedSkills.Contains(r.Submission!.SkillType));
            }
        }

        // Từ khoá: tên thí sinh / username / tên đề
        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            string kw = filter.Keyword.Trim().ToLower();
            query = query.Where(r =>
                r.Submission!.Session!.User!.FullName.ToLower().Contains(kw) ||
                r.Submission.Session.User.Username.ToLower().Contains(kw) ||
                r.Submission.Session.Exam!.Title.ToLower().Contains(kw));
        }

        if (filter.SkillType.HasValue)
            query = query.Where(r => r.Submission!.SkillType == filter.SkillType.Value);

        if (filter.FromDate.HasValue)
            query = query.Where(r => r.Submission!.SubmittedAt >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
        {
            // Bao gồm trọn ngày ToDate.
            var to = filter.ToDate.Value.Date.AddDays(1);
            query = query.Where(r => r.Submission!.SubmittedAt < to);
        }

        query = filter.SortBy?.ToLower() == "oldest"
            ? query.OrderBy(r => r.ApprovedAt ?? r.CreatedAt)
            : query.OrderByDescending(r => r.ApprovedAt ?? r.CreatedAt);
        var rows = await query
            .Skip(Math.Max(0, filter.Skip))
            .Take(filter.Take <= 0 ? 100 : Math.Min(filter.Take, 500))
            .ToListAsync(ct);

        return rows.Select(r => new GradedHistoryItemDto
        {
            ReportId = r.Id,
            SubmissionId = r.SubmissionId,
            SessionId = r.Submission?.SessionId ?? Guid.Empty,
            CandidateName = r.Submission?.Session?.User?.FullName ?? "Thí sinh",
            Username = r.Submission?.Session?.User?.Username ?? string.Empty,
            ExamTitle = r.Submission?.Session?.Exam?.Title ?? string.Empty,
            SkillType = r.Submission?.SkillType ?? SkillType.Writing,
            AiProposedBand = r.Submission?.BandScore,
            OfficialBand = r.Submission?.BandScore ?? 0,
            Criterion1 = r.CriterionScore1,
            Criterion2 = r.CriterionScore2,
            Criterion3 = r.CriterionScore3,
            Criterion4 = r.CriterionScore4,
            AiFeedback = r.FeedbackComments ?? string.Empty,
            TeacherFeedback = r.TeacherFeedback ?? string.Empty,
            EssayContent = r.Submission?.EssayContent ?? string.Empty,
            SubmittedAt = r.Submission?.SubmittedAt ?? r.CreatedAt,
            ApprovedAt = r.ApprovedAt,
            ApprovedBy = r.ApprovedBy
        }).ToList();
    }

    public async Task<IReadOnlyList<PendingReviewItemDto>> GetPendingReviewsFilteredAsync(
        HistoryFilterDto filter, string? teacherUsername = null, CancellationToken ct = default)
    {
        var all = await GetPendingReviewsAsync(teacherUsername, ct);

        IEnumerable<PendingReviewItemDto> q = all;

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            string kw = filter.Keyword.Trim().ToLower();
            q = q.Where(p => p.CandidateName.ToLower().Contains(kw));
        }

        if (filter.SkillType.HasValue)
            q = q.Where(p => p.SkillType == filter.SkillType.Value);

        if (filter.FromDate.HasValue)
            q = q.Where(p => p.SubmittedAt >= filter.FromDate.Value);

        if (filter.ToDate.HasValue)
        {
            var to = filter.ToDate.Value.Date.AddDays(1);
            q = q.Where(p => p.SubmittedAt < to);
        }

        q = filter.SortBy?.ToLower() == "oldest"
            ? q.OrderBy(p => p.SubmittedAt)
            : q.OrderByDescending(p => p.SubmittedAt);

        return q.Skip(Math.Max(0, filter.Skip))
                .Take(filter.Take <= 0 ? 100 : Math.Min(filter.Take, 500))
                .ToList();
    }
}
