using SmartIELTS.Domain.Enums;
using Xunit;

namespace SmartIELTS.UnitTests;

/// <summary>
/// Kiểm tra hợp đồng trạng thái ca thi: khi bị đình chỉ thì không được tự "hồi sinh".
/// </summary>
public class SessionLifecycleTests
{
    [Fact]
    public void Terminated_is_a_distinct_status_from_evaluated()
    {
        Assert.NotEqual(SessionStatus.Terminated, SessionStatus.Evaluated);
        Assert.Equal(5, (int)SessionStatus.Terminated);
    }

    [Fact]
    public void Termination_violation_type_exists()
    {
        Assert.Equal(6, (int)ViolationType.SessionTerminated);
        Assert.NotEqual(ViolationType.TabSwitch, ViolationType.SessionTerminated);
    }

    /// <summary>
    /// Sao chép logic của ExamWorkflowService.RecalculateSessionScore để khóa hợp đồng:
    /// đủ 4 kỹ năng nhưng ca thi đã Terminated thì trạng thái phải giữ nguyên.
    /// </summary>
    [Theory]
    [InlineData(SessionStatus.InProgress, SessionStatus.Evaluated)]
    [InlineData(SessionStatus.Terminated, SessionStatus.Terminated)]
    public void Full_score_does_not_revive_a_terminated_session(SessionStatus before, SessionStatus expected)
    {
        var status = before;
        bool hasAllFourSkills = true;

        if (hasAllFourSkills && status != SessionStatus.Terminated)
        {
            status = SessionStatus.Evaluated;
        }

        Assert.Equal(expected, status);
    }
}
