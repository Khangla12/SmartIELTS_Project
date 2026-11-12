using SmartIELTS.Application.GradingStrategies;
using SmartIELTS.Application.Interfaces;
using SmartIELTS.Domain.Interfaces;
using SmartIELTS.Infrastructure.Adapters;
using SmartIELTS.Infrastructure.Data;
using SmartIELTS.Infrastructure.Repositories;
using SmartIELTS.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace SmartIELTS.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructureServices(this IServiceCollection services, IConfiguration configuration)
    {
        string provider = configuration["DatabaseProvider"] ?? "Sqlite";
        string sqliteConn = configuration.GetConnectionString("SqliteConnection") ?? "Data Source=smartielts_system.db";
        string sqlServerConn = configuration.GetConnectionString("SqlServerConnection") ?? "Server=(localdb)\\mssqllocaldb;Database=SmartIELTS_DB;Trusted_Connection=True;MultipleActiveResultSets=true";

        services.AddDbContext<SmartIeltsDbContext>(options =>
        {
            if (string.Equals(provider, "SqlServer", StringComparison.OrdinalIgnoreCase))
            {
                options.UseSqlServer(sqlServerConn);
            }
            else
            {
                options.UseSqlite(sqliteConn);
            }
        });

        // Repositories & Unit of Work
        services.AddScoped(typeof(IRepository<>), typeof(SmartIeltsRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // AI Adapter
        // Mặc định: rule-based (offline). Nếu cấu hình Ai:Grading:Provider = DeepSeek/OpenAI
        // và có API key -> dùng LLM thật, tự fallback về rule-based khi lỗi.
        var aiOptions = new Ai.AiGradingOptions();
        var aiSection = configuration.GetSection("Ai:Grading");

        if (aiSection.Exists())
        {
            aiOptions.Provider = aiSection["Provider"] ?? aiOptions.Provider;
            aiOptions.BaseUrl = aiSection["BaseUrl"] ?? aiOptions.BaseUrl;
            aiOptions.Model = aiSection["Model"] ?? aiOptions.Model;
            aiOptions.ApiKey = aiSection["ApiKey"] ?? aiOptions.ApiKey;
            aiOptions.ApiKeyEnvironmentVariable = aiSection["ApiKeyEnvironmentVariable"] ?? aiOptions.ApiKeyEnvironmentVariable;
            aiOptions.FallbackToRuleBased = bool.TryParse(aiSection["FallbackToRuleBased"], out bool fb) ? fb : aiOptions.FallbackToRuleBased;

            if (int.TryParse(aiSection["TimeoutSeconds"], out int timeout)) aiOptions.TimeoutSeconds = timeout;
            if (double.TryParse(aiSection["Temperature"], out double temp)) aiOptions.Temperature = temp;
        }

        if (aiOptions.ShouldUseRemoteAi())
        {
            // Log rõ để biết đang chấm bằng AI thật hay rule-based (quan trọng khi demo).
            Console.WriteLine($"[SmartIELTS] Bộ chấm AI: {aiOptions.Provider} | Model: {aiOptions.Model} | Endpoint: {aiOptions.BaseUrl}");
            Console.WriteLine($"[SmartIELTS] API key: {MaskKey(aiOptions.ResolveApiKey())} | Fallback: {aiOptions.FallbackToRuleBased}");

            services.AddSingleton(aiOptions);
            services.AddScoped<IntelligentAiServiceAdapter>();          // fallback
            services.AddScoped<IAiServiceAdapter>(sp => new Ai.DeepSeekAiServiceAdapter(
                aiOptions,
                sp.GetRequiredService<IntelligentAiServiceAdapter>(),
                sp.GetRequiredService<ILogger<Ai.DeepSeekAiServiceAdapter>>()));
        }
        else
        {
            Console.WriteLine("[SmartIELTS] Bộ chấm AI: Rule-based (không có key hoặc Provider = RuleBased)");
            services.AddScoped<IAiServiceAdapter, IntelligentAiServiceAdapter>();
        }

        // Grading Strategies (Strategy Pattern)
        services.AddScoped<IExamGrader, ListeningToleranceGrader>();
        services.AddScoped<IExamGrader, ReadingExactGrader>();
        services.AddScoped<IExamGrader, WritingAiGrader>();
        services.AddScoped<IExamGrader, SpeakingSpeechGrader>();
        services.AddScoped<IGradingStrategyFactory, GradingStrategyFactory>();

        // Application use cases dùng chung cho WebApi và WpfApp
        services.AddScoped<IExamWorkflowService, ExamWorkflowService>();
        services.AddScoped<ITeachingDashboardService, TeachingDashboardService>();
        services.AddScoped<IExamManagementService, ExamManagementService>();
        services.AddScoped<ISpeakingSessionService, SpeakingSessionService>();

        // Observer bus cho cảnh báo giám thị (singleton: dùng chung trong một tiến trình)
        services.AddSingleton<IProctoringNotificationBus, InMemoryProctoringNotificationBus>();

        return services;
    }

    /// <summary>Che key khi log: chỉ hiện 4 ký tự đầu và 2 cuối, tránh lộ key vào log file.</summary>
    private static string MaskKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return "(rỗng)";
        if (key.Length <= 8) return "****";
        return $"{key[..4]}...{key[^2..]} (len={key.Length})";
    }
}
