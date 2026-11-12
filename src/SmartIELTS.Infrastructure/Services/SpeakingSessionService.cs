using SmartIELTS.Application.DTOs;
using SmartIELTS.Application.GradingStrategies;
using SmartIELTS.Application.Interfaces;
using SmartIELTS.Application.Speaking;
using SmartIELTS.Domain.Common;
using SmartIELTS.Domain.Entities;
using SmartIELTS.Domain.Enums;
using SmartIELTS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace SmartIELTS.Infrastructure.Services;

/// <summary>
/// Điều phối buổi vấn đáp IELTS Speaking dạng hội thoại nhiều lượt với giám khảo ảo.
/// <para>
/// Luồng: giám khảo đặt câu hỏi (kèm text gợi ý) → thí sinh trả lời (gõ hoặc nói thành transcript)
/// → AI chấm lượt đó → sinh câu hỏi tiếp theo → ... → chốt điểm trung bình và lưu bài nộp.
/// </para>
/// </summary>
public class SpeakingSessionService : ISpeakingSessionService
{
    private readonly SmartIeltsDbContext _context;
    private readonly IAiServiceAdapter _aiAdapter;

    public SpeakingSessionService(SmartIeltsDbContext context, IAiServiceAdapter aiAdapter)
    {
        _context = context;
        _aiAdapter = aiAdapter;
    }

    public async Task<SpeakingTurnResultDto> EvaluateTurnAsync(SpeakingTurnRequestDto request, CancellationToken ct = default)
    {
        // Không cho tiếp tục nếu ca thi đã bị đình chỉ.
        var session = await _context.ExamSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == request.SessionId, ct)
            ?? throw new InvalidOperationException("Phiên thi không tồn tại!");

        if (session.Status == SessionStatus.Terminated)
            throw new InvalidOperationException("Ca thi đã bị giám thị đình chỉ, không thể tiếp tục vấn đáp.");

        var state = new SpeakingSessionState
        {
            CurrentPart = request.Part,
            CurrentQuestion = request.Question,
            // PreviousTurns có thể null nếu client gửi JSON "previousTurns": null
            Turns = (request.PreviousTurns ?? new List<SpeakingTurnDto>())
                .Where(t => t != null)
                .OrderBy(t => t.TurnNumber)
                .Select(t => new SpeakingTurn
                {
                    TurnNumber = t.TurnNumber,
                    Part = t.Part,
                    Question = t.Question ?? string.Empty,
                    Answer = t.Answer ?? string.Empty
                })
                .ToList()
        };

        var aiResult = await _aiAdapter.EvaluateSpeakingTurnAsync(state, request.Answer, request.AudioRecordUrl, ct);

        // ---------------------------------------------------------------------
        // LUỒNG HỎI DO CODE QUYẾT ĐỊNH, KHÔNG để AI quyết.
        // Trước đây dùng aiResult.NextQuestion nên AI đếm sai số lượt
        // (Part 3 thành 4-5 câu) -> thí sinh thấy "8 lượt rồi vẫn còn lượt 9".
        // AI chỉ chấm điểm; kịch bản cố định 7 lượt: 3 Part1 + 1 Part2 + 3 Part3.
        // ---------------------------------------------------------------------
        int completedTurns = state.Turns.Count;        // các lượt ĐÃ xong trước lượt hiện tại
        int nextIndex = completedTurns + 1;            // lượt hiện tại vừa xong -> bước kế tiếp

        var nextStep = SpeakingInterviewScript.GetStep(nextIndex);
        bool finished = nextStep == null;

        return new SpeakingTurnResultDto
        {
            WordCount = aiResult.WordCount,
            TurnNumber = completedTurns + 1,

            NextQuestion = nextStep?.Question,
            NextQuestionHints = nextStep?.Hints,
            NextPart = nextStep?.Part ?? request.Part,
            IsFinished = finished,

            // Đã ẩn: đây là dữ liệu AI, không dành cho thí sinh.
            TurnBand = 0,
            FluencyCoherence = 0,
            Pronunciation = 0,
            LexicalResource = 0,
            GrammaticalRangeAccuracy = 0,
            LengthAssessment = string.Empty,
            TurnFeedback = string.Empty,
            ModelAnswer = string.Empty,
            Highlights = new List<SmartIELTS.Application.Interfaces.TextHighlightError>(),
            PhonemeIssues = new List<string>()
        };
    }

    public async Task<SubmitSkillResultDto> FinalizeAsync(
        Guid sessionId,
        List<SpeakingTurnDto> turns,
        string? audioRecordUrl,
        CancellationToken ct = default)
    {
        if (turns == null || turns.Count == 0)
            throw new InvalidOperationException("Chưa có câu trả lời nào để chốt điểm Speaking.");

        // Loại phần tử null / câu trả lời rỗng (client có thể gửi JSON không hợp lệ).
        turns = turns
            .Where(t => t != null && !string.IsNullOrWhiteSpace(t.Answer))
            .ToList();

        if (turns.Count == 0)
            throw new InvalidOperationException("Không có câu trả lời hợp lệ nào để chốt điểm Speaking.");

        var session = await _context.ExamSessions
            .Include(s => s.Exam)
            .ThenInclude(e => e!.Sections)
            .ThenInclude(sec => sec.Questions)
            .Include(s => s.Submissions)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new InvalidOperationException("Phiên thi không tồn tại!");

        if (session.Status == SessionStatus.Terminated)
            throw new InvalidOperationException("Ca thi đã bị giám thị đình chỉ, không thể nộp bài.");

        // Giới hạn thời gian phía server (bù 2 phút cho độ trễ mạng).
        if (session.Exam != null && session.Exam.TotalDurationMinutes > 0)
        {
            var deadline = session.StartTime.AddMinutes(session.Exam.TotalDurationMinutes + 2);
            if (DateTime.UtcNow > deadline)
                throw new InvalidOperationException(
                    $"Đã quá thời gian làm bài ({session.Exam.TotalDurationMinutes} phút). Bài nộp không được chấp nhận.");
        }

        var section = session.Exam?.Sections.FirstOrDefault(s => s.SkillType == SkillType.Speaking)
            ?? throw new InvalidOperationException("Đề thi không có phần Speaking!");

        // Toàn bộ hội thoại được lưu vào EssayContent dưới dạng văn bản dễ đọc cho giảng viên duyệt.
        string transcript = BuildTranscript(turns);

        // ---------------------------------------------------------------------
        // QUAN TRỌNG: chấm điểm CHỈ trên lời của thí sinh.
        // Trước đây đem cả transcript (gồm "Examiner: ..." + header) đi chấm
        // -> AI chấm cả câu hỏi của giám khảo, làm Fluency/Lexical/GRA bị thổi lên.
        // ---------------------------------------------------------------------
        string candidateOnlyText = BuildCandidateOnlyText(turns);

        var submission = new CandidateSubmission
        {
            SessionId = session.Id,
            SkillType = SkillType.Speaking,
            SubmittedAt = DateTime.UtcNow,
            EssayContent = transcript,
            AudioRecordUrl = audioRecordUrl,
            AnswersJson = System.Text.Json.JsonSerializer.Serialize(
                turns.Select(t => new { t.TurnNumber, Part = t.Part.ToString(), t.Question, t.Answer }))
        };

        var aiResult = await _aiAdapter.EvaluateSpeakingAsync(
            section.PassageText ?? "IELTS Speaking", candidateOnlyText, audioRecordUrl);

        // Điểm chốt = trung bình 4 tiêu chí của toàn bài (nhất quán với cách chấm Writing),
        // không dùng OverallBand rời rạc để tránh lệch với điểm từng lượt người học đã thấy.
        double finalBand = BandScoreCalculator.RoundToHalfBand(
            (aiResult.FluencyCoherence + aiResult.Pronunciation + aiResult.LexicalResource + aiResult.GrammaticalRangeAccuracy) / 4.0);

        submission.RawScore = finalBand;
        submission.BandScore = finalBand;

        var report = new EvaluationReport
        {
            SubmissionId = submission.Id,
            EvaluatedBy = EvaluatorType.AiEngine,
            CriterionScore1 = aiResult.FluencyCoherence,
            CriterionScore2 = aiResult.Pronunciation,
            CriterionScore3 = aiResult.LexicalResource,
            CriterionScore4 = aiResult.GrammaticalRangeAccuracy,
            FeedbackComments = BuildFinalFeedback(aiResult, turns),
            DetailedJsonAnalysis = System.Text.Json.JsonSerializer.Serialize(new
            {
                aiResult.PhonemeIssues,
                aiResult.AdaptiveFollowUpQuestion,
                aiResult.LengthAssessment,
                Turns = turns.Select(t => new
                {
                    t.TurnNumber,
                    PartName = t.Part.ToString(),
                    t.Question,
                    t.Answer,
                    WordCount = CountWords(t.Answer)
                })
            }),
            // Speaking cần giảng viên duyệt (Human-in-the-loop).
            IsApproved = false
        };

        await _context.CandidateSubmissions.AddAsync(submission, ct);
        await _context.EvaluationReports.AddAsync(report, ct);

        var previous = session.Submissions
            .Where(s => s.SkillType == SkillType.Speaking && s.Id != submission.Id)
            .ToList();

        if (previous.Count > 0)
        {
            _context.CandidateSubmissions.RemoveRange(previous);
            foreach (var old in previous) session.Submissions.Remove(old);
        }

        session.Submissions.Add(submission);
        RecalculateSession(session);

        await _context.SaveChangesAsync(ct);

        // Speaking cần giảng viên duyệt -> không trả điểm/nhận xét AI cho thí sinh.
        return new SubmitSkillResultDto
        {
            SkillType = SkillType.Speaking,
            ReportId = report.Id,
            IsPendingReview = true,
            ShowsRawScore = false,
            StatusMessage = "Bài vấn đáp đã được nộp. Giảng viên sẽ chấm và xác nhận điểm chính thức cho bạn."
        };
    }

    public async Task<IReadOnlyList<SpeakingPromptDto>> GetPromptsAsync(Guid sessionId, CancellationToken ct = default)
    {
        var session = await _context.ExamSessions
            .AsNoTracking()
            .Include(s => s.Exam)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new InvalidOperationException("Phiên thi không tồn tại!");

        var prompts = await _context.SpeakingPrompts
            .AsNoTracking()
            .Include(p => p.Section)
            .Where(p => p.Section != null && p.Section.ExamId == session.ExamId)
            .OrderBy(p => p.Part)
            .ThenBy(p => p.PromptOrder)
            .ToListAsync(ct);

        return prompts.Select(p => new SpeakingPromptDto
        {
            Id = p.Id,
            Part = p.Part,
            PromptOrder = p.PromptOrder,
            Question = p.QuestionText,
            CandidatePromptText = p.CandidatePromptText,
            PreparationSeconds = p.PreparationSeconds,
            ResponseSeconds = p.ResponseSeconds
        }).ToList();
    }

    /// <summary>
    /// Transcript đầy đủ để giảng viên đọc (có cả câu hỏi giám khảo).
    /// CHỈ dùng để hiển thị — không dùng để chấm điểm.
    /// </summary>
    private static string BuildTranscript(List<SpeakingTurnDto> turns)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("[IELTS SPEAKING - TRANSCRIPT BUỔI VẤN ĐÁP]");
        sb.AppendLine();

        foreach (var part in turns.GroupBy(t => t.Part).OrderBy(g => g.Key))
        {
            sb.AppendLine($"=== {DescribePart(part.Key)} ===");
            foreach (var turn in part.OrderBy(t => t.TurnNumber))
            {
                sb.AppendLine($"Examiner: {turn.Question}");
                sb.AppendLine($"Candidate: {turn.Answer}");
                sb.AppendLine();
            }
        }

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Chỉ lấy lời của thí sinh, nối lại thành một đoạn văn liền mạch để chấm điểm.
    /// Loại bỏ hoàn toàn câu hỏi giám khảo, header và tên part — nếu giữ lại,
    /// AI sẽ chấm cả văn của giám khảo và điểm Fluency/Lexical bị thổi lên sai.
    /// </summary>
    private static string BuildCandidateOnlyText(List<SpeakingTurnDto> turns)
    {
        var answers = turns
            .OrderBy(t => t.TurnNumber)
            .Select(t => (t.Answer ?? string.Empty).Trim())
            .Where(a => a.Length > 0)
            .ToList();

        return string.Join(" ", answers);
    }

    private static string DescribePart(SpeakingPart part) => part switch
    {
        SpeakingPart.Part1Introduction => "PART 1 - INTRODUCTION & INTERVIEW",
        SpeakingPart.Part2LongTurn => "PART 2 - LONG TURN (CUE CARD)",
        SpeakingPart.Part3Discussion => "PART 3 - TWO-WAY DISCUSSION",
        _ => part.ToString()
    };

    private static string BuildFinalFeedback(AiSpeakingEvaluationResult ai, List<SpeakingTurnDto> turns)
    {
        int totalWords = turns.Sum(t => CountWords(t.Answer));
        int turnsCount = turns.Count;

        int answeredPart1 = turns.Count(t => t.Part == SpeakingPart.Part1Introduction);
        int answeredPart2 = turns.Count(t => t.Part == SpeakingPart.Part2LongTurn);
        int answeredPart3 = turns.Count(t => t.Part == SpeakingPart.Part3Discussion);

        return $"Buổi vấn đáp gồm {turnsCount} lượt (Part 1: {answeredPart1}, Part 2: {answeredPart2}, Part 3: {answeredPart3}), " +
               $"tổng {totalWords} từ. " +
               $"Điểm 4 tiêu chí: Fluency {ai.FluencyCoherence}, Pronunciation {ai.Pronunciation}, " +
               $"Lexical {ai.LexicalResource}, Grammar {ai.GrammaticalRangeAccuracy}. " +
               $"{ai.Feedback}";
    }

    private static int CountWords(string text)
        => text.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;

    private static void RecalculateSession(ExamSession session)
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
            if (session.Status != SessionStatus.Terminated) session.Status = SessionStatus.Evaluated;
            session.EndTime ??= DateTime.UtcNow;
        }
        else if (session.Status != SessionStatus.Terminated)
        {
            session.Status = SessionStatus.InProgress;
        }
    }
}
