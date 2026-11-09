using System.Text.Json;
using SmartIELTS.Application.DTOs;
using SmartIELTS.Application.Interfaces;
using SmartIELTS.Domain.Entities;
using SmartIELTS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace SmartIELTS.Infrastructure.Services;

/// <summary>
/// Cài đặt CRUD đề thi cho Admin.
/// Đề đã có ca thi (ExamSessions) sẽ bị khóa sửa/xóa để không làm sai lệch điểm đã chấm.
/// </summary>
public class ExamManagementService : IExamManagementService
{
    private readonly SmartIeltsDbContext _context;

    public ExamManagementService(SmartIeltsDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ExamAdminDto>> GetAllExamsAsync(CancellationToken ct = default)
    {
        var exams = await _context.Exams
            .AsNoTracking()
            .Include(e => e.Sections)
            .ThenInclude(s => s.Questions)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(ct);

        var sessionCounts = await _context.ExamSessions
            .AsNoTracking()
            .GroupBy(s => s.ExamId)
            .Select(g => new { ExamId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ExamId, x => x.Count, ct);

        return exams.Select(e => MapExam(e, sessionCounts.GetValueOrDefault(e.Id))).ToList();
    }

    public async Task<ExamAdminDto?> GetExamAsync(Guid examId, CancellationToken ct = default)
    {
        var exam = await LoadExamAsync(examId, ct);
        if (exam == null) return null;

        int sessionCount = await _context.ExamSessions.CountAsync(s => s.ExamId == examId, ct);
        return MapExam(exam, sessionCount);
    }

    public async Task<ExamAdminDto> CreateExamAsync(CreateExamRequestDto request, CancellationToken ct = default)
    {
        var exam = new Exam
        {
            Title = request.Title.Trim(),
            Description = request.Description?.Trim() ?? string.Empty,
            ExamType = request.ExamType,
            TotalDurationMinutes = request.TotalDurationMinutes,
            IsActive = request.IsActive
        };

        await _context.Exams.AddAsync(exam, ct);
        await _context.SaveChangesAsync(ct);

        return MapExam(exam, 0);
    }

    public async Task<ExamAdminDto?> UpdateExamAsync(Guid examId, UpdateExamRequestDto request, CancellationToken ct = default)
    {
        var exam = await LoadExamAsync(examId, ct, tracking: true);
        if (exam == null) return null;

        exam.Title = request.Title.Trim();
        exam.Description = request.Description?.Trim() ?? string.Empty;
        exam.ExamType = request.ExamType;
        exam.TotalDurationMinutes = request.TotalDurationMinutes;
        exam.IsActive = request.IsActive;

        await _context.SaveChangesAsync(ct);

        int sessionCount = await _context.ExamSessions.CountAsync(s => s.ExamId == examId, ct);
        return MapExam(exam, sessionCount);
    }

    public async Task<bool> DeleteExamAsync(Guid examId, CancellationToken ct = default)
    {
        var exam = await _context.Exams.FirstOrDefaultAsync(e => e.Id == examId, ct);
        if (exam == null) return false;

        // FK ExamSessions -> Exams là Restrict: xóa sẽ ném lỗi. Chặn sớm để trả thông báo rõ ràng.
        bool hasSessions = await _context.ExamSessions.AnyAsync(s => s.ExamId == examId, ct);
        if (hasSessions) return false;

        _context.Exams.Remove(exam);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ExamHasSessionsAsync(Guid examId, CancellationToken ct = default)
        => await _context.ExamSessions.AnyAsync(s => s.ExamId == examId, ct);

    public async Task<ExamSectionAdminDto?> AddSectionAsync(Guid examId, CreateSectionRequestDto request, CancellationToken ct = default)
    {
        bool examExists = await _context.Exams.AnyAsync(e => e.Id == examId, ct);
        if (!examExists) return null;

        // Mỗi đề chỉ có 1 section cho mỗi kỹ năng (Strategy Pattern chọn theo SkillType).
        bool duplicate = await _context.ExamSections
            .AnyAsync(s => s.ExamId == examId && s.SkillType == request.SkillType, ct);
        if (duplicate)
            throw new InvalidOperationException($"Đề thi đã có phần thi {request.SkillType}. Mỗi kỹ năng chỉ một phần.");

        var section = new ExamSection
        {
            ExamId = examId,
            SkillType = request.SkillType,
            SectionOrder = request.SectionOrder,
            DurationMinutes = request.DurationMinutes,
            MediaUrl = NormalizeOptional(request.MediaUrl),
            PassageText = request.PassageText
        };

        await _context.ExamSections.AddAsync(section, ct);
        await _context.SaveChangesAsync(ct);

        return MapSection(section);
    }

    public async Task<ExamSectionAdminDto?> UpdateSectionAsync(Guid sectionId, UpdateSectionRequestDto request, CancellationToken ct = default)
    {
        var section = await _context.ExamSections
            .Include(s => s.Questions)
            .FirstOrDefaultAsync(s => s.Id == sectionId, ct);

        if (section == null) return null;

        section.SkillType = request.SkillType;
        section.SectionOrder = request.SectionOrder;
        section.DurationMinutes = request.DurationMinutes;
        section.MediaUrl = NormalizeOptional(request.MediaUrl);
        section.PassageText = request.PassageText;

        await _context.SaveChangesAsync(ct);
        return MapSection(section);
    }

    public async Task<bool> DeleteSectionAsync(Guid sectionId, CancellationToken ct = default)
    {
        var section = await _context.ExamSections.FirstOrDefaultAsync(s => s.Id == sectionId, ct);
        if (section == null) return false;

        _context.ExamSections.Remove(section);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<QuestionAdminDto?> AddQuestionAsync(Guid sectionId, CreateQuestionRequestDto request, CancellationToken ct = default)
    {
        bool sectionExists = await _context.ExamSections.AnyAsync(s => s.Id == sectionId, ct);
        if (!sectionExists) return null;

        ValidateOptions(request.OptionsJson);

        var question = new Question
        {
            SectionId = sectionId,
            QuestionNumber = request.QuestionNumber,
            QuestionType = request.QuestionType,
            QuestionText = request.QuestionText.Trim(),
            OptionsJson = NormalizeOptions(request.OptionsJson),
            CorrectAnswer = request.CorrectAnswer.Trim(),
            MaxScore = request.MaxScore
        };

        await _context.Questions.AddAsync(question, ct);
        await _context.SaveChangesAsync(ct);

        return MapQuestion(question);
    }

    public async Task<QuestionAdminDto?> UpdateQuestionAsync(Guid questionId, UpdateQuestionRequestDto request, CancellationToken ct = default)
    {
        var question = await _context.Questions.FirstOrDefaultAsync(q => q.Id == questionId, ct);
        if (question == null) return null;

        ValidateOptions(request.OptionsJson);

        question.QuestionNumber = request.QuestionNumber;
        question.QuestionType = request.QuestionType;
        question.QuestionText = request.QuestionText.Trim();
        question.OptionsJson = NormalizeOptions(request.OptionsJson);
        question.CorrectAnswer = request.CorrectAnswer.Trim();
        question.MaxScore = request.MaxScore;

        await _context.SaveChangesAsync(ct);
        return MapQuestion(question);
    }

    public async Task<bool> DeleteQuestionAsync(Guid questionId, CancellationToken ct = default)
    {
        var question = await _context.Questions.FirstOrDefaultAsync(q => q.Id == questionId, ct);
        if (question == null) return false;

        _context.Questions.Remove(question);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    private Task<Exam?> LoadExamAsync(Guid examId, CancellationToken ct, bool tracking = false)
    {
        IQueryable<Exam> query = _context.Exams
            .Include(e => e.Sections)
            .ThenInclude(s => s.Questions);

        if (!tracking) query = query.AsNoTracking();

        return query.FirstOrDefaultAsync(e => e.Id == examId, ct);
    }

    /// <summary>OptionsJson phải là JSON array chuỗi hợp lệ (hoặc null với câu điền từ).</summary>
    private static void ValidateOptions(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson)) return;

        try
        {
            var options = JsonSerializer.Deserialize<List<string>>(optionsJson);
            if (options == null || options.Count == 0)
                throw new InvalidOperationException("OptionsJson phải là mảng JSON có ít nhất 1 lựa chọn.");
        }
        catch (JsonException)
        {
            throw new InvalidOperationException(
                "OptionsJson không hợp lệ. Ví dụ đúng: [\"True\",\"False\",\"Not Given\"]");
        }
    }

    private static string? NormalizeOptions(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson)) return null;

        var options = JsonSerializer.Deserialize<List<string>>(optionsJson);
        return options == null ? null : JsonSerializer.Serialize(options);
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static ExamAdminDto MapExam(Exam exam, int sessionCount) => new()
    {
        Id = exam.Id,
        Title = exam.Title,
        Description = exam.Description,
        ExamType = exam.ExamType,
        TotalDurationMinutes = exam.TotalDurationMinutes,
        IsActive = exam.IsActive,
        CreatedAt = exam.CreatedAt,
        SectionCount = exam.Sections.Count,
        QuestionCount = exam.Sections.Sum(s => s.Questions.Count),
        SessionCount = sessionCount,
        Sections = exam.Sections
            .OrderBy(s => s.SectionOrder)
            .Select(MapSection)
            .ToList()
    };

    private static ExamSectionAdminDto MapSection(ExamSection section) => new()
    {
        Id = section.Id,
        ExamId = section.ExamId,
        SkillType = section.SkillType,
        SectionOrder = section.SectionOrder,
        DurationMinutes = section.DurationMinutes,
        MediaUrl = section.MediaUrl,
        PassageText = section.PassageText,
        Questions = section.Questions
            .OrderBy(q => q.QuestionNumber)
            .Select(MapQuestion)
            .ToList()
    };

    private static QuestionAdminDto MapQuestion(Question question) => new()
    {
        Id = question.Id,
        SectionId = question.SectionId,
        QuestionNumber = question.QuestionNumber,
        QuestionType = question.QuestionType,
        QuestionText = question.QuestionText,
        OptionsJson = question.OptionsJson,
        CorrectAnswer = question.CorrectAnswer,
        MaxScore = question.MaxScore
    };
}
