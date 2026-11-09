using SmartIELTS.Domain.Enums;

namespace SmartIELTS.Application.Interfaces;

/// <summary>Thông tin một vi phạm vừa được ghi nhận, dùng để phát cho các bảng giám sát.</summary>
public class ProctoringNotification
{
    public Guid SessionId { get; set; }
    public string CandidateName { get; set; } = string.Empty;
    public ViolationType ViolationType { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Bus thông báo vi phạm (Observer Pattern).
/// <para>
/// Trước đây WPF ghi vi phạm trực tiếp xuống database nên không đi qua controller,
/// khiến đoạn broadcast SignalR không bao giờ chạy. Mọi đường ghi vi phạm giờ đều
/// publish qua bus này, nhờ đó:
/// </para>
/// <list type="bullet">
///   <item>Dashboard trong cùng tiến trình (WPF) nhận cảnh báo ngay cả khi offline.</item>
///   <item>Web API chuyển tiếp cảnh báo qua SignalR cho client ở máy khác.</item>
/// </list>
/// </summary>
public interface IProctoringNotificationBus
{
    event Action<ProctoringNotification>? ViolationLogged;

    void Publish(ProctoringNotification notification);
}
