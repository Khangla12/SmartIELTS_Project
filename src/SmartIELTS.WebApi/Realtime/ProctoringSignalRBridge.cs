using SmartIELTS.Application.Interfaces;
using SmartIELTS.WebApi.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace SmartIELTS.WebApi.Realtime;

/// <summary>
/// Cầu nối Observer bus -> SignalR.
/// Mọi vi phạm được ghi qua <see cref="ITeachingDashboardService"/> sẽ được đẩy tới
/// dashboard giám thị (group <c>Proctoring_All</c>) và phòng của ca thi tương ứng.
/// </summary>
public sealed class ProctoringSignalRBridge : IDisposable
{
    private readonly IProctoringNotificationBus _bus;
    private readonly IHubContext<ProctoringHub> _hubContext;
    private readonly ILogger<ProctoringSignalRBridge> _logger;
    private readonly Action<ProctoringNotification> _handler;

    public ProctoringSignalRBridge(
        IProctoringNotificationBus bus,
        IHubContext<ProctoringHub> hubContext,
        ILogger<ProctoringSignalRBridge> logger)
    {
        _bus = bus;
        _hubContext = hubContext;
        _logger = logger;

        _handler = notification => _ = BroadcastAsync(notification);
        _bus.ViolationLogged += _handler;
    }

    private async Task BroadcastAsync(ProctoringNotification notification)
    {
        var payload = new
        {
            SessionId = notification.SessionId,
            CandidateName = notification.CandidateName,
            ViolationType = notification.ViolationType.ToString(),
            Description = notification.Description,
            Timestamp = notification.Timestamp
        };

        try
        {
            await _hubContext.Clients.Group(ProctoringHub.AllProctoringGroup)
                .SendAsync("ViolationAlertReceived", payload);

            await _hubContext.Clients.Group($"Proctoring_{notification.SessionId}")
                .SendAsync("ViolationAlertReceived", payload);
        }
        catch (Exception ex)
        {
            // Không được để lỗi realtime làm hỏng nghiệp vụ ghi log vi phạm.
            _logger.LogWarning(ex, "[SmartIELTS] Không gửi được cảnh báo SignalR.");
        }
    }

    public void Dispose() => _bus.ViolationLogged -= _handler;
}
