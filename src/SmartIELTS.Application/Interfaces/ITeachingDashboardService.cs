using SmartIELTS.Application.DTOs;

namespace SmartIELTS.Application.Interfaces;

/// <summary>
/// Use case của giảng viên / giám thị: duyệt điểm (Human-in-the-loop) và giám sát phòng thi.
/// </summary>
public interface ITeachingDashboardService
{
    /// <summary>
    /// Danh sách bài chờ duyệt, ĐÃ LỌC theo phân công của giảng viên.
    /// <paramref name="teacherUsername"/> = null nghĩa là Admin (xem tất cả).
    /// </summary>
    Task<IReadOnlyList<PendingReviewItemDto>> GetPendingReviewsAsync(
        string? teacherUsername = null, CancellationToken ct = default);

    /// <summary>Duyệt điểm. Kiểm tra giảng viên có được phân công đề này không.</summary>
    Task<ScoreApprovalResultDto?> ApproveScoreAsync(
        TeacherApproveScoreRequestDto request, string? teacherUsername = null, CancellationToken ct = default);

    Task<IReadOnlyList<ExamSessionItemDto>> GetActiveSessionsAsync(int take = 50, CancellationToken ct = default);

    Task<IReadOnlyList<ProctoringLogItemDto>> GetProctoringLogsAsync(int take = 100, CancellationToken ct = default);

    Task<bool> LogViolationAsync(ProctoringViolationDto request, CancellationToken ct = default);

    /// <summary>Đình chỉ ca thi: chuyển trạng thái Terminated, chốt thời điểm kết thúc và ghi biên bản.</summary>
    Task<SessionTerminationResultDto?> TerminateSessionAsync(
        Guid sessionId, string reason, string? proctorUsername = null, CancellationToken ct = default);

    // ---------------------------------------------------------------------
    // Phân công giảng viên
    // ---------------------------------------------------------------------

    /// <summary>Admin gán một giảng viên vào đề thi.</summary>
    Task<ExamAssignmentDto?> AssignTeacherAsync(
        Guid examId, Guid teacherId, string? allowedSkills = null, CancellationToken ct = default);

    /// <summary>Bỏ phân công.</summary>
    Task<bool> UnassignTeacherAsync(Guid assignmentId, CancellationToken ct = default);

    /// <summary>Danh sách phân công của một đề.</summary>
    Task<IReadOnlyList<ExamAssignmentDto>> GetAssignmentsAsync(Guid examId, CancellationToken ct = default);

    /// <summary>Danh sách giảng viên có thể phân công.</summary>
    Task<IReadOnlyList<TeacherOptionDto>> GetTeachersAsync(CancellationToken ct = default);

    /// <summary>
    /// Lịch sử các bài ĐÃ được giảng viên chấm (đã duyệt).
    /// Lọc theo phân công: giảng viên chỉ thấy bài của đề mình phụ trách.
    /// </summary>
    Task<IReadOnlyList<GradedHistoryItemDto>> GetGradedHistoryAsync(
        HistoryFilterDto filter, string? teacherUsername = null, CancellationToken ct = default);

    /// <summary>Lịch sử bài chờ duyệt có bộ lọc (dùng cho tab "Chờ duyệt").</summary>
    Task<IReadOnlyList<PendingReviewItemDto>> GetPendingReviewsFilteredAsync(
        HistoryFilterDto filter, string? teacherUsername = null, CancellationToken ct = default);
}

public class ExamAssignmentDto
{
    public Guid Id { get; set; }
    public Guid ExamId { get; set; }
    public string ExamTitle { get; set; } = string.Empty;
    public Guid TeacherId { get; set; }
    public string TeacherName { get; set; } = string.Empty;
    public string TeacherUsername { get; set; } = string.Empty;
    public string? AllowedSkills { get; set; }
    public DateTime AssignedAt { get; set; }
    public bool IsActive { get; set; }
}

public class TeacherOptionDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
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
