using SmartIELTS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace SmartIELTS.Infrastructure.Data;

public class SmartIeltsDbContext : DbContext
{
    public SmartIeltsDbContext(DbContextOptions<SmartIeltsDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Exam> Exams => Set<Exam>();
    public DbSet<ExamSection> ExamSections => Set<ExamSection>();
    public DbSet<Question> Questions => Set<Question>();
    public DbSet<ExamSession> ExamSessions => Set<ExamSession>();
    public DbSet<CandidateSubmission> CandidateSubmissions => Set<CandidateSubmission>();
    public DbSet<EvaluationReport> EvaluationReports => Set<EvaluationReport>();
    public DbSet<ProctoringLog> ProctoringLogs => Set<ProctoringLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.Property(e => e.Username).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(200);
            entity.Property(e => e.FullName).HasMaxLength(200);
        });

        // Exam
        modelBuilder.Entity<Exam>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).HasMaxLength(250).IsRequired();
            entity.HasMany(e => e.Sections)
                  .WithOne(s => s.Exam)
                  .HasForeignKey(s => s.ExamId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.ExamSessions)
                  .WithOne(s => s.Exam)
                  .HasForeignKey(s => s.ExamId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // ExamSection
        modelBuilder.Entity<ExamSection>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasMany(s => s.Questions)
                  .WithOne(q => q.Section)
                  .HasForeignKey(q => q.SectionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Question
        modelBuilder.Entity<Question>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.QuestionText).IsRequired();
        });

        // ExamSession
        modelBuilder.Entity<ExamSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(s => s.User)
                  .WithMany(u => u.ExamSessions)
                  .HasForeignKey(s => s.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(s => s.Submissions)
                  .WithOne(sub => sub.Session)
                  .HasForeignKey(sub => sub.SessionId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(s => s.ProctoringLogs)
                  .WithOne(p => p.Session)
                  .HasForeignKey(p => p.SessionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // CandidateSubmission
        modelBuilder.Entity<CandidateSubmission>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasMany(sub => sub.EvaluationReports)
                  .WithOne(r => r.Submission)
                  .HasForeignKey(r => r.SubmissionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // EvaluationReport
        modelBuilder.Entity<EvaluationReport>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        // ProctoringLog
        modelBuilder.Entity<ProctoringLog>(entity =>
        {
            entity.HasKey(e => e.Id);
        });
    }
}
