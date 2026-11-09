using SmartIELTS.Application.Interfaces;

namespace SmartIELTS.Infrastructure.Services;

/// <summary>
/// Cài đặt trong bộ nhớ của <see cref="IProctoringNotificationBus"/>.
/// Đăng ký dạng singleton để dashboard và luồng ghi vi phạm dùng chung một instance.
/// </summary>
public class InMemoryProctoringNotificationBus : IProctoringNotificationBus
{
    public event Action<ProctoringNotification>? ViolationLogged;

    public void Publish(ProctoringNotification notification)
    {
        // Không để một subscriber lỗi làm hỏng luồng ghi vi phạm.
        var handlers = ViolationLogged;
        if (handlers == null) return;

        foreach (var handler in handlers.GetInvocationList().Cast<Action<ProctoringNotification>>())
        {
            try
            {
                handler(notification);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[SmartIELTS] Proctoring subscriber lỗi: {ex.Message}");
            }
        }
    }
}
