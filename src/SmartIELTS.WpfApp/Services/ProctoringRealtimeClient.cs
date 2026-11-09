using SmartIELTS.Application.Interfaces;
using Microsoft.AspNetCore.SignalR.Client;

namespace SmartIELTS.WpfApp.Services;

/// <summary>
/// Kết nối SignalR tới Web API để dashboard giám thị nhận cảnh báo vi phạm realtime.
/// <para>
/// Thiết kế "best effort": nếu API không chạy (chế độ offline / demo không mạng),
/// dashboard vẫn hoạt động bình thường nhờ cảnh báo cục bộ từ
/// <see cref="IProctoringNotificationBus"/>.
/// </para>
/// </summary>
public sealed class ProctoringRealtimeClient : IAsyncDisposable
{
    private HubConnection? _connection;

    public event Action<ProctoringNotification>? ViolationReceived;

    public bool IsConnected => _connection?.State == HubConnectionState.Connected;

    public string StatusText => _connection?.State switch
    {
        HubConnectionState.Connected => "Realtime: Đã kết nối",
        HubConnectionState.Connecting => "Realtime: Đang kết nối...",
        HubConnectionState.Reconnecting => "Realtime: Đang kết nối lại...",
        _ => "Realtime: Ngoại tuyến (chỉ nhận cảnh báo cục bộ)"
    };

    /// <summary>Thử kết nối, nuốt lỗi nếu API không sẵn sàng.</summary>
    public async Task TryConnectAsync(string apiBaseUrl)
    {
        if (_connection != null) return;

        try
        {
            _connection = new HubConnectionBuilder()
                .WithUrl($"{apiBaseUrl.TrimEnd('/')}/hubs/proctoring")
                .WithAutomaticReconnect()
                .Build();

            _connection.On<ProctoringNotification>("ViolationAlertReceived", notification =>
            {
                if (notification != null) ViolationReceived?.Invoke(notification);
            });

            await _connection.StartAsync();
            await _connection.InvokeAsync("JoinAllProctoring");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SmartIELTS] Không kết nối được SignalR: {ex.Message}");
            _connection = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection == null) return;

        try
        {
            await _connection.DisposeAsync();
        }
        catch
        {
            // Bỏ qua lỗi khi đóng kết nối.
        }
        finally
        {
            _connection = null;
        }
    }
}
