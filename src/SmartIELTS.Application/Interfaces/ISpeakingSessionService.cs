using SmartIELTS.Application.DTOs;
using SmartIELTS.Domain.Enums;

namespace SmartIELTS.Application.Interfaces;

public class SpeakingTurnRequestDto
{
    public Guid SessionId { get; set; }

    /// <summary>Câu hỏi giám khảo vừa đặt (để chấm đúng ngữ cảnh).</summary>
    public string Question { get; set; } = string.Empty;

    public SpeakingPart Part { get; set; } = SpeakingPart.Part1Introduction;

    /// <summary>Câu trả lời của thí sinh. Nếu bật nhận dạng giọng nói, đây là transcript do STT sinh ra.</summary>
    public string Answer { get; set; } = string.Empty;

    /// <summary>Đường dẫn tương đối file ghi âm của lượt này (nếu có).</summary>
    public string? AudioRecordUrl { get; set; }

    /// <summary>Các lượt trước đó (client giữ trạng thái hội thoại, không cần lưu DB giữa các lượt).</summary>
    public List<SpeakingTurnDto> PreviousTurns { get; set; } = new();
}

public class SpeakingTurnDto
{
    public int TurnNumber { get; set; }
    public SpeakingPart Part { get; set; }
    public string Question { get; set; } = string.Empty;
    public string Answer { get; set; } = string.Empty;
}

public class SpeakingTurnResultDto
{
    /// <summary>Số thứ tự lượt vừa hoàn thành (1-based).</summary>
    public int TurnNumber { get; set; }

    /// <summary>Tổng số lượt của buổi vấn đáp (chuẩn IELTS: 7).</summary>
    public int TotalTurns { get; set; } = SmartIELTS.Application.Speaking.SpeakingInterviewScript.TotalSteps;

    public double TurnBand { get; set; }
    public double FluencyCoherence { get; set; }
    public double Pronunciation { get; set; }
    public double LexicalResource { get; set; }
    public double GrammaticalRangeAccuracy { get; set; }
    public int WordCount { get; set; }
    public string LengthAssessment { get; set; } = string.Empty;
    public string TurnFeedback { get; set; } = string.Empty;

    /// <summary>Câu hỏi tiếp theo của giám khảo. Null = buổi vấn đáp kết thúc.</summary>
    public string? NextQuestion { get; set; }

    /// <summary>Text gợi ý để thí sinh đọc trước khi trả lời câu hỏi tiếp theo.</summary>
    public string? NextQuestionHints { get; set; }

    public SpeakingPart NextPart { get; set; }

    /// <summary>Bài mẫu tham khảo cho câu vừa trả lời (hiện sau khi nộp, không hiện trước).</summary>
    public string ModelAnswer { get; set; } = string.Empty;

    public List<TextHighlightError> Highlights { get; set; } = new();
    public List<string> PhonemeIssues { get; set; } = new();
    public bool IsFinished { get; set; }
}

public class SpeakingTurnHistoryDto
{
    public double AverageBand { get; set; }
    public int TotalTurns { get; set; }
    public int TotalWords { get; set; }
    public List<SpeakingTurnDto> Turns { get; set; } = new();
}

public interface ISpeakingSessionService
{
    /// <summary>
    /// Chấm một lượt trả lời bằng giám khảo ảo và trả về câu hỏi tiếp theo.
    /// Không ghi DB — toàn bộ hội thoại được nộp một lần ở bước cuối.
    /// </summary>
    Task<SpeakingTurnResultDto> EvaluateTurnAsync(SpeakingTurnRequestDto request, CancellationToken ct = default);

    /// <summary>
    /// Chốt toàn bộ buổi vấn đáp: tính điểm trung bình các lượt, lưu bài nộp Speaking
    /// (kèm transcript đầy đủ) và trả về kết quả chấm cuối cùng.
    /// </summary>
    Task<SubmitSkillResultDto> FinalizeAsync(
        Guid sessionId,
        List<SpeakingTurnDto> turns,
        string? audioRecordUrl,
        CancellationToken ct = default);

    /// <summary>Nộp bài theo kịch bản SpeakingPrompt có sẵn (đọc text và trả lời từng câu).</summary>
    Task<IReadOnlyList<SpeakingPromptDto>> GetPromptsAsync(Guid sessionId, CancellationToken ct = default);
}
