using System.Security.Claims;
using SmartIELTS.Application.Interfaces;

namespace SmartIELTS.WebApi.Security;

/// <summary>
/// Helper kiểm tra quyền truy cập ca thi.
/// <para>
/// Quy tắc:
/// - Teacher/Admin: truy cập mọi ca thi.
/// - Candidate: CHỈ ca thi của chính mình (so khớp ClaimTypes.NameIdentifier với ExamSession.UserId).
/// </para>
/// Dùng chung cho mọi endpoint nhận SessionId từ client để chặn IDOR.
/// </summary>
public static class SessionAccessGuard
{
    public static bool IsStaff(ClaimsPrincipal user)
        => user.IsInRole("Teacher") || user.IsInRole("Admin");

    public static Guid? CurrentUserId(ClaimsPrincipal user)
    {
        string? raw = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(raw, out var id) ? id : null;
    }

    /// <summary>
    /// Trả về true nếu user được phép thao tác trên ca thi này.
    /// </summary>
    public static async Task<bool> CanAccessAsync(
        ClaimsPrincipal user,
        IExamWorkflowService workflow,
        Guid sessionId,
        CancellationToken ct = default)
    {
        if (IsStaff(user)) return true;

        var currentId = CurrentUserId(user);
        if (currentId == null) return false;

        var owner = await workflow.GetSessionOwnerIdAsync(sessionId, ct);
        return owner.HasValue && owner.Value == currentId.Value;
    }
}
