namespace SmartIELTS.Infrastructure.Ai;

/// <summary>
/// Cấu hình cho bộ chấm AI thật (DeepSeek / OpenAI-compatible).
/// <para>
/// Không đặt API key trong source code. Thứ tự ưu tiên khi đọc key:
/// 1. Biến môi trường <c>DEEPSEEK_API_KEY</c> (khuyến nghị khi demo).
/// 2. <c>Ai:Grading:ApiKey</c> trong appsettings.json (tiện lúc dev, KHÔNG commit).
/// </para>
/// Nếu không tìm thấy key, hệ thống tự fallback sang bộ chấm rule-based.
/// </summary>
public class AiGradingOptions
{
    /// <summary>"DeepSeek" | "OpenAI" | "RuleBased" | "Auto"</summary>
    public string Provider { get; set; } = "Auto";

    public string BaseUrl { get; set; } = "https://api.deepseek.com";

    public string Model { get; set; } = "deepseek-chat";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Tên biến môi trường chứa key (ưu tiên hơn ApiKey trong file cấu hình).</summary>
    public string ApiKeyEnvironmentVariable { get; set; } = "DEEPSEEK_API_KEY";

    /// <summary>
    /// Timeout mỗi lần gọi. Lưu ý: model reasoning cần 20–30 giây cho 1 bài Writing,
    /// nên để mặc định 180 giây để tránh fail oan khi mạng chậm.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 180;

    /// <summary>
    /// Fallback: nếu gọi AI thật lỗi (mất mạng, hết quota, timeout) thì dùng rule-based
    /// thay vì làm hỏng bài thi. Nên bật = true khi demo.
    /// </summary>
    public bool FallbackToRuleBased { get; set; } = true;

    /// <summary>Nhiệt độ thấp để kết quả chấm ổn định, ít dao động giữa các lần.</summary>
    public double Temperature { get; set; } = 0.2;

    /// <summary>Lấy key thực tế theo thứ tự: biến môi trường -> cấu hình.</summary>
    public string ResolveApiKey()
    {
        if (!string.IsNullOrWhiteSpace(ApiKeyEnvironmentVariable))
        {
            string? fromEnv = Environment.GetEnvironmentVariable(ApiKeyEnvironmentVariable);
            if (!string.IsNullOrWhiteSpace(fromEnv)) return fromEnv.Trim();
        }

        return ApiKey?.Trim() ?? string.Empty;
    }

    public bool HasApiKey() => !string.IsNullOrWhiteSpace(ResolveApiKey());

    /// <summary>
    /// Quyết định có dùng AI thật hay không.
    /// "Auto" = dùng AI nếu có key, ngược lại rule-based.
    /// </summary>
    public bool ShouldUseRemoteAi()
    {
        if (string.IsNullOrWhiteSpace(Provider)) return false;
        if (Provider.Equals("RuleBased", StringComparison.OrdinalIgnoreCase)) return false;
        if (Provider.Equals("Auto", StringComparison.OrdinalIgnoreCase)) return HasApiKey();
        return HasApiKey();
    }
}
