using SmartIELTS.Application.DTOs;
using SmartIELTS.Application.Interfaces;
using SmartIELTS.Domain.Common;
using SmartIELTS.Domain.Entities;
using SmartIELTS.Domain.Enums;
using SmartIELTS.Infrastructure;
using SmartIELTS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SmartIELTS.WpfApp.Services;

/// <summary>
/// Cầu nối giữa UI Kiosk và tầng Application.
/// Toàn bộ nghiệp vụ (chấm điểm, tính band, duyệt điểm, autosave) được ủy thác cho
/// IExamWorkflowService / ITeachingDashboardService để dùng chung với Web API.
/// </summary>
public class LocalExamManager : IDisposable
{
    private ServiceProvider _serviceProvider;
    private readonly Task _initDbTask;

    public User? CurrentUser { get; private set; }
    public ExamDto? CurrentExam { get; private set; }
    public ExamSession? CurrentSession { get; private set; }

    /// <summary>Bus cảnh báo giám thị trong cùng tiến trình (luôn hoạt động, kể cả offline).</summary>
    public IProctoringNotificationBus NotificationBus { get; }

    public string ApiBaseUrl { get; } = "http://localhost:5294";

    public string RecordingFolder { get; } = "recordings";

    public LocalExamManager()
    {
        var config = BuildConfiguration();

        ApiBaseUrl = config["ApiBaseUrl"] ?? ApiBaseUrl;
        RecordingFolder = config["Recording:Folder"] ?? RecordingFolder;

        _serviceProvider = BuildServiceProvider(config);
        NotificationBus = _serviceProvider.GetRequiredService<IProctoringNotificationBus>();
        _initDbTask = Task.Run(() => InitializeDatabaseAsync(config));
    }

    private static IConfiguration BuildConfiguration()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .Build();

        if (!string.IsNullOrEmpty(config["DatabaseProvider"])) return config;

        // Không có appsettings.json -> mặc định SQL Server SmartIELTS_DB (có fallback SQLite khi lỗi).
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "DatabaseProvider", "SqlServer" },
                { "ConnectionStrings:SqlServerConnection", "Server=localhost;Database=SmartIELTS_DB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true" },
                { "ConnectionStrings:SqliteConnection", "Data Source=smartielts_system.db" }
            })
            .Build();
    }

    private static ServiceProvider BuildServiceProvider(IConfiguration config)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructureServices(config);
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Khởi tạo DB trong background. Nếu SQL Server không kết nối được thì tạo ServiceProvider mới
    /// trỏ sang SQLite (trước đây chỉ đổi config nhưng vẫn dùng provider cũ nên fallback không có tác dụng).
    /// </summary>
    private async Task InitializeDatabaseAsync(IConfiguration config)
    {
        try
        {
            await EnsureDatabaseAsync(_serviceProvider, config);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SmartIELTS LocalExamManager] DB Init: {ex.Message}");

            if (!string.Equals(config["DatabaseProvider"], "SqlServer", StringComparison.OrdinalIgnoreCase)) return;

            var fallbackConfig = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "DatabaseProvider", "Sqlite" },
                    { "ConnectionStrings:SqliteConnection", "Data Source=smartielts_system.db" }
                })
                .Build();

            try
            {
                var fallbackProvider = BuildServiceProvider(fallbackConfig);
                await EnsureDatabaseAsync(fallbackProvider, fallbackConfig);

                _serviceProvider = fallbackProvider;
                Console.WriteLine("[SmartIELTS LocalExamManager] Đã fallback sang SQLite.");
            }
            catch (Exception fallbackEx)
            {
                Console.WriteLine($"[SmartIELTS LocalExamManager] SQLite Fallback Error: {fallbackEx.Message}");
            }
        }
    }

    private static async Task EnsureDatabaseAsync(IServiceProvider provider, IConfiguration config)    {
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SmartIeltsDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("SmartIELTS.Wpf");

        await context.Database.EnsureCreatedAsync();
        await SmartIeltsSchemaGuard.EnsureAsync(context, logger);
        await SmartIeltsDbSeeder.SeedAsync(context);
    }

    public async Task<User?> LoginAsync(string username, string password)
    {
        await _initDbTask;
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SmartIeltsDbContext>();

        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Username.ToLower() == username.Trim().ToLower());

        if (user == null || !PasswordHasher.Verify(password, user.PasswordHash)) return null;

        // Nâng cấp mật khẩu plaintext cũ sang PBKDF2.
        if (!PasswordHasher.IsHashed(user.PasswordHash))
        {
            user.PasswordHash = PasswordHasher.Hash(password);
            await context.SaveChangesAsync();
        }

        CurrentUser = user;
        return user;
    }

    public async Task<ExamDto?> GetActiveExamAsync()
    {
        await _initDbTask;
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SmartIeltsDbContext>();

        var exam = await context.Exams
            .AsNoTracking()
            .Include(e => e.Sections)
            .ThenInclude(s => s.Questions)
            .Where(e => e.IsActive)
            .OrderBy(e => e.CreatedAt)
            .FirstOrDefaultAsync();

        if (exam == null) return null;

        ExamDto dto = new()
        {
            Id = exam.Id,
            Title = exam.Title,
            Description = exam.Description,
            ExamType = exam.ExamType,
            TotalDurationMinutes = exam.TotalDurationMinutes,
            Sections = exam.Sections
                .OrderBy(s => s.SectionOrder)
                .Select(s => new ExamSectionDto
                {
                    Id = s.Id,
                    SkillType = s.SkillType,
                    SectionOrder = s.SectionOrder,
                    DurationMinutes = s.DurationMinutes,
                    MediaUrl = s.MediaUrl,
                    PassageText = s.PassageText,
                    Questions = s.Questions
                        .OrderBy(q => q.QuestionNumber)
                        .Select(q => new QuestionDto
                        {
                            Id = q.Id,
                            QuestionNumber = q.QuestionNumber,
                            QuestionType = q.QuestionType,
                            QuestionText = q.QuestionText,
                            OptionsJson = q.OptionsJson,
                            MaxScore = q.MaxScore
                        })
                        .ToList()
                })
                .ToList()
        };

        CurrentExam = dto;
        return dto;
    }

    public async Task<ExamSession> StartSessionAsync()
    {
        if (CurrentUser == null || CurrentExam == null)
            throw new InvalidOperationException("Chưa đăng nhập hoặc chưa nạp đề thi!");

        await _initDbTask;
        using var scope = _serviceProvider.CreateScope();
        var workflow = scope.ServiceProvider.GetRequiredService<IExamWorkflowService>();

        var started = await workflow.StartSessionAsync(CurrentUser.Id, CurrentExam.Id)
            ?? throw new InvalidOperationException("Không thể khởi tạo ca thi!");

        CurrentSession = new ExamSession
        {
            Id = started.SessionId,
            UserId = CurrentUser.Id,
            ExamId = CurrentExam.Id,
            StartTime = started.StartTime,
            Status = SessionStatus.InProgress
        };

        return CurrentSession;
    }

    public async Task<SubmitSkillResultDto> SubmitSkillAsync(SkillType skill, Dictionary<Guid, string>? answers, string? essayContent, string? audioUrl)
    {
        if (CurrentSession == null)
            throw new InvalidOperationException("Không có phiên thi đang hoạt động!");

        await _initDbTask;
        using var scope = _serviceProvider.CreateScope();
        var workflow = scope.ServiceProvider.GetRequiredService<IExamWorkflowService>();

        var result = await workflow.SubmitSkillAsync(new SubmitSkillRequestDto
        {
            SessionId = CurrentSession.Id,
            SkillType = skill,
            Answers = answers,
            EssayContent = essayContent,
            AudioRecordUrl = audioUrl
        });

        return result;
    }

    /// <summary>Autosave thật: ghi bản nháp xuống database (Offline-First).</summary>
    public async Task<bool> SaveDraftAsync(SkillType skill, string? draftContent, Dictionary<Guid, string>? draftAnswers)
    {
        if (CurrentSession == null) return false;

        await _initDbTask;
        using var scope = _serviceProvider.CreateScope();
        var workflow = scope.ServiceProvider.GetRequiredService<IExamWorkflowService>();

        return await workflow.SaveDraftAsync(new AutosaveDraftRequestDto
        {
            SessionId = CurrentSession.Id,
            SkillType = skill,
            DraftContent = draftContent,
            DraftAnswers = draftAnswers
        });
    }

    public async Task<AutosaveDraftRequestDto?> GetDraftAsync(SkillType skill)
    {
        if (CurrentSession == null) return null;

        await _initDbTask;
        using var scope = _serviceProvider.CreateScope();
        var workflow = scope.ServiceProvider.GetRequiredService<IExamWorkflowService>();

        return await workflow.GetDraftAsync(CurrentSession.Id, skill);
    }

    public async Task LogProctoringViolationAsync(ViolationType type, string description)
    {
        if (CurrentSession == null) return;

        try
        {
            await _initDbTask;
            using var scope = _serviceProvider.CreateScope();
            var dashboard = scope.ServiceProvider.GetRequiredService<ITeachingDashboardService>();

            await dashboard.LogViolationAsync(new ProctoringViolationDto
            {
                SessionId = CurrentSession.Id,
                ViolationType = type,
                Description = description
            });
        }
        catch (Exception ex)
        {
            // Không để lỗi ghi log giám thị làm sập UI thi.
            Console.WriteLine($"[SmartIELTS] Không ghi được log vi phạm: {ex.Message}");
        }
    }

    public async Task<SessionResultDto> GetSessionSummaryAsync()
    {
        if (CurrentSession == null) throw new InvalidOperationException("Phiên thi không tồn tại!");

        await _initDbTask;
        using var scope = _serviceProvider.CreateScope();
        var workflow = scope.ServiceProvider.GetRequiredService<IExamWorkflowService>();

        var result = await workflow.GetSessionResultAsync(CurrentSession.Id)
            ?? throw new InvalidOperationException("Không tìm thấy kết quả ca thi!");

        CurrentSession.OverallBandScore = result.OverallBandScore;
        CurrentSession.Status = result.Status;

        return result;
    }

    public async Task<List<ExamSessionItemDto>> GetActiveSessionsAsync()
    {
        await _initDbTask;
        using var scope = _serviceProvider.CreateScope();
        var dashboard = scope.ServiceProvider.GetRequiredService<ITeachingDashboardService>();
        return (await dashboard.GetActiveSessionsAsync()).ToList();
    }

    public async Task<List<ProctoringLogItemDto>> GetProctoringLogsAsync()
    {
        await _initDbTask;
        using var scope = _serviceProvider.CreateScope();
        var dashboard = scope.ServiceProvider.GetRequiredService<ITeachingDashboardService>();
        return (await dashboard.GetProctoringLogsAsync()).ToList();
    }

    public async Task<List<PendingReviewItemDto>> GetPendingReviewsAsync()
    {
        await _initDbTask;
        using var scope = _serviceProvider.CreateScope();
        var dashboard = scope.ServiceProvider.GetRequiredService<ITeachingDashboardService>();
        return (await dashboard.GetPendingReviewsAsync()).ToList();
    }

    public async Task<SubmitSkillResultDto> FinalizeSpeakingAsync(List<SpeakingTurnDto> turns, string? audioRecordUrl)
    {
        if (CurrentSession == null)
            throw new InvalidOperationException("Không có phiên thi đang hoạt động!");

        await _initDbTask;
        using var scope = _serviceProvider.CreateScope();
        var speaking = scope.ServiceProvider.GetRequiredService<ISpeakingSessionService>();

        return await speaking.FinalizeAsync(CurrentSession.Id, turns, audioRecordUrl);
    }

    public async Task<SpeakingTurnResultDto> SubmitSpeakingTurnAsync(SpeakingTurnRequestDto request)
    {
        await _initDbTask;
        using var scope = _serviceProvider.CreateScope();
        var speaking = scope.ServiceProvider.GetRequiredService<ISpeakingSessionService>();

        var result = await speaking.EvaluateTurnAsync(request);
        LastTurnBand = result.TurnBand;
        return result;
    }

    public async Task<List<SpeakingPromptDto>> GetSpeakingPromptsAsync()
    {
        if (CurrentSession == null) return new List<SpeakingPromptDto>();

        await _initDbTask;
        using var scope = _serviceProvider.CreateScope();
        var speaking = scope.ServiceProvider.GetRequiredService<ISpeakingSessionService>();

        return (await speaking.GetPromptsAsync(CurrentSession.Id)).ToList();
    }

    /// <summary>Band của lượt vấn đáp gần nhất, dùng để hiển thị thống kê trên UI.</summary>
    public double LastTurnBand { get; private set; }

    public async Task ApproveScoreAsync(Guid reportId, double c1, double c2, double c3, double c4, double approvedBand, string teacherComments)
    {
        await _initDbTask;
        using var scope = _serviceProvider.CreateScope();
        var dashboard = scope.ServiceProvider.GetRequiredService<ITeachingDashboardService>();

        await dashboard.ApproveScoreAsync(new TeacherApproveScoreRequestDto
        {
            ReportId = reportId,
            CriterionScore1 = c1,
            CriterionScore2 = c2,
            CriterionScore3 = c3,
            CriterionScore4 = c4,
            ApprovedBandScore = approvedBand,
            TeacherComments = teacherComments
        });
    }

    /// <summary>Đình chỉ ca thi đang diễn ra (dùng trên dashboard giám thị).</summary>
    public async Task<SessionTerminationResultDto?> TerminateSessionAsync(Guid sessionId, string reason)
    {
        await _initDbTask;
        using var scope = _serviceProvider.CreateScope();
        var dashboard = scope.ServiceProvider.GetRequiredService<ITeachingDashboardService>();

        return await dashboard.TerminateSessionAsync(sessionId, reason, CurrentUser?.Username);
    }

    // ---------------------------------------------------------------------
    // Admin: quản trị đề thi
    // ---------------------------------------------------------------------

    public async Task<List<ExamAdminDto>> GetExamsForAdminAsync()
    {
        await _initDbTask;
        using var scope = _serviceProvider.CreateScope();
        var exams = scope.ServiceProvider.GetRequiredService<IExamManagementService>();
        return (await exams.GetAllExamsAsync()).ToList();
    }

    public async Task<ExamAdminDto?> CreateExamAsync(CreateExamRequestDto request)
    {
        await _initDbTask;
        using var scope = _serviceProvider.CreateScope();
        var exams = scope.ServiceProvider.GetRequiredService<IExamManagementService>();
        return await exams.CreateExamAsync(request);
    }

    public async Task<bool> DeleteExamAsync(Guid examId)
    {
        await _initDbTask;
        using var scope = _serviceProvider.CreateScope();
        var exams = scope.ServiceProvider.GetRequiredService<IExamManagementService>();
        return await exams.DeleteExamAsync(examId);
    }

    public async Task<bool> ExamHasSessionsAsync(Guid examId)
    {
        await _initDbTask;
        using var scope = _serviceProvider.CreateScope();
        var exams = scope.ServiceProvider.GetRequiredService<IExamManagementService>();
        return await exams.ExamHasSessionsAsync(examId);
    }

    public async Task<ExamSectionAdminDto?> AddSectionAsync(Guid examId, CreateSectionRequestDto request)
    {
        await _initDbTask;
        using var scope = _serviceProvider.CreateScope();
        var exams = scope.ServiceProvider.GetRequiredService<IExamManagementService>();
        return await exams.AddSectionAsync(examId, request);
    }

    public async Task<bool> DeleteSectionAsync(Guid sectionId)
    {
        await _initDbTask;
        using var scope = _serviceProvider.CreateScope();
        var exams = scope.ServiceProvider.GetRequiredService<IExamManagementService>();
        return await exams.DeleteSectionAsync(sectionId);
    }

    public async Task<QuestionAdminDto?> AddQuestionAsync(Guid sectionId, CreateQuestionRequestDto request)
    {
        await _initDbTask;
        using var scope = _serviceProvider.CreateScope();
        var exams = scope.ServiceProvider.GetRequiredService<IExamManagementService>();
        return await exams.AddQuestionAsync(sectionId, request);
    }

    public async Task<bool> DeleteQuestionAsync(Guid questionId)
    {
        await _initDbTask;
        using var scope = _serviceProvider.CreateScope();
        var exams = scope.ServiceProvider.GetRequiredService<IExamManagementService>();
        return await exams.DeleteQuestionAsync(questionId);
    }
    public async Task AddManualViolationLogAsync(string candidateUsername, string description)
    {
        await _initDbTask;
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SmartIeltsDbContext>();
        var dashboard = scope.ServiceProvider.GetRequiredService<ITeachingDashboardService>();

        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Username.ToLower() == candidateUsername.Trim().ToLower());

        if (user == null) return;

        var session = await context.ExamSessions
            .Where(s => s.UserId == user.Id)
            .OrderByDescending(s => s.StartTime)
            .FirstOrDefaultAsync();

        if (session == null) return;

        await dashboard.LogViolationAsync(new ProctoringViolationDto
        {
            SessionId = session.Id,
            ViolationType = ViolationType.TabSwitch,
            Description = $"[Giám thị lập biên bản]: {description}"
        });
    }

    private async Task<ITeachingDashboardService> GetDashboardAsync()
    {
        await _initDbTask;
        using var scope = _serviceProvider.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ITeachingDashboardService>();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        GC.SuppressFinalize(this);
    }
}
