using SmartIELTS.Application.DTOs;
using SmartIELTS.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SmartIELTS.WebApi.Controllers;

/// <summary>
/// Quản trị đề thi (Admin): Exam -> Section -> Question.
/// Toàn bộ controller yêu cầu role Admin.
/// </summary>
[ApiController]
[Route("api/admin/exams")]
[Authorize(Roles = "Admin")]
public class AdminExamsController : ControllerBase
{
    private readonly IExamManagementService _exams;
    private readonly ITeachingDashboardService _dashboard;

    public AdminExamsController(IExamManagementService exams, ITeachingDashboardService dashboard)
    {
        _exams = exams;
        _dashboard = dashboard;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
        => Ok(await _exams.GetAllExamsAsync(ct));

    [HttpGet("{examId:guid}")]
    public async Task<IActionResult> GetById(Guid examId, CancellationToken ct)
    {
        var exam = await _exams.GetExamAsync(examId, ct);
        return exam == null ? NotFound(new { message = "Không tìm thấy đề thi!" }) : Ok(exam);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateExamRequestDto request, CancellationToken ct)
    {
        var created = await _exams.CreateExamAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { examId = created.Id }, created);
    }

    [HttpPut("{examId:guid}")]
    public async Task<IActionResult> Update(Guid examId, [FromBody] UpdateExamRequestDto request, CancellationToken ct)
    {
        var updated = await _exams.UpdateExamAsync(examId, request, ct);
        return updated == null ? NotFound(new { message = "Không tìm thấy đề thi!" }) : Ok(updated);
    }

    [HttpDelete("{examId:guid}")]
    public async Task<IActionResult> Delete(Guid examId, CancellationToken ct)
    {
        bool exists = await _exams.GetExamAsync(examId, ct) != null;
        if (!exists) return NotFound(new { message = "Không tìm thấy đề thi!" });

        bool deleted = await _exams.DeleteExamAsync(examId, ct);
        if (!deleted)
        {
            return Conflict(new
            {
                message = "Không thể xóa: đề thi đã có ca thi. Hãy chuyển IsActive = false thay vì xóa."
            });
        }

        return Ok(new { message = "Đã xóa đề thi." });
    }

    // ---------------------------------------------------------------------
    // Sections
    // ---------------------------------------------------------------------

    [HttpPost("{examId:guid}/sections")]
    public async Task<IActionResult> AddSection(Guid examId, [FromBody] CreateSectionRequestDto request, CancellationToken ct)
    {
        try
        {
            var section = await _exams.AddSectionAsync(examId, request, ct);
            return section == null
                ? NotFound(new { message = "Không tìm thấy đề thi!" })
                : CreatedAtAction(nameof(GetById), new { examId }, section);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPut("sections/{sectionId:guid}")]
    public async Task<IActionResult> UpdateSection(Guid sectionId, [FromBody] UpdateSectionRequestDto request, CancellationToken ct)
    {
        var section = await _exams.UpdateSectionAsync(sectionId, request, ct);
        return section == null ? NotFound(new { message = "Không tìm thấy phần thi!" }) : Ok(section);
    }

    [HttpDelete("sections/{sectionId:guid}")]
    public async Task<IActionResult> DeleteSection(Guid sectionId, CancellationToken ct)
    {
        bool deleted = await _exams.DeleteSectionAsync(sectionId, ct);
        return deleted ? Ok(new { message = "Đã xóa phần thi." }) : NotFound(new { message = "Không tìm thấy phần thi!" });
    }

    // ---------------------------------------------------------------------
    // Questions
    // ---------------------------------------------------------------------

    [HttpPost("sections/{sectionId:guid}/questions")]
    public async Task<IActionResult> AddQuestion(Guid sectionId, [FromBody] CreateQuestionRequestDto request, CancellationToken ct)
    {
        try
        {
            var question = await _exams.AddQuestionAsync(sectionId, request, ct);
            return question == null
                ? NotFound(new { message = "Không tìm thấy phần thi!" })
                : Ok(question);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("questions/{questionId:guid}")]
    public async Task<IActionResult> UpdateQuestion(Guid questionId, [FromBody] UpdateQuestionRequestDto request, CancellationToken ct)
    {
        try
        {
            var question = await _exams.UpdateQuestionAsync(questionId, request, ct);
            return question == null ? NotFound(new { message = "Không tìm thấy câu hỏi!" }) : Ok(question);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("questions/{questionId:guid}")]
    public async Task<IActionResult> DeleteQuestion(Guid questionId, CancellationToken ct)
    {
        try
        {
            bool deleted = await _exams.DeleteQuestionAsync(questionId, ct);
            return deleted ? Ok(new { message = "Đã xóa câu hỏi." }) : NotFound(new { message = "Không tìm thấy câu hỏi!" });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    // ---------------------------------------------------------------------
    // Phân công giảng viên chấm đề
    // ---------------------------------------------------------------------

    [HttpGet("{examId:guid}/assignments")]
    public async Task<IActionResult> GetAssignments(Guid examId, CancellationToken ct)
        => Ok(await _dashboard.GetAssignmentsAsync(examId, ct));

    [HttpGet("teachers")]
    public async Task<IActionResult> GetTeachers(CancellationToken ct)
        => Ok(await _dashboard.GetTeachersAsync(ct));

    [HttpPost("{examId:guid}/assignments")]
    public async Task<IActionResult> AssignTeacher(Guid examId, [FromBody] AssignTeacherRequestDto request, CancellationToken ct)
    {
        var created = await _dashboard.AssignTeacherAsync(examId, request.TeacherId, request.AllowedSkills, ct);

        return created == null
            ? NotFound(new { message = "Không tìm thấy đề thi hoặc giảng viên hợp lệ!" })
            : Ok(created);
    }

    [HttpDelete("assignments/{assignmentId:guid}")]
    public async Task<IActionResult> UnassignTeacher(Guid assignmentId, CancellationToken ct)
    {
        bool removed = await _dashboard.UnassignTeacherAsync(assignmentId, ct);
        return removed ? Ok(new { message = "Đã bỏ phân công." }) : NotFound(new { message = "Không tìm thấy phân công!" });
    }
}

public class AssignTeacherRequestDto
{
    public Guid TeacherId { get; set; }

    /// <summary>"Writing", "Speaking" hoặc "Writing,Speaking". Bỏ trống = chấm tất cả.</summary>
    public string? AllowedSkills { get; set; }
}
