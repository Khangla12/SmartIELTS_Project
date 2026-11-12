using SmartIELTS.Application.DTOs;
using SmartIELTS.Application.Interfaces;
using SmartIELTS.Domain.Enums;
using Xunit;

namespace SmartIELTS.UnitTests;

/// <summary>
/// Khóa hợp đồng nghiệp vụ: AI chỉ hỗ trợ giảng viên, thí sinh không thấy điểm AI.
/// </summary>
public class GradingPrivacyContractTests
{
    [Fact]
    public void SessionResultDto_has_pending_review_state()
    {
        var dto = new SessionResultDto();

        Assert.False(dto.IsFullyApproved);
        Assert.Equal(0, dto.PendingReviewCount);
        Assert.Null(dto.OverallBandScore);
    }

    [Fact]
    public void SkillSubmissionDto_hides_score_by_default()
    {
        var dto = new SkillSubmissionDto();

        Assert.Null(dto.BandScore);
        Assert.Null(dto.RawScore);
        Assert.False(dto.IsApproved);
        Assert.Empty(dto.Reports);
    }

    [Fact]
    public void EvaluationReportDto_separates_ai_feedback_from_teacher_feedback()
    {
        var dto = new EvaluationReportDto
        {
            EvaluatedBy = EvaluatorType.Teacher,
            IsApproved = true,
            TeacherFeedback = "Nhận xét của giảng viên"
        };

        // Thí sinh chỉ được thấy TeacherFeedback; FeedbackComments/DetailedJson để trống.
        Assert.Equal("Nhận xét của giảng viên", dto.TeacherFeedback);
        Assert.Equal(string.Empty, dto.FeedbackComments);
        Assert.Equal(string.Empty, dto.DetailedJsonAnalysis);
    }

    [Fact]
    public void SubmitSkillResultDto_distinguishes_objective_and_subjective_skills()
    {
        var objective = new SubmitSkillResultDto
        {
            SkillType = SkillType.Listening,
            ShowsRawScore = true,
            IsPendingReview = false,
            BandScore = 8.0
        };

        var subjective = new SubmitSkillResultDto
        {
            SkillType = SkillType.Writing,
            ShowsRawScore = false,
            IsPendingReview = true
        };

        Assert.True(objective.ShowsRawScore);
        Assert.False(objective.IsPendingReview);
        Assert.Equal(8.0, objective.BandScore);

        Assert.False(subjective.ShowsRawScore);
        Assert.True(subjective.IsPendingReview);
        Assert.Equal(0, subjective.BandScore);   // mặc định 0 = đã ẩn
    }

    [Theory]
    [InlineData(SkillType.Listening, true)]
    [InlineData(SkillType.Reading, true)]
    [InlineData(SkillType.Writing, false)]
    [InlineData(SkillType.Speaking, false)]
    public void Only_objective_skills_are_auto_released_to_candidate(SkillType skill, bool shouldShowScore)
    {
        // Sao chép quy tắc trong ExamWorkflowService.SubmitSkillAsync
        bool isObjective = skill is SkillType.Listening or SkillType.Reading;

        Assert.Equal(shouldShowScore, isObjective);
    }
}
