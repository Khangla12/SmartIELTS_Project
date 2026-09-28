using Microsoft.AspNetCore.SignalR;

namespace SmartIELTS.WebApi.Hubs;

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

public class ProctoringHub : Hub
{
    public async Task JoinProctoringRoom(string sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"Proctoring_{sessionId}");
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
