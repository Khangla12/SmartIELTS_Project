using SmartIELTS.Application.DTOs;
using SmartIELTS.Domain.Entities;
using SmartIELTS.Domain.Interfaces;
using SmartIELTS.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SmartIELTS.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ExamsController : ControllerBase
{
    private readonly IRepository<Exam> _examRepo;
    private readonly SmartIeltsDbContext _context;

    public ExamsController(IRepository<Exam> examRepo, SmartIeltsDbContext context)
    {
        _examRepo = examRepo;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetAllExams()
    {
        var exams = await _examRepo.GetAllAsync();
        var dtos = exams.Where(e => e.IsActive).Select(e => new ExamDto
        {
            Id = e.Id,
            Title = e.Title,
            Description = e.Description,
            ExamType = e.ExamType,
            TotalDurationMinutes = e.TotalDurationMinutes
        });

        return Ok(dtos);
    }

    /// <summary>
    /// Trả về DTO thay vì entity thô để không lộ CorrectAnswer của câu hỏi cho thí sinh.
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetExamById(Guid id, CancellationToken ct)
    {
        var exam = await _context.Exams
            .AsNoTracking()
            .Include(e => e.Sections)
            .ThenInclude(s => s.Questions)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (exam == null) return NotFound(new { message = "Không tìm thấy đề thi!" });

        var dto = new ExamDto
        {
            Id = exam.Id,
            Title = exam.Title,
            Description = exam.Description,
            ExamType = exam.ExamType,
            TotalDurationMinutes = exam.TotalDurationMinutes,
            Sections = exam.Sections
                .OrderBy(s => s.SectionOrder)
                .Select(s => new ExamSectionDto
                {
                    Id = s.Id,
                    SkillType = s.SkillType,
                    SectionOrder = s.SectionOrder,
                    DurationMinutes = s.DurationMinutes,
                    MediaUrl = s.MediaUrl,
                    PassageText = s.PassageText,
                    Questions = s.Questions
                        .OrderBy(q => q.QuestionNumber)
                        .Select(q => new QuestionDto
                        {
                            Id = q.Id,
                            QuestionNumber = q.QuestionNumber,
                            QuestionType = q.QuestionType,
                            QuestionText = q.QuestionText,
                            OptionsJson = q.OptionsJson,
                            MaxScore = q.MaxScore
                        })
                        .ToList()
                })
                .ToList()
        };

        return Ok(dto);
    }
}
