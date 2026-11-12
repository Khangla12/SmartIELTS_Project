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

        // ---------------------------------------------------------------------
        // QUAN TRỌNG: nếu thí sinh đã có ca thi ĐANG LÀM DỞ cho cùng đề thì DÙNG LẠI.
        // Trước đây mỗi lần bấm "Vào phòng thi" tạo ca mới -> 4 kỹ năng bị rải
        // sang 4 ca khác nhau, không bao giờ đủ 4 kỹ năng trong 1 ca để chốt Overall.
        // ---------------------------------------------------------------------
        var existing = await _context.ExamSessions
            .Include(s => s.Submissions)
            .Where(s => s.UserId == userId
                     && s.ExamId == examId
                     && s.Status == SessionStatus.InProgress)
            .OrderByDescending(s => s.StartTime)
            .FirstOrDefaultAsync(ct);

        ExamSession session;

        if (existing != null)
        {
            // Đã nộp đủ 4 kỹ năng -> không cho làm lại, phải tạo ca mới (nếu muốn thi lại).
            bool allFourSubmitted = existing.Submissions
                .Select(s => s.SkillType).Distinct().Count() >= 4;

            if (allFourSubmitted)
            {
                session = NewSession(userId, examId);
                await _context.ExamSessions.AddAsync(session, ct);
                await _context.SaveChangesAsync(ct);
            }
            else
            {
                // Tiếp tục ca cũ: giữ nguyên các kỹ năng đã nộp.
                session = existing;
            }
        }
        else
        {
            session = NewSession(userId, examId);
            await _context.ExamSessions.AddAsync(session, ct);
            await _context.SaveChangesAsync(ct);
        }

        return new StartSessionResultDto
        {
            SessionId = session.Id,
            StartTime = session.StartTime,
            Exam = DtoMapper.ToExamDto(exam),
            // Cho client biết đang tiếp tục ca cũ hay ca mới.
            Resumed = existing != null && session.Id == existing.Id,
            SubmittedSkills = session.Submissions.Select(s => s.SkillType).Distinct().ToList()
        };
    }

    private static ExamSession NewSession(Guid userId, Guid examId) => new()
    {
        UserId = userId,
        ExamId = examId,
        StartTime = DateTime.UtcNow,
        Status = SessionStatus.InProgress
    };

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

        // ---------------------------------------------------------------------
        // Kiểm tra thời gian làm bài phía SERVER.
        // Trước đây chỉ client WPF đếm giờ -> có thể nộp sau khi hết giờ bằng API.
        // Cho phép dư 2 phút để bù độ trễ mạng khi bấm nộp sát giờ.
        // ---------------------------------------------------------------------
        if (session.Exam != null && session.Exam.TotalDurationMinutes > 0)
        {
            var deadline = session.StartTime.AddMinutes(session.Exam.TotalDurationMinutes + 2);

            if (DateTime.UtcNow > deadline)
            {
                throw new InvalidOperationException(
                    $"Đã quá thời gian làm bài ({session.Exam.TotalDurationMinutes} phút). " +
                    "Bài nộp không được chấp nhận.");
            }
        }

        // ---------------------------------------------------------------------
        // Validate nội dung nộp bài — không cho nộp bài trống.
        // ---------------------------------------------------------------------
        if (request.SkillType is SkillType.Listening or SkillType.Reading)
        {
            // Chỉ tính các đáp án thuộc đúng section này.
            var validIds = section.Questions.Select(q => q.Id).ToHashSet();
            int answered = request.Answers?.Count(a => validIds.Contains(a.Key) && !string.IsNullOrWhiteSpace(a.Value)) ?? 0;

            if (answered == 0)
                throw new InvalidOperationException(
                    $"Bạn chưa trả lời câu nào trong phần {request.SkillType}. Hãy làm bài trước khi nộp.");
        }
        else if (string.IsNullOrWhiteSpace(request.EssayContent))
        {
            throw new InvalidOperationException(
                $"Nội dung bài {request.SkillType} đang trống. Hãy trả lời trước khi nộp.");
        }

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

        // ---------------------------------------------------------------------
        // Nộp lại cùng một kỹ năng:
        // - Bài cũ ĐÃ duyệt      -> chặn (bảo toàn điểm chính thức).
        // - Bài cũ ĐANG CHỜ duyệt -> chặn (tránh tạo nhiều bản trùng khi bấm nhanh).
        // - Bài cũ chỉ do AI chấm và chưa ai duyệt -> cho thay thế.
        // ---------------------------------------------------------------------
        var previous = session.Submissions
            .Where(s => s.SkillType == request.SkillType && s.Id != submission.Id)
            .ToList();

        if (previous.Any(p => p.EvaluationReports.Any(r => r.IsApproved)))
        {
            throw new InvalidOperationException(
                $"Kỹ năng {request.SkillType} đã được giảng viên chấm và xác nhận điểm. " +
                "Không thể nộp lại để bảo toàn kết quả chính thức.");
        }

        // Đã nộp rồi và đang chờ duyệt -> không cho nộp lần nữa.
        // (Trước đây bấm nút nhiều lần tạo ra 3 bản Writing trùng.)
        if (previous.Count > 0)
        {
            throw new InvalidOperationException(
                $"Kỹ năng {request.SkillType} đã được nộp và đang chờ giảng viên chấm. " +
                "Không cần nộp lại.");
        }

        session.Submissions.Add(submission);
        RecalculateSessionScore(session, request.SkillType);

        await _context.SaveChangesAsync(ct);

        // ---------------------------------------------------------------------
        // QUY TẮC: thí sinh KHÔNG thấy bất kỳ điểm nào sau khi nộp.
        // Kể cả Listening/Reading (chấm khách quan) cũng chỉ công bố điểm khi
        // CẢ 4 KỸ NĂNG đã được giảng viên duyệt — tránh lộ dần từng phần.
        // ---------------------------------------------------------------------
        return new SubmitSkillResultDto
        {
            SkillType = request.SkillType,
            ReportId = report.Id,
            IsPendingReview = true,
            ShowsRawScore = false,
            StatusMessage = "Bài làm đã được ghi nhận. Điểm sẽ chỉ hiển thị sau khi giảng viên " +
                            "chấm và xác nhận cho toàn bộ bài thi."
        };
    }

    public async Task<SessionResultDto?> GetSessionResultAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await LoadSessionForResultAsync(sessionId, ct);
        if (session == null) return null;

        // Dùng cho THÍ SINH: ẩn toàn bộ dữ liệu AI, chỉ hiện điểm đã được giảng viên duyệt.
        return DtoMapper.ToSessionResultDto(session);
    }

    /// <summary>Bản đầy đủ cho giảng viên/admin (bao gồm điểm và nhận xét AI).</summary>
    public async Task<SessionResultDto?> GetSessionResultForStaffAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await LoadSessionForResultAsync(sessionId, ct);
        if (session == null) return null;

        return DtoMapper.ToSessionResultDtoForStaff(session);
    }

    private Task<ExamSession?> LoadSessionForResultAsync(Guid sessionId, CancellationToken ct)
        => _context.ExamSessions
            .AsNoTracking()
            .Include(s => s.User)
            .Include(s => s.Exam)
            .Include(s => s.Submissions)
            .ThenInclude(sub => sub.EvaluationReports)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);

    public async Task<Guid?> GetSessionOwnerIdAsync(Guid sessionId, CancellationToken ct = default)
    {
        return await _context.ExamSessions
            .AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Select(s => (Guid?)s.UserId)
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<CandidateSessionHistoryDto>> GetCandidateHistoryAsync(
        Guid userId, CancellationToken ct = default)
    {
        var sessions = await _context.ExamSessions
            .AsNoTracking()
            .Include(s => s.Exam)
            .Include(s => s.Submissions)
            .ThenInclude(sub => sub.EvaluationReports)
            .Where(s => s.UserId == userId)
            // Chỉ lấy ca thi ĐÃ NỘP ít nhất 1 kỹ năng.
            // Ca rỗng được tạo mỗi lần đăng nhập -> không có ý nghĩa với thí sinh.
            .Where(s => s.Submissions.Any())
            .OrderByDescending(s => s.StartTime)
            .Take(100)
            .ToListAsync(ct);

        return sessions.Select(s =>
        {
            var views = s.Submissions
                .Select(sub => DtoMapper.ToSkillDtoPublic(sub))
                .ToList();

            bool hasAllFour = views.Count == 4;
            bool allApproved = hasAllFour && views.All(v => v.IsApproved);
            int pending = views.Count(v => !v.IsApproved);

            // -----------------------------------------------------------------
            // THÍ SINH KHÔNG THẤY ĐIỂM cho đến khi ĐỦ 4 KỸ NĂNG và TẤT CẢ đã duyệt.
            // (Trước đây duyệt 1 kỹ năng là thấy điểm kỹ năng đó.)
            // -----------------------------------------------------------------
            bool canPublish = allApproved;

            double? BandOf(SkillType skill)
            {
                if (!canPublish) return null;
                var v = views.FirstOrDefault(x => x.SkillType == skill);
                return v?.BandScore;
            }

            var l = BandOf(SkillType.Listening);
            var r = BandOf(SkillType.Reading);
            var w = BandOf(SkillType.Writing);
            var sp = BandOf(SkillType.Speaking);

            var teacherFeedback = canPublish
                ? s.Submissions
                    .SelectMany(sub => sub.EvaluationReports)
                    .Where(rep => rep.IsApproved && !string.IsNullOrWhiteSpace(rep.TeacherFeedback))
                    .Select(rep => rep.TeacherFeedback!)
                    .FirstOrDefault()
                : null;

            return new CandidateSessionHistoryDto
            {
                SessionId = s.Id,
                ExamTitle = s.Exam?.Title ?? "Kỳ thi IELTS",
                StartTime = s.StartTime,
                EndTime = s.EndTime,
                Status = s.Status,
                SubmittedSkills = views.Count,
                PendingReviewCount = pending,
                IsFullyApproved = allApproved,
                ListeningBand = l,
                ReadingBand = r,
                WritingBand = w,
                SpeakingBand = sp,
                OverallBand = allApproved && l.HasValue && r.HasValue && w.HasValue && sp.HasValue
                    ? s.OverallBandScore ?? BandScoreCalculator.CalculateOverallBand(l.Value, r.Value, w.Value, sp.Value)
                    : null,
                TeacherFeedback = teacherFeedback,
                StatusText = BuildHistoryStatusText(s, views.Count, pending, allApproved)
            };
        }).ToList();
    }

    private static string BuildHistoryStatusText(ExamSession session, int submitted, int pending, bool allApproved)
    {
        if (session.Status == SessionStatus.Terminated)
            return "Ca thi đã bị giám thị đình chỉ";

        if (submitted == 0)
            return "Chưa nộp bài";

        if (allApproved)
            return "Đã có điểm chính thức";

        if (pending > 0)
            return $"{pending} kỹ năng đang chờ giảng viên chấm";

        return $"Đã nộp {submitted}/4 kỹ năng";
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
