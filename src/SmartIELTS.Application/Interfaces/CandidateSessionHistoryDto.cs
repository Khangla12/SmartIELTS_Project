using SmartIELTS.Domain.Enums;

namespace SmartIELTS.Application.Interfaces;

/// <summary>
/// Một dòng trong lịch sử thi của thí sinh (màn hình "Coi điểm của tôi").
/// Chỉ chứa dữ liệu thí sinh được phép thấy — không có điểm/nhận xét của AI.
/// </summary>
public class CandidateSessionHistoryDto
{
    public Guid SessionId { get; set; }
    public string ExamTitle { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime? EndTime { get; set; }
    public SessionStatus Status { get; set; }

    /// <summary>Chỉ có giá trị khi cả 4 kỹ năng đã được giảng viên duyệt.</summary>
    public double? OverallBand { get; set; }

    /// <summary>Điểm từng kỹ năng — chỉ hiện với kỹ năng đã duyệt.</summary>
    public double? ListeningBand { get; set; }
    public double? ReadingBand { get; set; }
    public double? WritingBand { get; set; }
    public double? SpeakingBand { get; set; }

    /// <summary>Số kỹ năng đã nộp (0-4).</summary>
    public int SubmittedSkills { get; set; }

    /// <summary>Số kỹ năng đang chờ giảng viên chấm.</summary>
    public int PendingReviewCount { get; set; }

    public bool IsFullyApproved { get; set; }

    /// <summary>Trạng thái hiển thị cho thí sinh (tiếng Việt).</summary>
    public string StatusText { get; set; } = string.Empty;

    /// <summary>Nhận xét chính thức của giảng viên (chỉ khi đã duyệt).</summary>
    public string? TeacherFeedback { get; set; }
}
