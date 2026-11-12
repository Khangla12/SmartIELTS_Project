using SmartIELTS.Domain.Enums;

namespace SmartIELTS.Application.DTOs;

/// <summary>
/// Kết quả ca thi — DTO dùng cho MỌI client (kể cả thí sinh).
/// <para>
/// QUY TẮC NGHIỆP VỤ: AI chỉ hỗ trợ giảng viên chấm bài. Thí sinh KHÔNG BAO GIỜ thấy
/// điểm hay nhận xét của AI. Chỉ khi giảng viên duyệt xong (<c>IsApproved = true</c>)
/// thì điểm do giảng viên chốt mới được hiển thị.
/// </para>
/// Việc lọc dữ liệu được thực hiện ở <c>DtoMapper.ToSessionResultDto</c>.
/// </summary>
public class SessionResultDto
{
    public Guid SessionId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string ExamTitle { get; set; } = string.Empty;
    public SessionStatus Status { get; set; }

    /// <summary>Overall Band — chỉ có giá trị khi ĐỦ 4 kỹ năng và tất cả đã được giảng viên duyệt.</summary>
    public double? OverallBandScore { get; set; }

    public double? ListeningBand { get; set; }
    public double? ReadingBand { get; set; }
    public double? WritingBand { get; set; }
    public double? SpeakingBand { get; set; }

    public List<SkillSubmissionDto> Submissions { get; set; } = new();

    // ---------------------------------------------------------------------
    // Trạng thái duyệt — để UI biết hiển thị gì
    // ---------------------------------------------------------------------

    /// <summary>True khi mọi kỹ năng đã nộp đều được giảng viên duyệt.</summary>
    public bool IsFullyApproved { get; set; }

    /// <summary>Số kỹ năng đang chờ giảng viên duyệt.</summary>
    public int PendingReviewCount { get; set; }

    /// <summary>Thông điệp trạng thái hiển thị cho thí sinh (tiếng Việt).</summary>
    public string StatusMessage { get; set; } = string.Empty;
}

/// <summary>
/// Bài nộp của một kỹ năng, đã được LỌC theo trạng thái duyệt.
/// Thí sinh chỉ thấy điểm + nhận xét của giảng viên, không thấy dữ liệu AI.
/// </summary>
public class SkillSubmissionDto
{
    public SkillType SkillType { get; set; }

    /// <summary>Chỉ có giá trị khi kỹ năng này đã được giảng viên duyệt.</summary>
    public double? RawScore { get; set; }

    /// <summary>Chỉ có giá trị khi kỹ năng này đã được giảng viên duyệt.</summary>
    public double? BandScore { get; set; }

    public string? EssayContent { get; set; }

    /// <summary>True = đã được giảng viên duyệt (điểm đã chốt).</summary>
    public bool IsApproved { get; set; }

    /// <summary>Đã nộp bài, đang chờ giảng viên chấm.</summary>
    public bool IsPendingReview { get; set; }

    /// <summary>Nhận xét chính thức của giảng viên (chỉ có khi đã duyệt).</summary>
    public string? TeacherFeedback { get; set; }

    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// Báo cáo chi tiết. Với thí sinh, danh sách này CHỈ chứa phần đã duyệt
    /// và đã ẩn toàn bộ dữ liệu AI (điểm tiêu chí, detailed JSON, nhận xét AI).
    /// </summary>
    public List<EvaluationReportDto> Reports { get; set; } = new();
}

public class EvaluationReportDto
{
    public Guid Id { get; set; }
    public EvaluatorType EvaluatedBy { get; set; }

    // Các trường dưới đây để null khi thí sinh xem bài chưa duyệt.
    public double? CriterionScore1 { get; set; }
    public double? CriterionScore2 { get; set; }
    public double? CriterionScore3 { get; set; }
    public double? CriterionScore4 { get; set; }

    /// <summary>Nhận xét của AI — CHỈ trả cho giảng viên, null với thí sinh.</summary>
    public string FeedbackComments { get; set; } = string.Empty;

    /// <summary>JSON phân tích chi tiết — CHỈ trả cho giảng viên, null với thí sinh.</summary>
    public string DetailedJsonAnalysis { get; set; } = string.Empty;

    /// <summary>Nhận xét chính thức của giảng viên — thí sinh được xem.</summary>
    public string? TeacherFeedback { get; set; }

    public bool IsApproved { get; set; }
    public DateTime? ApprovedAt { get; set; }
}
