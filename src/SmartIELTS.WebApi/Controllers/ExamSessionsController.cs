using System.Security.Claims;
using SmartIELTS.Application.DTOs;
using SmartIELTS.Application.Interfaces;
using SmartIELTS.WebApi.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SmartIELTS.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class ExamSessionsController : ControllerBase
{
    private readonly IExamWorkflowService _workflow;

    public ExamSessionsController(IExamWorkflowService workflow)
    {
        _workflow = workflow;
    }

    /// <summary>
    /// Bắt đầu ca thi. Thí sinh chỉ được tạo ca cho CHÍNH MÌNH;
    /// Teacher/Admin có thể tạo hộ (dùng cho phòng máy).
    /// </summary>
    [HttpPost("start")]
    public async Task<IActionResult> StartSession([FromBody] StartSessionRequestDto request, CancellationToken ct)
    {
        Guid? currentUserId = SessionAccessGuard.CurrentUserId(User);

        // Chặn giả mạo: thí sinh không được gửi UserId của người khác.
        if (!SessionAccessGuard.IsStaff(User))
        {
            if (currentUserId == null || currentUserId.Value != request.UserId)
                return Forbid();
        }

        var result = await _workflow.StartSessionAsync(request.UserId, request.ExamId, ct);
        if (result == null)
        {
            return NotFound(new { message = "Đề thi hoặc thí sinh không tồn tại!" });
        }

        return Ok(new
        {
            SessionId = result.SessionId,
            StartTime = result.StartTime,
            Exam = result.Exam
        });
    }

    [HttpPost("autosave")]
    public async Task<IActionResult> AutosaveDraft([FromBody] AutosaveDraftRequestDto request, CancellationToken ct)
    {
        if (!await SessionAccessGuard.CanAccessAsync(User, _workflow, request.SessionId, ct))
            return Forbid();

        bool saved = await _workflow.SaveDraftAsync(request, ct);
        if (!saved) return NotFound(new { message = "Phiên thi không tồn tại!" });

        return Ok(new { message = "Lưu nháp thành công", timestamp = DateTime.UtcNow });
    }

    [HttpGet("{id}/draft")]
    public async Task<IActionResult> GetDraft(Guid id, [FromQuery] SmartIELTS.Domain.Enums.SkillType skillType, CancellationToken ct)
    {
        if (!await SessionAccessGuard.CanAccessAsync(User, _workflow, id, ct))
            return Forbid();

        var draft = await _workflow.GetDraftAsync(id, skillType, ct);
        if (draft == null) return NotFound(new { message = "Chưa có bản nháp cho kỹ năng này!" });

        return Ok(draft);
    }

    [HttpPost("submit-skill")]
    public async Task<IActionResult> SubmitSkill([FromBody] SubmitSkillRequestDto request, CancellationToken ct)
    {
        if (!await SessionAccessGuard.CanAccessAsync(User, _workflow, request.SessionId, ct))
            return Forbid();

        try
        {
            var result = await _workflow.SubmitSkillAsync(request, ct);
            return Ok(new
            {
                result.SkillType,
                result.BandScore,
                result.RawScore,
                result.TotalQuestions,
                result.IsPendingReview,
                result.ShowsRawScore,
                result.StatusMessage,
                Report = new
                {
                    result.ReportId,
                    result.CriterionScore1,
                    result.CriterionScore2,
                    result.CriterionScore3,
                    result.CriterionScore4,
                    result.FeedbackComments,
                    result.DetailedJsonAnalysis
                }
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id}/result")]
    public async Task<IActionResult> GetSessionResult(Guid id, CancellationToken ct)
    {
        bool isStaff = SessionAccessGuard.IsStaff(User);

        // Kiểm tra quyền TRƯỚC khi tải dữ liệu (tránh oracle dò GUID).
        if (!await SessionAccessGuard.CanAccessAsync(User, _workflow, id, ct))
            return Forbid();

        var result = isStaff
            ? await _workflow.GetSessionResultForStaffAsync(id, ct)
            : await _workflow.GetSessionResultAsync(id, ct);

        if (result == null) return NotFound(new { message = "Không tìm thấy kết quả ca thi!" });

        return Ok(result);
    }

    /// <summary>
    /// Lịch sử thi của chính thí sinh đang đăng nhập (màn hình "Coi điểm của tôi").
    /// </summary>
    [HttpGet("my-history")]
    public async Task<IActionResult> GetMyHistory(CancellationToken ct)
    {
        var userId = SessionAccessGuard.CurrentUserId(User);
        if (userId == null) return Forbid();

        return Ok(await _workflow.GetCandidateHistoryAsync(userId.Value, ct));
    }
}
