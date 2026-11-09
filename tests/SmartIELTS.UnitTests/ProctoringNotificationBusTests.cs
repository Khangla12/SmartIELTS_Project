using SmartIELTS.Application.Interfaces;
using SmartIELTS.Infrastructure.Services;
using SmartIELTS.Domain.Enums;
using Xunit;

namespace SmartIELTS.UnitTests;

public class ProctoringNotificationBusTests
{
    [Fact]
    public void Publish_delivers_to_all_subscribers()
    {
        var bus = new InMemoryProctoringNotificationBus();
        var received = new List<ProctoringNotification>();

        bus.ViolationLogged += received.Add;
        bus.ViolationLogged += received.Add;

        bus.Publish(new ProctoringNotification
        {
            SessionId = Guid.NewGuid(),
            CandidateName = "Thí sinh",
            ViolationType = ViolationType.HotkeyBlocked,
            Description = "Alt + Tab"
        });

        Assert.Equal(2, received.Count);
    }

    [Fact]
    public void Publish_survives_a_failing_subscriber()
    {
        var bus = new InMemoryProctoringNotificationBus();
        bool secondCalled = false;

        bus.ViolationLogged += _ => throw new InvalidOperationException("subscriber lỗi");
        bus.ViolationLogged += _ => secondCalled = true;

        // Không được ném lỗi ra ngoài: ghi log vi phạm phải luôn thành công.
        bus.Publish(new ProctoringNotification { ViolationType = ViolationType.TabSwitch });

        Assert.True(secondCalled);
    }

    [Fact]
    public void Publish_without_subscribers_does_not_throw()
    {
        var bus = new InMemoryProctoringNotificationBus();
        bus.Publish(new ProctoringNotification { ViolationType = ViolationType.NoiseDetected });
    }

    [Fact]
    public void Unsubscribed_handler_stops_receiving()
    {
        var bus = new InMemoryProctoringNotificationBus();
        int count = 0;
        Action<ProctoringNotification> handler = _ => count++;

        bus.ViolationLogged += handler;
        bus.Publish(new ProctoringNotification());
        bus.ViolationLogged -= handler;
        bus.Publish(new ProctoringNotification());

        Assert.Equal(1, count);
    }
}
