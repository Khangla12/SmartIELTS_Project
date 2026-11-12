using SmartIELTS.Domain.Enums;

namespace SmartIELTS.Domain.Entities;

public class Exam
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ExamType ExamType { get; set; } = ExamType.Academic;
    public int TotalDurationMinutes { get; set; } = 165; // 30 (L) + 60 (R) + 60 (W) + 15 (S)
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ExamSection> Sections { get; set; } = new List<ExamSection>();
    public ICollection<ExamSession> ExamSessions { get; set; } = new List<ExamSession>();
    public ICollection<ExamAssignment> Assignments { get; set; } = new List<ExamAssignment>();
}

public class ExamSection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ExamId { get; set; }
    public Exam? Exam { get; set; }

    public SkillType SkillType { get; set; }
    public int SectionOrder { get; set; } // 1..4 (Listening, Reading, Writing, Speaking)
    public int DurationMinutes { get; set; }
    public string? MediaUrl { get; set; } // Audio link for Listening / video
    public string? PassageText { get; set; } // Passages for Reading / Prompt text for Writing & Speaking

    public ICollection<Question> Questions { get; set; } = new List<Question>();
    public ICollection<SpeakingPrompt> SpeakingPrompts { get; set; } = new List<SpeakingPrompt>();
}

public class Question
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SectionId { get; set; }
    public ExamSection? Section { get; set; }

    public int QuestionNumber { get; set; }
    public QuestionType QuestionType { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string? OptionsJson { get; set; } // Danh sách lựa chọn A, B, C, D nếu là trắc nghiệm
    public string CorrectAnswer { get; set; } = string.Empty; // Đáp án chuẩn
    public double MaxScore { get; set; } = 1.0;
}
