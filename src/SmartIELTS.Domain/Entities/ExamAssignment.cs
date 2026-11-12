namespace SmartIELTS.Domain.Entities;

/// <summary>
/// Phân công giảng viên chấm một đề thi.
/// <para>
/// Mục đích: mỗi giảng viên chỉ thấy và duyệt được bài của những đề mình phụ trách.
/// Trước đây mọi Teacher đều thấy toàn bộ bài của mọi đề (không kiểm soát chéo).
/// </para>
/// Quy ước: nếu một đề KHÔNG có phân công nào -> Admin phụ trách (hoặc chế độ mở).
/// </summary>
public class ExamAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ExamId { get; set; }
    public Exam? Exam { get; set; }

    public Guid TeacherId { get; set; }
    public User? Teacher { get; set; }

    /// <summary>
    /// Chỉ cho phép giảng viên này chấm các kỹ năng chủ quan được chỉ định.
    /// Null/rỗng = được chấm tất cả kỹ năng của đề.
    /// </summary>
    public string? AllowedSkills { get; set; }

    public DateTime AssignedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;
}
