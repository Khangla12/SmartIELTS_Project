using SmartIELTS.Domain.Enums;

namespace SmartIELTS.Domain.Entities;

public class ExamSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public Guid ExamId { get; set; }
    public Exam? Exam { get; set; }

    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.InProgress;
    public double? OverallBandScore { get; set; }

    public ICollection<CandidateSubmission> Submissions { get; set; } = new List<CandidateSubmission>();
    public ICollection<ProctoringLog> ProctoringLogs { get; set; } = new List<ProctoringLog>();
}

public class CandidateSubmission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public ExamSession? Session { get; set; }

    public SkillType SkillType { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    public double? RawScore { get; set; } // Số câu đúng (0-40) hoặc điểm thô
    public double? BandScore { get; set; } // Điểm quy đổi IELTS (vd: 6.5, 7.0)

    public string? AudioRecordUrl { get; set; } // File thu âm Speaking
    public string? EssayContent { get; set; } // Bài luận Task 1 & 2 Writing
    public string? AnswersJson { get; set; } // Dictionary <QuestionId, CandidateAnswer>

    public ICollection<EvaluationReport> EvaluationReports { get; set; } = new List<EvaluationReport>();
}

public class EvaluationReport
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubmissionId { get; set; }
    public CandidateSubmission? Submission { get; set; }

    public EvaluatorType EvaluatedBy { get; set; } = EvaluatorType.AiEngine;

    // 4 tiêu chí chuẩn Cambridge (Writing: TR, CC, LR, GRA; Speaking: FC, PR, LR, GRA)
    public double? CriterionScore1 { get; set; } // Task Response / Fluency & Coherence
    public double? CriterionScore2 { get; set; } // Coherence & Cohesion / Pronunciation
    public double? CriterionScore3 { get; set; } // Lexical Resource
    public double? CriterionScore4 { get; set; } // Grammatical Range & Accuracy

    public string FeedbackComments { get; set; } = string.Empty;
    public string DetailedJsonAnalysis { get; set; } = string.Empty; // Danh sách bôi màu Đỏ/Vàng/Xanh, phát âm âm vị
    public bool IsApproved { get; set; } = false; // Giảng viên duyệt kép (Human-in-the-loop)
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ProctoringLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SessionId { get; set; }
    public ExamSession? Session { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public ViolationType ViolationType { get; set; }
    public string? EvidenceSnapshotUrl { get; set; }
    public string Description { get; set; } = string.Empty;
}
