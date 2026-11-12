using SmartIELTS.Application.DTOs;
using SmartIELTS.Domain.Enums;

namespace SmartIELTS.Application.Interfaces;

public class StartSessionResultDto
{
    public Guid SessionId { get; set; }
    public DateTime StartTime { get; set; }
    public ExamDto Exam { get; set; } = new();

    /// <summary>True = tiếp tục ca thi đang làm dở (không phải ca mới).</summary>
    public bool Resumed { get; set; }

    /// <summary>Các kỹ năng đã nộp trong ca thi này (để client chuyển tới kỹ năng còn thiếu).</summary>
    public List<SkillType> SubmittedSkills { get; set; } = new();
}

public class SubmitSkillResultDto
{
    public SkillType SkillType { get; set; }
    public double RawScore { get; set; }
    public int TotalQuestions { get; set; }
    public double BandScore { get; set; }
    public Guid ReportId { get; set; }

    /// <summary>Overall Band của ca thi sau khi nộp (null nếu chưa đủ 4 kỹ năng).</summary>
    public double? OverallBandScore { get; set; }

    public double? CriterionScore1 { get; set; }
    public double? CriterionScore2 { get; set; }
    public double? CriterionScore3 { get; set; }
    public double? CriterionScore4 { get; set; }
    public string FeedbackComments { get; set; } = string.Empty;
    public string DetailedJsonAnalysis { get; set; } = string.Empty;

    // ---------------------------------------------------------------------
    // Trạng thái duyệt — quyết định UI hiển thị gì cho thí sinh
    // ---------------------------------------------------------------------

    /// <summary>
    /// True = bài đang chờ giảng viên duyệt (Writing/Speaking).
    /// Khi đó <see cref="BandScore"/>, <see cref="FeedbackComments"/>… đã được
    /// xóa về giá trị trung tính để thí sinh không thấy điểm AI.
    /// </summary>
    public bool IsPendingReview { get; set; }

    /// <summary>Thông điệp hiển thị cho thí sinh sau khi nộp.</summary>
    public string StatusMessage { get; set; } = string.Empty;

    /// <summary>Số câu đúng (chỉ có ý nghĩa với Listening/Reading).</summary>
    public bool ShowsRawScore { get; set; }
}

/// <summary>
/// Use case trung tâm của luồng thi, dùng chung cho cả Web API và WPF Kiosk
/// để tránh hai nơi cài đặt lệch nhau.
/// </summary>
public interface IExamWorkflowService
{
    Task<StartSessionResultDto?> StartSessionAsync(Guid userId, Guid examId, CancellationToken ct = default);

    Task<SubmitSkillResultDto> SubmitSkillAsync(SubmitSkillRequestDto request, CancellationToken ct = default);

    Task<SessionResultDto?> GetSessionResultAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>Kết quả đầy đủ cho giảng viên/admin: bao gồm điểm và nhận xét của AI.</summary>
    Task<SessionResultDto?> GetSessionResultForStaffAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>Lưu nháp bài làm (Offline-First autosave). Trả về true nếu tìm thấy ca thi.</summary>
    Task<bool> SaveDraftAsync(AutosaveDraftRequestDto request, CancellationToken ct = default);

    Task<AutosaveDraftRequestDto?> GetDraftAsync(Guid sessionId, SkillType skillType, CancellationToken ct = default);

    /// <summary>UserId chủ sở hữu ca thi (dùng để chặn xem điểm người khác).</summary>
    Task<Guid?> GetSessionOwnerIdAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// Lịch sử các ca thi của MỘT thí sinh (mới nhất trước).
    /// Dùng cho màn hình "Coi điểm của tôi" sau khi đăng nhập.
    /// </summary>
    Task<IReadOnlyList<CandidateSessionHistoryDto>> GetCandidateHistoryAsync(
        Guid userId, CancellationToken ct = default);
}
