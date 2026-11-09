using SmartIELTS.Application.DTOs;
using SmartIELTS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SmartIELTS.WebApi.Controllers;

/// <summary>
/// Giám khảo ảo cho phần IELTS Speaking: hội thoại nhiều lượt, chấm từng câu trả lời.
/// </summary>
[ApiController]
[Route("api/speaking")]
[Authorize]
public class SpeakingController : ControllerBase
{
    private readonly ISpeakingSessionService _speaking;

    public SpeakingController(ISpeakingSessionService speaking)
    {
        _speaking = speaking;
    }

    /// <summary>Lấy kịch bản các câu hỏi Speaking (Part 1/2/3) kèm text gợi ý cho thí sinh đọc.</summary>
    [HttpGet("{sessionId:guid}/prompts")]
    public async Task<IActionResult> GetPrompts(Guid sessionId, CancellationToken ct)
    {
        try
        {
            return Ok(await _speaking.GetPromptsAsync(sessionId, ct));
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Nộp MỘT câu trả lời: AI chấm lượt đó và trả về câu hỏi tiếp theo (kèm gợi ý).
    /// </summary>
    [HttpPost("turns")]
    public async Task<IActionResult> SubmitTurn([FromBody] SpeakingTurnRequestDto request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Answer))
            return BadRequest(new { message = "Câu trả lời không được để trống." });

        try
        {
            return Ok(await _speaking.EvaluateTurnAsync(request, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Chốt điểm toàn bộ buổi vấn đáp và lưu bài nộp.</summary>
    [HttpPost("finalize")]
    public async Task<IActionResult> Finalize([FromBody] FinalizeSpeakingRequestDto request, CancellationToken ct)
    {
        try
        {
            var result = await _speaking.FinalizeAsync(request.SessionId, request.Turns, request.AudioRecordUrl, ct);
            return Ok(new
            {
                result.SkillType,
                result.BandScore,
                result.RawScore,
                result.TotalQuestions,
                result.OverallBandScore,
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
}

public class FinalizeSpeakingRequestDto
{
    public Guid SessionId { get; set; }
    public List<SpeakingTurnDto> Turns { get; set; } = new();
    public string? AudioRecordUrl { get; set; }
}
