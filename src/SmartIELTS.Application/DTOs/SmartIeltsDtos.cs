using System.ComponentModel.DataAnnotations;
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
    [Required(ErrorMessage = "Tên đăng nhập không được để trống")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu không được để trống")]
    public string Password { get; set; } = string.Empty;
}

public class RegisterRequestDto
{
    [Required(ErrorMessage = "Tên đăng nhập không được để trống")]
    [StringLength(100, MinimumLength = 3, ErrorMessage = "Tên đăng nhập phải từ 3 đến 100 ký tự")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "Mật khẩu không được để trống")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "Mật khẩu phải có ít nhất 6 ký tự")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "Họ tên không được để trống")]
    [StringLength(200)]
    public string FullName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [StringLength(200)]
    public string Email { get; set; } = string.Empty;
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
    [Required(ErrorMessage = "UserId không được để trống")]
    public Guid UserId { get; set; }

    [Required(ErrorMessage = "ExamId không được để trống")]
    public Guid ExamId { get; set; }
}

public class TerminateSessionRequestDto
{
    [Required(ErrorMessage = "Lý do đình chỉ không được để trống")]
    [StringLength(500, MinimumLength = 5, ErrorMessage = "Lý do phải từ 5 đến 500 ký tự")]
    public string Reason { get; set; } = string.Empty;
}

public class SubmitSkillRequestDto
{
    [Required(ErrorMessage = "SessionId không được để trống")]
    public Guid SessionId { get; set; }

    [Required(ErrorMessage = "SkillType không được để trống")]
    [EnumDataType(typeof(SkillType), ErrorMessage = "SkillType không hợp lệ")]
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
    public int TotalQuestions { get; set; }
    public double BandScore { get; set; }
    public double? CriterionScore1 { get; set; } // TR / FC
    public double? CriterionScore2 { get; set; } // CC / PR
    public double? CriterionScore3 { get; set; } // LR
    public double? CriterionScore4 { get; set; } // GRA
    public string FeedbackComments { get; set; } = string.Empty;
    public string DetailedJsonAnalysis { get; set; } = string.Empty;
}

public class SessionResultDto{
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
    [Required(ErrorMessage = "SessionId không được để trống")]
    public Guid SessionId { get; set; }

    [EnumDataType(typeof(ViolationType), ErrorMessage = "ViolationType không hợp lệ")]
    public ViolationType ViolationType { get; set; }

    [Required(ErrorMessage = "Mô tả vi phạm không được để trống")]
    public string Description { get; set; } = string.Empty;

    public string? EvidenceSnapshotUrl { get; set; }
}

public class TeacherApproveScoreRequestDto
{
    [Required(ErrorMessage = "ReportId không được để trống")]
    public Guid ReportId { get; set; }

    [Range(1, 9, ErrorMessage = "Điểm tiêu chí phải trong khoảng 1 - 9")]
    public double CriterionScore1 { get; set; }

    [Range(1, 9, ErrorMessage = "Điểm tiêu chí phải trong khoảng 1 - 9")]
    public double CriterionScore2 { get; set; }

    [Range(1, 9, ErrorMessage = "Điểm tiêu chí phải trong khoảng 1 - 9")]
    public double CriterionScore3 { get; set; }

    [Range(1, 9, ErrorMessage = "Điểm tiêu chí phải trong khoảng 1 - 9")]
    public double CriterionScore4 { get; set; }

    [Range(1, 9, ErrorMessage = "Điểm Band phải trong khoảng 1 - 9")]
    public double ApprovedBandScore { get; set; }

    public string TeacherComments { get; set; } = string.Empty;
}

public class ExamSessionItemDto
{
    public Guid SessionId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string ExamTitle { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public double? OverallBand { get; set; }
    public int ViolationCount { get; set; }
}

public class ProctoringLogItemDto
{
    public Guid Id { get; set; }
    public Guid SessionId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string ViolationType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class PendingReviewItemDto
{
    public Guid ReportId { get; set; }
    public Guid SubmissionId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public SkillType SkillType { get; set; }
    public double ProposedBand { get; set; }
    public double Criterion1 { get; set; }
    public double Criterion2 { get; set; }
    public double Criterion3 { get; set; }
    public double Criterion4 { get; set; }
    public string AiFeedback { get; set; } = string.Empty;
    public string DetailedJson { get; set; } = string.Empty;
    public string EssayContent { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
}
