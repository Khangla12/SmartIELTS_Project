using SmartIELTS.Application.DTOs;
using SmartIELTS.Domain.Enums;

namespace SmartIELTS.Application.Interfaces;

public class StartSessionResultDto
{
    public Guid SessionId { get; set; }
    public DateTime StartTime { get; set; }
    public ExamDto Exam { get; set; } = new();
}

public class SubmitSkillResultDto
{
    public SkillType SkillType { get; set; }
    public double RawScore { get; set; }
    public int TotalQuestions { get; set; }
    public double BandScore { get; set; }
    public Guid ReportId { get; set; }

    /// <summary>Overall Band của ca thi sau khi nộp (null nếu chưa đủ 4 kỹ năng).</summary>
    public double? OverallBandScore { get; set; }

    public double? CriterionScore1 { get; set; }
    public double? CriterionScore2 { get; set; }
    public double? CriterionScore3 { get; set; }
    public double? CriterionScore4 { get; set; }
    public string FeedbackComments { get; set; } = string.Empty;
    public string DetailedJsonAnalysis { get; set; } = string.Empty;
}

/// <summary>
/// Use case trung tâm của luồng thi, dùng chung cho cả Web API và WPF Kiosk
/// để tránh hai nơi cài đặt lệch nhau.
/// </summary>
public interface IExamWorkflowService
{
    Task<StartSessionResultDto?> StartSessionAsync(Guid userId, Guid examId, CancellationToken ct = default);

    Task<SubmitSkillResultDto> SubmitSkillAsync(SubmitSkillRequestDto request, CancellationToken ct = default);

    Task<SessionResultDto?> GetSessionResultAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>Lưu nháp bài làm (Offline-First autosave). Trả về true nếu tìm thấy ca thi.</summary>
    Task<bool> SaveDraftAsync(AutosaveDraftRequestDto request, CancellationToken ct = default);

    Task<AutosaveDraftRequestDto?> GetDraftAsync(Guid sessionId, SkillType skillType, CancellationToken ct = default);
}
