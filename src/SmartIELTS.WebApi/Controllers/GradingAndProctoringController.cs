using SmartIELTS.Application.DTOs;
using SmartIELTS.Application.Interfaces;
using SmartIELTS.Domain.Enums;
using SmartIELTS.WebApi.Hubs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace SmartIELTS.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Teacher,Admin")]
public class GradingController : ControllerBase
{
    private readonly ITeachingDashboardService _dashboard;
    private readonly IHubContext<ProctoringHub> _hubContext;

    public GradingController(ITeachingDashboardService dashboard, IHubContext<ProctoringHub> hubContext)
    {
        _dashboard = dashboard;
        _hubContext = hubContext;
    }

    [HttpGet("pending-reviews")]
    public async Task<IActionResult> GetPendingReviews(CancellationToken ct)
        => Ok(await _dashboard.GetPendingReviewsAsync(ct));

    [HttpPost("approve-score")]
    public async Task<IActionResult> ApproveScore([FromBody] TeacherApproveScoreRequestDto request, CancellationToken ct)
    {
        var result = await _dashboard.ApproveScoreAsync(request, ct);
        if (result == null) return NotFound(new { message = "Không tìm thấy bản đánh giá!" });

        return Ok(new
        {
            message = "Giáo viên đã duyệt điểm thành công!",
            approvedBand = result.OfficialBandScore,
            overallBand = result.OverallBandScore
        });
    }
}

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ProctoringController : ControllerBase
{
    private readonly ITeachingDashboardService _dashboard;
    private readonly IHubContext<ProctoringHub> _hubContext;

    public ProctoringController(ITeachingDashboardService dashboard, IHubContext<ProctoringHub> hubContext)
    {
        _dashboard = dashboard;
        _hubContext = hubContext;
    }

    [HttpPost("log")]
    public async Task<IActionResult> LogViolation([FromBody] ProctoringViolationDto request, CancellationToken ct)
    {
        bool logged = await _dashboard.LogViolationAsync(request, ct);
        if (!logged) return NotFound(new { message = "Phiên thi không tồn tại!" });

        // Broadcast tới phòng giám sát đang mở (Observer / Pub-Sub qua SignalR).
        await _hubContext.Clients.Group($"Proctoring_{request.SessionId}").SendAsync("ViolationAlertReceived", new
        {
            SessionId = request.SessionId,
            ViolationType = request.ViolationType.ToString(),
            Description = request.Description,
            Timestamp = DateTime.UtcNow
        }, ct);

        return Ok(new { message = "Đã ghi nhận vi phạm Kiosk" });
    }

    [HttpGet("logs/{sessionId}")]
    public async Task<IActionResult> GetSessionLogs(Guid sessionId, CancellationToken ct)
    {
        var logs = (await _dashboard.GetProctoringLogsAsync(1000, ct))
            .Where(l => l.SessionId == sessionId)
            .ToList();

        return Ok(logs);
    }

    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions(CancellationToken ct)
        => Ok(await _dashboard.GetActiveSessionsAsync(50, ct));

    /// <summary>Giám thị đình chỉ ca thi đang diễn ra (vi phạm quy chế).</summary>
    [HttpPost("sessions/{sessionId}/terminate")]
    public async Task<IActionResult> TerminateSession(Guid sessionId, [FromBody] TerminateSessionRequestDto request, CancellationToken ct)
    {
        string? proctor = User.Identity?.Name;

        var result = await _dashboard.TerminateSessionAsync(sessionId, request.Reason, proctor, ct);
        if (result == null) return NotFound(new { message = "Ca thi không tồn tại!" });

        return Ok(new
        {
            message = "Đã đình chỉ ca thi và ghi biên bản.",
            result.SessionId,
            result.CandidateName,
            result.TerminatedAt,
            result.Reason,
            result.PartialOverallBand,
            result.SubmittedSkillCount
        });
    }
}
