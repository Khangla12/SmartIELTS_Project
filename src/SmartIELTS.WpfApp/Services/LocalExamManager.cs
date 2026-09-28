using System.Text.Json;
using SmartIELTS.Application.DTOs;
using SmartIELTS.Application.GradingStrategies;
using SmartIELTS.Domain.Common;
using SmartIELTS.Domain.Entities;
using SmartIELTS.Domain.Enums;
using SmartIELTS.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace SmartIELTS.WpfApp.Services;

public class LocalExamManager
{
    private readonly IServiceProvider _serviceProvider;
    public User? CurrentUser { get; private set; }
    public Exam? CurrentExam { get; private set; }
    public ExamSession? CurrentSession { get; private set; }

    public LocalExamManager()
    {
        var services = new ServiceCollection();

        // Load appsettings.json or fallback
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

        var config = builder.Build();

        // If no provider configured, default to SqlServer SmartIELTS_DB
        if (string.IsNullOrEmpty(config["DatabaseProvider"]))
        {
            config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    { "DatabaseProvider", "SqlServer" },
                    { "ConnectionStrings:SqlServerConnection", "Server=localhost;Database=SmartIELTS_DB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true" },
                    { "ConnectionStrings:SqliteConnection", "Data Source=smartielts_system.db" }
                })
                .Build();
        }

        SmartIELTS.Infrastructure.DependencyInjection.AddInfrastructureServices(services, config);
        _serviceProvider = services.BuildServiceProvider();

        // Ensure DB & Seeder
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<SmartIeltsDbContext>();
            context.Database.EnsureCreated();
            SmartIeltsDbSeeder.SeedAsync(context).Wait();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SmartIELTS LocalExamManager] DB Init: {ex.Message}");
        }
    }

    public async Task<User?> LoginAsync(string username, string password)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SmartIeltsDbContext>();

        var user = await context.Users.FirstOrDefaultAsync(u => u.Username.ToLower() == username.Trim().ToLower());
        if (user != null && user.PasswordHash == password)
        {
            CurrentUser = user;
            return user;
        }
        return null;
    }

    public async Task<Exam?> GetActiveExamAsync()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SmartIeltsDbContext>();

        CurrentExam = await context.Exams
            .Include(e => e.Sections)
            .ThenInclude(s => s.Questions)
            .FirstOrDefaultAsync(e => e.IsActive);

        return CurrentExam;
    }

    public async Task<ExamSession> StartSessionAsync()
    {
        if (CurrentUser == null || CurrentExam == null)
            throw new InvalidOperationException("Chưa đăng nhập hoặc chưa nạp đề thi!");

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SmartIeltsDbContext>();

        var session = new ExamSession
        {
            UserId = CurrentUser.Id,
            ExamId = CurrentExam.Id,
            StartTime = DateTime.UtcNow,
            Status = SessionStatus.InProgress
        };

        await context.ExamSessions.AddAsync(session);
        await context.SaveChangesAsync();

        CurrentSession = session;
        return session;
    }

    public async Task<GradingResultDto> SubmitSkillAsync(SkillType skill, Dictionary<Guid, string>? answers, string? essayContent, string? audioUrl)
    {
        if (CurrentSession == null || CurrentExam == null)
            throw new InvalidOperationException("Không có phiên thi đang hoạt động!");

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SmartIeltsDbContext>();
        var gradingFactory = scope.ServiceProvider.GetRequiredService<IGradingStrategyFactory>();

        var section = CurrentExam.Sections.First(s => s.SkillType == skill);

        string answersJson = answers != null 
            ? JsonSerializer.Serialize(answers.ToDictionary(k => k.Key.ToString(), v => v.Value))
            : string.Empty;

        var submission = new CandidateSubmission
        {
            SessionId = CurrentSession.Id,
            SkillType = skill,
            SubmittedAt = DateTime.UtcNow,
            EssayContent = essayContent,
            AudioRecordUrl = audioUrl,
            AnswersJson = answersJson
        };

        var grader = gradingFactory.GetGrader(skill);
        var result = await grader.GradeAsync(submission, section);

        submission.RawScore = result.RawScore;
        submission.BandScore = result.BandScore;

        var report = new EvaluationReport
        {
            Submission = submission,
            EvaluatedBy = EvaluatorType.AiEngine,
            CriterionScore1 = result.CriterionScore1,
            CriterionScore2 = result.CriterionScore2,
            CriterionScore3 = result.CriterionScore3,
            CriterionScore4 = result.CriterionScore4,
            FeedbackComments = result.FeedbackComments,
            DetailedJsonAnalysis = result.DetailedJsonAnalysis,
            IsApproved = (skill == SkillType.Listening || skill == SkillType.Reading)
        };

        submission.EvaluationReports.Add(report);

        await context.CandidateSubmissions.AddAsync(submission);
        await context.SaveChangesAsync();

        return result;
    }

    public async Task LogProctoringViolationAsync(ViolationType type, string description)
    {
        if (CurrentSession == null) return;

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SmartIeltsDbContext>();

        var log = new ProctoringLog
        {
            SessionId = CurrentSession.Id,
            Timestamp = DateTime.UtcNow,
            ViolationType = type,
            Description = description
        };

        await context.ProctoringLogs.AddAsync(log);
        await context.SaveChangesAsync();
    }

    public async Task<SessionResultDto> GetSessionSummaryAsync()
    {
        if (CurrentSession == null) throw new InvalidOperationException("Phiên thi không tồn tại!");

        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SmartIeltsDbContext>();

        var session = await context.ExamSessions
            .Include(s => s.User)
            .Include(s => s.Exam)
            .Include(s => s.Submissions)
            .ThenInclude(sub => sub.EvaluationReports)
            .FirstAsync(s => s.Id == CurrentSession.Id);

        var lSub = session.Submissions.FirstOrDefault(s => s.SkillType == SkillType.Listening);
        var rSub = session.Submissions.FirstOrDefault(s => s.SkillType == SkillType.Reading);
        var wSub = session.Submissions.FirstOrDefault(s => s.SkillType == SkillType.Writing);
        var sSub = session.Submissions.FirstOrDefault(s => s.SkillType == SkillType.Speaking);

        double l = lSub?.BandScore ?? 0;
        double r = rSub?.BandScore ?? 0;
        double w = wSub?.BandScore ?? 0;
        double s = sSub?.BandScore ?? 0;

        session.OverallBandScore = BandScoreCalculator.CalculateOverallBand(l, r, w, s);
        session.Status = SessionStatus.Evaluated;
        session.EndTime = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return new SessionResultDto
        {
            SessionId = session.Id,
            CandidateName = session.User?.FullName ?? "Thí sinh",
            ExamTitle = session.Exam?.Title ?? "Kỳ thi IELTS",
            Status = session.Status,
            OverallBandScore = session.OverallBandScore,
            ListeningBand = lSub?.BandScore,
            ReadingBand = rSub?.BandScore,
            WritingBand = wSub?.BandScore,
            SpeakingBand = sSub?.BandScore,
            Submissions = session.Submissions.Select(sub => new SkillSubmissionDto
            {
                SkillType = sub.SkillType,
                RawScore = sub.RawScore,
                BandScore = sub.BandScore,
                EssayContent = sub.EssayContent,
                Reports = sub.EvaluationReports.Select(rep => new EvaluationReportDto
                {
                    Id = rep.Id,
                    EvaluatedBy = rep.EvaluatedBy,
                    CriterionScore1 = rep.CriterionScore1,
                    CriterionScore2 = rep.CriterionScore2,
                    CriterionScore3 = rep.CriterionScore3,
                    CriterionScore4 = rep.CriterionScore4,
                    FeedbackComments = rep.FeedbackComments,
                    DetailedJsonAnalysis = rep.DetailedJsonAnalysis,
                    IsApproved = rep.IsApproved
                }).ToList()
            }).ToList()
        };
    }
}
