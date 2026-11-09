using SmartIELTS.Domain.Enums;

namespace SmartIELTS.Domain.Entities;

/// <summary>
/// Kịch bản một cửa ải Speaking: câu hỏi của giám khảo, gợi ý trả lời và thời gian trả lời.
/// Mỗi đề thi có một bộ câu hỏi Speaking gắn với phần thi <see cref="SkillType.Speaking"/>.
/// </summary>
public class SpeakingPrompt
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SectionId { get; set; }
    public ExamSection? Section { get; set; }

    public SpeakingPart Part { get; set; }

    /// <summary>Thứ tự câu hỏi trong Part (1, 2, 3...).</summary>
    public int PromptOrder { get; set; }

    /// <summary>Câu hỏi giám khảo đặt ra (tiếng Anh).</summary>
    public string QuestionText { get; set; } = string.Empty;

    /// <summary>
    /// Văn bản cho thí sinh đọc và trả lời — gợi ý ý tưởng / từ vựng / cấu trúc.
    /// Đây là "text cho thí sinh đọc trả lời vấn đáp".
    /// </summary>
    public string CandidatePromptText { get; set; } = string.Empty;

    /// <summary>Thời gian chuẩn bị (giây) - Part 2 dùng 60 giây.</summary>
    public int PreparationSeconds { get; set; }

    /// <summary>Thời gian trả lời tối đa (giây).</summary>
    public int ResponseSeconds { get; set; } = 60;
}
