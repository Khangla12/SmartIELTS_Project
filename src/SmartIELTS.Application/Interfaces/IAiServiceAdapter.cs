using SmartIELTS.Domain.Enums;

namespace SmartIELTS.Application.Interfaces;

public class AiWritingEvaluationResult
{
    public double TaskResponse { get; set; }
    public double CoherenceCohesion { get; set; }
    public double LexicalResource { get; set; }
    public double GrammaticalRangeAccuracy { get; set; }
    public double OverallBand { get; set; }
    public string Feedback { get; set; } = string.Empty;
    public List<TextHighlightError> Highlights { get; set; } = new();
    public string ModelAnswerBand8 { get; set; } = string.Empty;
}

public class TextHighlightError
{
    public string ErrorType { get; set; } = "Grammar"; // Grammar (Red), Vocabulary (Yellow), Highlight (Green)
    public string OriginalText { get; set; } = string.Empty;
    public string Suggestion { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public string SeverityColor { get; set; } = "Red"; // "Red", "Yellow", "Green"
}

public class AiSpeakingEvaluationResult
{
    public double FluencyCoherence { get; set; }
    public double Pronunciation { get; set; }
    public double LexicalResource { get; set; }
    public double GrammaticalRangeAccuracy { get; set; }
    public double OverallBand { get; set; }
    public string Feedback { get; set; } = string.Empty;
    public List<string> PhonemeIssues { get; set; } = new();
    public string AdaptiveFollowUpQuestion { get; set; } = string.Empty;

    // --- Bổ sung cho phần vấn đáp có hội thoại nhiều lượt ---

    /// <summary>Nhận xét riêng cho câu trả lời vừa nộp.</summary>
    public string TurnFeedback { get; set; } = string.Empty;

    /// <summary>Câu hỏi tiếp theo của giám khảo (null nếu kết thúc buổi vấn đáp).</summary>
    public string? NextQuestion { get; set; }

    /// <summary>Gợi ý nội dung/từ vựng để thí sinh đọc và trả lời câu hỏi tiếp theo.</summary>
    public string? NextQuestionHints { get; set; }

    /// <summary>Điểm ước lượng từng lượt (dùng để hiển thị tiến bộ).</summary>
    public double TurnBand { get; set; }

    /// <summary>Lỗi ngữ pháp/từ vựng phát hiện trong câu trả lời.</summary>
    public List<TextHighlightError> Highlights { get; set; } = new();

    /// <summary>Bài mẫu tham khảo cho câu hỏi vừa trả lời.</summary>
    public string ModelAnswer { get; set; } = string.Empty;

    /// <summary>Số từ thí sinh đã nói (từ transcript).</summary>
    public int WordCount { get; set; }

    /// <summary>Đánh giá độ dài: quá ngắn / đạt / tốt so với yêu cầu.</summary>
    public string LengthAssessment { get; set; } = string.Empty;
}

/// <summary>Một lượt hội thoại vấn đáp (câu hỏi giám khảo + câu trả lời thí sinh).</summary>
public class SpeakingTurn
{
    public int TurnNumber { get; set; }
    public SpeakingPart Part { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
    public DateTime AnsweredAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Trạng thái buổi vấn đáp: các lượt đã trả lời + câu hỏi hiện tại.
/// Dùng để giám khảo AI hỏi tiếp thích ứng (Adaptive follow-up) dựa trên câu trả lời trước.
/// </summary>
public class SpeakingSessionState
{
    public SpeakingPart CurrentPart { get; set; } = SpeakingPart.Part1Introduction;
    public string CurrentQuestion { get; set; } = string.Empty;
    public List<SpeakingTurn> Turns { get; set; } = new();

    public int TurnCount => Turns.Count;
    public int WordsSpoken => Turns.Sum(t => t.Answer.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);
}

public interface IAiServiceAdapter
{
    Task<AiWritingEvaluationResult> EvaluateWritingAsync(string topic, string essayText);

    /// <summary>Chấm điểm toàn bộ bài Speaking (dùng cho API nộp bài một lần).</summary>
    Task<AiSpeakingEvaluationResult> EvaluateSpeakingAsync(string topic, string transcriptText, string? audioUrl);

    /// <summary>
    /// Chấm một lượt trả lời và sinh câu hỏi tiếp theo cho giám khảo ảo.
    /// Trả về <see cref="AiSpeakingEvaluationResult.NextQuestion"/> = null khi đã kết thúc.
    /// </summary>
    Task<AiSpeakingEvaluationResult> EvaluateSpeakingTurnAsync(
        SpeakingSessionState state,
        string answer,
        string? audioUrl,
        CancellationToken ct = default);
}
