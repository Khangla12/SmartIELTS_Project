using SmartIELTS.Domain.Common;
using SmartIELTS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace SmartIELTS.Infrastructure.Data;

/// <summary>
/// Đồng bộ schema tối thiểu cho các bảng mới được thêm sau khi database đã tồn tại.
/// EnsureCreated() không cập nhật schema của database đã có, nên cần bước này.
/// Khi dự án chuyển sang EF Migrations thì có thể xóa class này.
/// </summary>
public static class SmartIeltsSchemaGuard
{
    public static async Task EnsureAsync(SmartIeltsDbContext context, ILogger? logger = null)
    {
        bool supportsRawSql = context.Database.IsSqlServer() || context.Database.IsSqlite();

        if (supportsRawSql && !await TableExistsAsync(context, "ExamDrafts"))
        {
            string createTableSql = context.Database.IsSqlServer()
                ? @"CREATE TABLE [ExamDrafts] (
                        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                        [SessionId] UNIQUEIDENTIFIER NOT NULL,
                        [SkillType] INT NOT NULL,
                        [DraftContent] NVARCHAR(MAX) NULL,
                        [DraftAnswersJson] NVARCHAR(MAX) NULL,
                        [UpdatedAt] DATETIME2 NOT NULL,
                        CONSTRAINT [FK_ExamDrafts_ExamSessions_SessionId]
                            FOREIGN KEY ([SessionId]) REFERENCES [ExamSessions] ([Id]) ON DELETE CASCADE
                    );
                    CREATE UNIQUE INDEX [IX_ExamDrafts_SessionId_SkillType]
                        ON [ExamDrafts] ([SessionId], [SkillType]);"
                : @"CREATE TABLE IF NOT EXISTS ""ExamDrafts"" (
                        ""Id"" TEXT NOT NULL PRIMARY KEY,
                        ""SessionId"" TEXT NOT NULL,
                        ""SkillType"" INTEGER NOT NULL,
                        ""DraftContent"" TEXT NULL,
                        ""DraftAnswersJson"" TEXT NULL,
                        ""UpdatedAt"" TEXT NOT NULL,
                        CONSTRAINT ""FK_ExamDrafts_ExamSessions_SessionId""
                            FOREIGN KEY (""SessionId"") REFERENCES ""ExamSessions"" (""Id"") ON DELETE CASCADE
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ExamDrafts_SessionId_SkillType""
                        ON ""ExamDrafts"" (""SessionId"", ""SkillType"");";

            try
            {
                await context.Database.ExecuteSqlRawAsync(createTableSql);
                logger?.LogInformation("[SmartIELTS] Đã bổ sung bảng ExamDrafts vào database hiện có.");
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "[SmartIELTS] Không thể tạo bảng ExamDrafts.");
            }
        }

        if (supportsRawSql && !await TableExistsAsync(context, "SpeakingPrompts"))
        {
            string createPromptsSql = context.Database.IsSqlServer()
                ? @"CREATE TABLE [SpeakingPrompts] (
                        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                        [SectionId] UNIQUEIDENTIFIER NOT NULL,
                        [Part] INT NOT NULL,
                        [PromptOrder] INT NOT NULL,
                        [QuestionText] NVARCHAR(MAX) NOT NULL,
                        [CandidatePromptText] NVARCHAR(MAX) NOT NULL,
                        [PreparationSeconds] INT NOT NULL,
                        [ResponseSeconds] INT NOT NULL,
                        CONSTRAINT [FK_SpeakingPrompts_ExamSections_SectionId]
                            FOREIGN KEY ([SectionId]) REFERENCES [ExamSections] ([Id]) ON DELETE CASCADE
                    );
                    CREATE INDEX [IX_SpeakingPrompts_SectionId_Part_PromptOrder]
                        ON [SpeakingPrompts] ([SectionId], [Part], [PromptOrder]);"
                : @"CREATE TABLE IF NOT EXISTS ""SpeakingPrompts"" (
                        ""Id"" TEXT NOT NULL PRIMARY KEY,
                        ""SectionId"" TEXT NOT NULL,
                        ""Part"" INTEGER NOT NULL,
                        ""PromptOrder"" INTEGER NOT NULL,
                        ""QuestionText"" TEXT NOT NULL,
                        ""CandidatePromptText"" TEXT NOT NULL,
                        ""PreparationSeconds"" INTEGER NOT NULL,
                        ""ResponseSeconds"" INTEGER NOT NULL,
                        CONSTRAINT ""FK_SpeakingPrompts_ExamSections_SectionId""
                            FOREIGN KEY (""SectionId"") REFERENCES ""ExamSections"" (""Id"") ON DELETE CASCADE
                    );
                    CREATE INDEX IF NOT EXISTS ""IX_SpeakingPrompts_SectionId_Part_PromptOrder""
                        ON ""SpeakingPrompts"" (""SectionId"", ""Part"", ""PromptOrder"");";

            try
            {
                await context.Database.ExecuteSqlRawAsync(createPromptsSql);
                logger?.LogInformation("[SmartIELTS] Đã bổ sung bảng SpeakingPrompts vào database hiện có.");
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "[SmartIELTS] Không thể tạo bảng SpeakingPrompts.");
            }
        }

        // Bảng phân công giảng viên chấm đề.
        if (supportsRawSql && !await TableExistsAsync(context, "ExamAssignments"))
        {
            string createAssignmentsSql = context.Database.IsSqlServer()
                ? @"CREATE TABLE [ExamAssignments] (
                        [Id] UNIQUEIDENTIFIER NOT NULL PRIMARY KEY,
                        [ExamId] UNIQUEIDENTIFIER NOT NULL,
                        [TeacherId] UNIQUEIDENTIFIER NOT NULL,
                        [AllowedSkills] NVARCHAR(200) NULL,
                        [AssignedAt] DATETIME2 NOT NULL,
                        [IsActive] BIT NOT NULL,
                        CONSTRAINT [FK_ExamAssignments_Exams_ExamId]
                            FOREIGN KEY ([ExamId]) REFERENCES [Exams] ([Id]) ON DELETE CASCADE,
                        CONSTRAINT [FK_ExamAssignments_Users_TeacherId]
                            FOREIGN KEY ([TeacherId]) REFERENCES [Users] ([Id]) ON DELETE CASCADE
                    );
                    CREATE UNIQUE INDEX [IX_ExamAssignments_ExamId_TeacherId]
                        ON [ExamAssignments] ([ExamId], [TeacherId]);"
                : @"CREATE TABLE IF NOT EXISTS ""ExamAssignments"" (
                        ""Id"" TEXT NOT NULL PRIMARY KEY,
                        ""ExamId"" TEXT NOT NULL,
                        ""TeacherId"" TEXT NOT NULL,
                        ""AllowedSkills"" TEXT NULL,
                        ""AssignedAt"" TEXT NOT NULL,
                        ""IsActive"" INTEGER NOT NULL,
                        CONSTRAINT ""FK_ExamAssignments_Exams_ExamId""
                            FOREIGN KEY (""ExamId"") REFERENCES ""Exams"" (""Id"") ON DELETE CASCADE,
                        CONSTRAINT ""FK_ExamAssignments_Users_TeacherId""
                            FOREIGN KEY (""TeacherId"") REFERENCES ""Users"" (""Id"") ON DELETE CASCADE
                    );
                    CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ExamAssignments_ExamId_TeacherId""
                        ON ""ExamAssignments"" (""ExamId"", ""TeacherId"");";

            try
            {
                await context.Database.ExecuteSqlRawAsync(createAssignmentsSql);
                logger?.LogInformation("[SmartIELTS] Đã bổ sung bảng ExamAssignments vào database hiện có.");
            }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "[SmartIELTS] Không thể tạo bảng ExamAssignments.");
            }
        }

        // Bổ sung 3 cột mới cho EvaluationReports (tách nhận xét giảng viên khỏi nhận xét AI).
        if (supportsRawSql)
        {
            await AddColumnIfMissingAsync(context, "EvaluationReports", "TeacherFeedback",
                context.Database.IsSqlServer() ? "NVARCHAR(MAX) NULL" : "TEXT NULL", logger);

            await AddColumnIfMissingAsync(context, "EvaluationReports", "ApprovedAt",
                context.Database.IsSqlServer() ? "DATETIME2 NULL" : "TEXT NULL", logger);

            await AddColumnIfMissingAsync(context, "EvaluationReports", "ApprovedBy",
                context.Database.IsSqlServer() ? "NVARCHAR(100) NULL" : "TEXT NULL", logger);
        }

        // Nâng cấp mật khẩu plaintext của dữ liệu seed cũ sang PBKDF2.
        var plaintextUsers = await context.Users
            .Where(u => !string.IsNullOrEmpty(u.PasswordHash) && !u.PasswordHash.Contains("."))
            .ToListAsync();

        if (plaintextUsers.Count == 0) return;

        foreach (var user in plaintextUsers)
        {
            user.PasswordHash = PasswordHasher.Hash(user.PasswordHash);
        }

        await context.SaveChangesAsync();
        logger?.LogInformation("[SmartIELTS] Đã nâng cấp {Count} mật khẩu sang PBKDF2.", plaintextUsers.Count);
    }

    /// <summary>Thêm cột nếu chưa tồn tại (EnsureCreated không cập nhật schema DB đã có).</summary>
    private static async Task AddColumnIfMissingAsync(
        SmartIeltsDbContext context, string table, string column, string sqlType, ILogger? logger)
    {
        // Whitelist: chỉ cho phép tên bảng/cột/kiểu dữ liệu đã biết -> chặn SQL injection.
        var allowedTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "EvaluationReports" };
        var allowedColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "TeacherFeedback", "ApprovedAt", "ApprovedBy" };
        var allowedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "NVARCHAR(MAX) NULL", "TEXT NULL", "DATETIME2 NULL", "NVARCHAR(100) NULL" };

        if (!allowedTables.Contains(table) || !allowedColumns.Contains(column) || !allowedTypes.Contains(sqlType))
        {
            logger?.LogWarning("[SmartIELTS] Từ chối thêm cột không nằm trong whitelist: {Table}.{Column}", table, column);
            return;
        }

        try
        {
            bool isSqlServer = context.Database.IsSqlServer();

            string probeSql = isSqlServer
                ? $"SELECT CASE WHEN COL_LENGTH('dbo.{table}', '{column}') IS NULL THEN 0 ELSE 1 END"
                : $"SELECT CASE WHEN EXISTS (SELECT 1 FROM pragma_table_info('{table}') WHERE name='{column}') THEN 1 ELSE 0 END";

            var probe = await context.Database.SqlQueryRaw<int>(probeSql).ToListAsync();
            if (probe.FirstOrDefault() == 1) return;

            string alterSql = $"ALTER TABLE [{table}] ADD [{column}] {sqlType};";
            await context.Database.ExecuteSqlRawAsync(alterSql);

            logger?.LogInformation("[SmartIELTS] Đã thêm cột {Table}.{Column}.", table, column);
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex, "[SmartIELTS] Không thêm được cột {Table}.{Column}.", table, column);
        }
    }

    private static async Task<bool> TableExistsAsync(SmartIeltsDbContext context, string tableName)
    {
        try
        {
            string sql = context.Database.IsSqlServer()
                ? $"SELECT CASE WHEN OBJECT_ID(N'dbo.{tableName}', N'U') IS NULL THEN 0 ELSE 1 END"
                : $"SELECT CASE WHEN EXISTS (SELECT 1 FROM sqlite_master WHERE type='table' AND name='{tableName}') THEN 1 ELSE 0 END";

            var result = await context.Database
                .SqlQueryRaw<int>(sql)
                .ToListAsync();

            return result.FirstOrDefault() == 1;
        }
        catch
        {
            // Không xác định được thì thử CREATE ... IF NOT EXISTS ở bước sau.
            return false;
        }
    }
}
