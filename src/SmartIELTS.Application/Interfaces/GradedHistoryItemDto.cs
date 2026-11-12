using SmartIELTS.Domain.Enums;

namespace SmartIELTS.Application.Interfaces;

/// <summary>
/// Một dòng trong "Lịch sử đã chấm" của giảng viên.
/// Khác với PendingReviewItemDto: đây là bài ĐÃ duyệt, dùng để tra cứu lại.
/// </summary>
public class GradedHistoryItemDto
{
    public Guid ReportId { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid SessionId { get; set; }

    public string CandidateName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string ExamTitle { get; set; } = string.Empty;

    public SkillType SkillType { get; set; }

    /// <summary>Điểm AI đề xuất ban đầu (để so sánh với điểm giảng viên chốt).</summary>
    public double? AiProposedBand { get; set; }

    /// <summary>Điểm chính thức do giảng viên chốt.</summary>
    public double OfficialBand { get; set; }

    public double? Criterion1 { get; set; }
    public double? Criterion2 { get; set; }
    public double? Criterion3 { get; set; }
    public double? Criterion4 { get; set; }

    public string AiFeedback { get; set; } = string.Empty;
    public string TeacherFeedback { get; set; } = string.Empty;

    /// <summary>Nội dung bài làm — cần để hiện lại khi giảng viên sửa điểm.</summary>
    public string EssayContent { get; set; } = string.Empty;

    public DateTime SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? ApprovedBy { get; set; }
}

/// <summary>Bộ lọc dùng chung cho danh sách lịch sử.</summary>
public class HistoryFilterDto
{
    /// <summary>Tìm theo tên thí sinh / username / tên đề (không phân biệt hoa thường).</summary>
    public string? Keyword { get; set; }

    public SkillType? SkillType { get; set; }

    /// <summary>Lọc theo ngày nộp (từ ngày).</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>Lọc theo ngày nộp (đến ngày).</summary>
    public DateTime? ToDate { get; set; }

    /// <summary>Sắp xếp: "newest" (mặc định) hoặc "oldest".</summary>
    public string SortBy { get; set; } = "newest";

    public int Skip { get; set; } = 0;
    public int Take { get; set; } = 100;
}
