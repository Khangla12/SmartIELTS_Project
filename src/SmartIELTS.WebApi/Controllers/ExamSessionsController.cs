using SmartIELTS.Application.DTOs;
using SmartIELTS.Application.Interfaces;
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

    [HttpPost("start")]
    public async Task<IActionResult> StartSession([FromBody] StartSessionRequestDto request, CancellationToken ct)
    {
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
        bool saved = await _workflow.SaveDraftAsync(request, ct);
        if (!saved) return NotFound(new { message = "Phiên thi không tồn tại!" });

        return Ok(new { message = "Lưu nháp thành công", timestamp = DateTime.UtcNow });
    }

    [HttpGet("{id}/draft")]
    public async Task<IActionResult> GetDraft(Guid id, [FromQuery] SmartIELTS.Domain.Enums.SkillType skillType, CancellationToken ct)
    {
        var draft = await _workflow.GetDraftAsync(id, skillType, ct);
        if (draft == null) return NotFound(new { message = "Chưa có bản nháp cho kỹ năng này!" });

        return Ok(draft);
    }

    [HttpPost("submit-skill")]
    public async Task<IActionResult> SubmitSkill([FromBody] SubmitSkillRequestDto request, CancellationToken ct)
    {
        try
        {
            var result = await _workflow.SubmitSkillAsync(request, ct);
            return Ok(new
            {
                result.SkillType,
                result.BandScore,
                result.RawScore,
                result.TotalQuestions,
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
        var result = await _workflow.GetSessionResultAsync(id, ct);
        if (result == null) return NotFound(new { message = "Không tìm thấy kết quả ca thi!" });

        return Ok(result);
    }
}
