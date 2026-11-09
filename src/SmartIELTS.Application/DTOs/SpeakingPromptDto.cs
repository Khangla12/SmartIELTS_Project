using SmartIELTS.Domain.Enums;

namespace SmartIELTS.Application.DTOs;

/// <summary>Một cửa ải Speaking: câu hỏi giám khảo + text gợi ý cho thí sinh đọc.</summary>
public class SpeakingPromptDto
{
    public Guid Id { get; set; }
    public SpeakingPart Part { get; set; }
    public int PromptOrder { get; set; }
    public string Question { get; set; } = string.Empty;
    public string CandidatePromptText { get; set; } = string.Empty;
    public int PreparationSeconds { get; set; }
    public int ResponseSeconds { get; set; }
}
