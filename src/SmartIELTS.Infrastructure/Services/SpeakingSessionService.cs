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
            Turns = request.PreviousTurns
                .OrderBy(t => t.TurnNumber)
                .Select(t => new SpeakingTurn
                {
                    TurnNumber = t.TurnNumber,
                    Part = t.Part,
                    Question = t.Question,
                    Answer = t.Answer
                })
                .ToList()
        };

        var aiResult = await _aiAdapter.EvaluateSpeakingTurnAsync(state, request.Answer, request.AudioRecordUrl, ct);

        // Part của câu hỏi tiếp theo: nếu AI trả về câu hỏi thì giữ logic chuyển phần của adapter.
        var nextPart = ResolveNextPart(request.Part, state.Turns.Count, aiResult.NextQuestion != null);

        return new SpeakingTurnResultDto
        {
            TurnBand = aiResult.TurnBand,
            FluencyCoherence = aiResult.FluencyCoherence,
            Pronunciation = aiResult.Pronunciation,
            LexicalResource = aiResult.LexicalResource,
            GrammaticalRangeAccuracy = aiResult.GrammaticalRangeAccuracy,
            WordCount = aiResult.WordCount,
            LengthAssessment = aiResult.LengthAssessment,
            TurnFeedback = aiResult.TurnFeedback,
            NextQuestion = aiResult.NextQuestion,
            NextQuestionHints = aiResult.NextQuestionHints,
            NextPart = nextPart,
            ModelAnswer = aiResult.ModelAnswer,
            Highlights = aiResult.Highlights,
            PhonemeIssues = aiResult.PhonemeIssues,
            IsFinished = string.IsNullOrWhiteSpace(aiResult.NextQuestion)
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

        var session = await _context.ExamSessions
            .Include(s => s.Exam)
            .ThenInclude(e => e!.Sections)
            .ThenInclude(sec => sec.Questions)
            .Include(s => s.Submissions)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct)
            ?? throw new InvalidOperationException("Phiên thi không tồn tại!");

        if (session.Status == SessionStatus.Terminated)
            throw new InvalidOperationException("Ca thi đã bị giám thị đình chỉ, không thể nộp bài.");

        var section = session.Exam?.Sections.FirstOrDefault(s => s.SkillType == SkillType.Speaking)
            ?? throw new InvalidOperationException("Đề thi không có phần Speaking!");

        // Toàn bộ hội thoại được lưu vào EssayContent dưới dạng văn bản dễ đọc cho giảng viên duyệt.
        string transcript = BuildTranscript(turns);

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
            section.PassageText ?? "IELTS Speaking", transcript, audioRecordUrl);

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

        return new SubmitSkillResultDto
        {
            SkillType = SkillType.Speaking,
            RawScore = submission.RawScore ?? 0,
            TotalQuestions = turns.Count,
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

    private static SpeakingPart ResolveNextPart(SpeakingPart current, int completedTurns, bool hasNextQuestion)
    {
        if (!hasNextQuestion) return current;

        return current switch
        {
            // Part 1 có 3 câu -> câu thứ 4 chuyển sang Part 2.
            SpeakingPart.Part1Introduction => completedTurns >= 2 ? SpeakingPart.Part2LongTurn : SpeakingPart.Part1Introduction,
            SpeakingPart.Part2LongTurn => SpeakingPart.Part3Discussion,
            _ => SpeakingPart.Part3Discussion
        };
    }

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
