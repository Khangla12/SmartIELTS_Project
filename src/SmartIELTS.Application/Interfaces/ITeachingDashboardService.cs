using SmartIELTS.Application.DTOs;

namespace SmartIELTS.Application.Interfaces;

/// <summary>
/// Use case của giảng viên / giám thị: duyệt điểm (Human-in-the-loop) và giám sát phòng thi.
/// </summary>
public interface ITeachingDashboardService
{
    Task<IReadOnlyList<PendingReviewItemDto>> GetPendingReviewsAsync(CancellationToken ct = default);

    /// <summary>Duyệt điểm và tính lại Overall Band của ca thi. Trả về null nếu không tìm thấy báo cáo.</summary>
    Task<ScoreApprovalResultDto?> ApproveScoreAsync(TeacherApproveScoreRequestDto request, CancellationToken ct = default);

    Task<IReadOnlyList<ExamSessionItemDto>> GetActiveSessionsAsync(int take = 50, CancellationToken ct = default);

    Task<IReadOnlyList<ProctoringLogItemDto>> GetProctoringLogsAsync(int take = 100, CancellationToken ct = default);

    Task<bool> LogViolationAsync(ProctoringViolationDto request, CancellationToken ct = default);

    /// <summary>Đình chỉ ca thi: chuyển trạng thái Terminated, chốt thời điểm kết thúc và ghi biên bản.</summary>
    Task<SessionTerminationResultDto?> TerminateSessionAsync(
        Guid sessionId, string reason, string? proctorUsername = null, CancellationToken ct = default);
}

public class SessionTerminationResultDto
{
    public Guid SessionId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public DateTime TerminatedAt { get; set; }
    public string Reason { get; set; } = string.Empty;
    /// <summary>Band đã chấm được tính đến thời điểm đình chỉ (null nếu chưa có bài nào).</summary>
    public double? PartialOverallBand { get; set; }
    public int SubmittedSkillCount { get; set; }
}

public class ScoreApprovalResultDto
{
    public Guid ReportId { get; set; }
    /// <summary>Band chính thức được tính lại từ 4 tiêu chí (không lấy số client gửi).</summary>
    public double OfficialBandScore { get; set; }
    /// <summary>Overall Band của ca thi sau khi duyệt (null nếu chưa đủ 4 kỹ năng).</summary>
    public double? OverallBandScore { get; set; }
}
