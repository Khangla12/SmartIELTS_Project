using SmartIELTS.Domain.Enums;

namespace SmartIELTS.Application.DTOs;

public class UserDto
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public UserRole Role { get; set; }
}

public class LoginRequestDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class LoginResponseDto
{
    public string Token { get; set; } = string.Empty;
    public UserDto User { get; set; } = null!;
}

public class ExamDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ExamType ExamType { get; set; }
    public int TotalDurationMinutes { get; set; }
    public List<ExamSectionDto> Sections { get; set; } = new();
}

public class ExamSectionDto
{
    public Guid Id { get; set; }
    public SkillType SkillType { get; set; }
    public int SectionOrder { get; set; }
    public int DurationMinutes { get; set; }
    public string? MediaUrl { get; set; }
    public string? PassageText { get; set; }
    public List<QuestionDto> Questions { get; set; } = new();
}

public class QuestionDto
{
    public Guid Id { get; set; }
    public int QuestionNumber { get; set; }
    public QuestionType QuestionType { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public string? OptionsJson { get; set; }
    public double MaxScore { get; set; }
}

public class StartSessionRequestDto
{
    public Guid UserId { get; set; }
    public Guid ExamId { get; set; }
}

public class SubmitSkillRequestDto
{
    public Guid SessionId { get; set; }
    public SkillType SkillType { get; set; }
    public string? EssayContent { get; set; }
    public string? AudioRecordUrl { get; set; }
    public Dictionary<Guid, string>? Answers { get; set; }
}

public class AutosaveDraftRequestDto
{
    public Guid SessionId { get; set; }
    public SkillType SkillType { get; set; }
    public string? DraftContent { get; set; }
    public Dictionary<Guid, string>? DraftAnswers { get; set; }
}

public class GradingResultDto
{
    public double RawScore { get; set; }
    public double BandScore { get; set; }
    public double? CriterionScore1 { get; set; } // TR / FC
    public double? CriterionScore2 { get; set; } // CC / PR
    public double? CriterionScore3 { get; set; } // LR
    public double? CriterionScore4 { get; set; } // GRA
    public string FeedbackComments { get; set; } = string.Empty;
    public string DetailedJsonAnalysis { get; set; } = string.Empty;
}

public class SessionResultDto
{
    public Guid SessionId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string ExamTitle { get; set; } = string.Empty;
    public SessionStatus Status { get; set; }
    public double? OverallBandScore { get; set; }
    public double? ListeningBand { get; set; }
    public double? ReadingBand { get; set; }
    public double? WritingBand { get; set; }
    public double? SpeakingBand { get; set; }
    public List<SkillSubmissionDto> Submissions { get; set; } = new();
}

public class SkillSubmissionDto
{
    public SkillType SkillType { get; set; }
    public double? RawScore { get; set; }
    public double? BandScore { get; set; }
    public string? EssayContent { get; set; }
    public List<EvaluationReportDto> Reports { get; set; } = new();
}

public class EvaluationReportDto
{
    public Guid Id { get; set; }
    public EvaluatorType EvaluatedBy { get; set; }
    public double? CriterionScore1 { get; set; }
    public double? CriterionScore2 { get; set; }
    public double? CriterionScore3 { get; set; }
    public double? CriterionScore4 { get; set; }
    public string FeedbackComments { get; set; } = string.Empty;
    public string DetailedJsonAnalysis { get; set; } = string.Empty;
    public bool IsApproved { get; set; }
}

public class ProctoringViolationDto
{
    public Guid SessionId { get; set; }
    public ViolationType ViolationType { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? EvidenceSnapshotUrl { get; set; }
}

public class TeacherApproveScoreRequestDto
{
    public Guid ReportId { get; set; }
    public double CriterionScore1 { get; set; }
    public double CriterionScore2 { get; set; }
    public double CriterionScore3 { get; set; }
    public double CriterionScore4 { get; set; }
    public double ApprovedBandScore { get; set; }
    public string TeacherComments { get; set; } = string.Empty;
}
