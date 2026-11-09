using System.ComponentModel.DataAnnotations;
using SmartIELTS.Domain.Enums;

namespace SmartIELTS.Application.DTOs;

// =========================================================================
// DTO cho quản trị đề thi (Admin CRUD): Exam -> ExamSection -> Question
// =========================================================================

public class ExamAdminDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ExamType ExamType { get; set; }
    public int TotalDurationMinutes { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public int SectionCount { get; set; }
    public int QuestionCount { get; set; }
    public int SessionCount { get; set; }
    public List<ExamSectionAdminDto> Sections { get; set; } = new();
}

public class ExamSectionAdminDto
{
    public Guid Id { get; set; }
    public Guid ExamId { get; set; }
    public SkillType SkillType { get; set; }
    public int SectionOrder { get; set; }
    public int DurationMinutes { get; set; }
    public string? MediaUrl { get; set; }
    public string? PassageText { get; set; }
    public List<QuestionAdminDto> Questions { get; set; } = new();
}

public class QuestionAdminDto
{
    public Guid Id { get; set; }
    public Guid SectionId { get; set; }
    public int QuestionNumber { get; set; }
    public QuestionType QuestionType { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string? OptionsJson { get; set; }
    public string CorrectAnswer { get; set; } = string.Empty;
    public double MaxScore { get; set; } = 1.0;
}

public class CreateExamRequestDto
{
    [Required(ErrorMessage = "Tiêu đề đề thi không được để trống")]
    [StringLength(250, MinimumLength = 5, ErrorMessage = "Tiêu đề phải từ 5 đến 250 ký tự")]
    public string Title { get; set; } = string.Empty;

    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [EnumDataType(typeof(ExamType), ErrorMessage = "ExamType không hợp lệ")]
    public ExamType ExamType { get; set; } = ExamType.Academic;

    [Range(1, 600, ErrorMessage = "Tổng thời lượng phải trong khoảng 1 - 600 phút")]
    public int TotalDurationMinutes { get; set; } = 165;

    public bool IsActive { get; set; } = true;
}

public class UpdateExamRequestDto : CreateExamRequestDto
{
}

public class CreateSectionRequestDto
{
    [EnumDataType(typeof(SkillType), ErrorMessage = "SkillType không hợp lệ")]
    public SkillType SkillType { get; set; }

    [Range(1, 10, ErrorMessage = "Thứ tự phần thi phải trong khoảng 1 - 10")]
    public int SectionOrder { get; set; }

    [Range(1, 300, ErrorMessage = "Thời lượng phải trong khoảng 1 - 300 phút")]
    public int DurationMinutes { get; set; }

    [StringLength(500)]
    public string? MediaUrl { get; set; }

    public string? PassageText { get; set; }
}

public class UpdateSectionRequestDto : CreateSectionRequestDto
{
}

public class CreateQuestionRequestDto
{
    [Range(1, 100, ErrorMessage = "Số thứ tự câu hỏi phải trong khoảng 1 - 100")]
    public int QuestionNumber { get; set; }

    [EnumDataType(typeof(QuestionType), ErrorMessage = "QuestionType không hợp lệ")]
    public QuestionType QuestionType { get; set; }

    [Required(ErrorMessage = "Nội dung câu hỏi không được để trống")]
    public string QuestionText { get; set; } = string.Empty;

    /// <summary>JSON array lựa chọn, ví dụ ["True","False","Not Given"]. Bỏ trống với câu điền từ.</summary>
    public string? OptionsJson { get; set; }

    [Required(ErrorMessage = "Đáp án đúng không được để trống")]
    public string CorrectAnswer { get; set; } = string.Empty;

    [Range(0.5, 10, ErrorMessage = "Điểm tối đa phải trong khoảng 0.5 - 10")]
    public double MaxScore { get; set; } = 1.0;
}

public class UpdateQuestionRequestDto : CreateQuestionRequestDto
{
}
