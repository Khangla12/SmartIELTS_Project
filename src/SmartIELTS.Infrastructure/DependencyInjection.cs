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
        services.AddScoped<IAiServiceAdapter, IntelligentAiServiceAdapter>();

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
}
