using Microsoft.AspNetCore.SignalR;

namespace SmartIELTS.WebApi.Hubs;

/// <summary>
/// Kênh realtime của ca thi (giữ nguyên API cũ để không phá client hiện có).
/// </summary>
public class ExamHub : Hub
{
    public async Task JoinExamRoom(string sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"ExamSession_{sessionId}");
    }

    public async Task LeaveExamRoom(string sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"ExamSession_{sessionId}");
    }

    public async Task SyncRemainingTime(string sessionId, int remainingSeconds)
    {
        await Clients.Group($"ExamSession_{sessionId}").SendAsync("TimeSynced", remainingSeconds);
    }
}

/// <summary>
/// Kênh giám sát phòng thi.
/// Hai group:
///  - <c>Proctoring_All</c>: mọi bảng giám sát đều nhận (dùng cho dashboard giáo viên).
///  - <c>Proctoring_{sessionId}</c>: chỉ nhận vi phạm của một ca thi cụ thể.
/// </summary>
public class ProctoringHub : Hub
{
    public const string AllProctoringGroup = "Proctoring_All";

    public async Task JoinProctoringRoom(string sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Proctoring_{sessionId}");
    }

    /// <summary>Dashboard giám thị đăng ký nhận cảnh báo của tất cả ca thi.</summary>
    public async Task JoinAllProctoring()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, AllProctoringGroup);
    }

    public async Task LeaveAllProctoring()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, AllProctoringGroup);
    }

    public async Task SendViolationAlert(string sessionId, string violationType, string message)
    {
        await Clients.Group($"Proctoring_{sessionId}").SendAsync("ViolationAlertReceived", new
        {
            SessionId = sessionId,
            ViolationType = violationType,
            Message = message,
            Timestamp = DateTime.UtcNow
        });
    }
}
