using SmartIELTS.Domain.Enums;

namespace SmartIELTS.Domain.Entities;

/// <summary>
/// Bản nháp bài làm được lưu định kỳ trong lúc thi (Offline-First).
/// Mỗi ca thi chỉ có tối đa một bản nháp cho mỗi kỹ năng.
/// </summary>
public class ExamDraft
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SessionId { get; set; }
    public ExamSession? Session { get; set; }

    public SkillType SkillType { get; set; }

    /// <summary>Nội dung bài viết (Writing) hoặc transcript (Speaking).</summary>
    public string? DraftContent { get; set; }

    /// <summary>Đáp án dạng JSON của Listening / Reading.</summary>
    public string? DraftAnswersJson { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
