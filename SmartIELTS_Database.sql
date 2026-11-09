-- =========================================================================
-- HỆ THỐNG KHẢO THÍ TIẾNG ANH TOÀN DIỆN IELTS 4 KỸ NĂNG (SmartIELTS)
-- SCRIPT KHỞI TẠO CƠ SỞ DỮ LIỆU SQL SERVER (3NF) - SWD392 & PRN222
-- =========================================================================

USE master;
GO

-- 1. Khởi tạo Cơ sở dữ liệu SmartIELTS_DB nếu chưa tồn tại
IF NOT EXISTS (SELECT * FROM sys.databases WHERE name = 'SmartIELTS_DB')
BEGIN
    CREATE DATABASE SmartIELTS_DB;
    PRINT N'Cơ sở dữ liệu SmartIELTS_DB đã được tạo thành công!';
END
GO

USE SmartIELTS_DB;
GO

-- 2. Xóa bảng cũ nếu cần thiết lập lại từ đầu
IF OBJECT_ID('dbo.ProctoringLogs', 'U') IS NOT NULL DROP TABLE dbo.ProctoringLogs;
IF OBJECT_ID('dbo.ExamDrafts', 'U') IS NOT NULL DROP TABLE dbo.ExamDrafts;
IF OBJECT_ID('dbo.SpeakingPrompts', 'U') IS NOT NULL DROP TABLE dbo.SpeakingPrompts;
IF OBJECT_ID('dbo.EvaluationReports', 'U') IS NOT NULL DROP TABLE dbo.EvaluationReports;
IF OBJECT_ID('dbo.CandidateSubmissions', 'U') IS NOT NULL DROP TABLE dbo.CandidateSubmissions;
IF OBJECT_ID('dbo.ExamSessions', 'U') IS NOT NULL DROP TABLE dbo.ExamSessions;
IF OBJECT_ID('dbo.Questions', 'U') IS NOT NULL DROP TABLE dbo.Questions;
IF OBJECT_ID('dbo.ExamSections', 'U') IS NOT NULL DROP TABLE dbo.ExamSections;
IF OBJECT_ID('dbo.Exams', 'U') IS NOT NULL DROP TABLE dbo.Exams;
IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL DROP TABLE dbo.Users;
GO

-- 3. Tạo Bảng Users (Quản lý người dùng)
CREATE TABLE dbo.Users (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    Username NVARCHAR(100) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(MAX) NOT NULL,
    FullName NVARCHAR(200) NOT NULL,
    Email NVARCHAR(200) NULL,
    Role INT NOT NULL DEFAULT 1, -- 1: Candidate, 2: Teacher, 3: Admin
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
GO

-- 4. Tạo Bảng Exams (Đề thi IELTS)
CREATE TABLE dbo.Exams (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    Title NVARCHAR(250) NOT NULL,
    Description NVARCHAR(MAX) NULL,
    ExamType INT NOT NULL DEFAULT 1, -- 1: Academic, 2: GeneralTraining
    TotalDurationMinutes INT NOT NULL DEFAULT 165,
    IsActive BIT NOT NULL DEFAULT 1,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
GO

-- 5. Tạo Bảng ExamSections (4 Kỹ năng của Đề thi)
CREATE TABLE dbo.ExamSections (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    ExamId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES dbo.Exams(Id) ON DELETE CASCADE,
    SkillType INT NOT NULL, -- 1: Listening, 2: Reading, 3: Writing, 4: Speaking
    SectionOrder INT NOT NULL,
    DurationMinutes INT NOT NULL,
    MediaUrl NVARCHAR(MAX) NULL,
    PassageText NVARCHAR(MAX) NULL
);
GO

-- 6. Tạo Bảng Questions (Câu hỏi thi)
CREATE TABLE dbo.Questions (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    SectionId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES dbo.ExamSections(Id) ON DELETE CASCADE,
    QuestionNumber INT NOT NULL,
    QuestionType INT NOT NULL, -- 1: MultipleChoice, 2: FillBlank, 3: TrueFalseNotGiven, 4: Matching, 5: Essay, 6: SpeakingPrompt
    QuestionText NVARCHAR(MAX) NOT NULL,
    OptionsJson NVARCHAR(MAX) NULL,
    CorrectAnswer NVARCHAR(MAX) NOT NULL,
    MaxScore FLOAT NOT NULL DEFAULT 1.0
);
GO

-- 7. Tạo Bảng ExamSessions (Phiên/Ca thi của Thí sinh)
CREATE TABLE dbo.ExamSessions (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    UserId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES dbo.Users(Id) ON DELETE CASCADE,
    ExamId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES dbo.Exams(Id),
    StartTime DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    EndTime DATETIME2 NULL,
    Status INT NOT NULL DEFAULT 1, -- 1: InProgress, 2: Submitted, 3: Evaluated, 4: Published
    OverallBandScore FLOAT NULL
);
GO

-- 8. Tạo Bảng CandidateSubmissions (Bài làm của từng kỹ năng)
CREATE TABLE dbo.CandidateSubmissions (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    SessionId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES dbo.ExamSessions(Id) ON DELETE CASCADE,
    SkillType INT NOT NULL,
    SubmittedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    RawScore FLOAT NULL,
    BandScore FLOAT NULL,
    AudioRecordUrl NVARCHAR(MAX) NULL,
    EssayContent NVARCHAR(MAX) NULL,
    AnswersJson NVARCHAR(MAX) NULL
);
GO

-- 9. Tạo Bảng EvaluationReports (Bản chấm điểm AI & Giảng viên thẩm định)
CREATE TABLE dbo.EvaluationReports (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    SubmissionId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES dbo.CandidateSubmissions(Id) ON DELETE CASCADE,
    EvaluatedBy INT NOT NULL DEFAULT 1, -- 1: AiEngine, 2: Teacher
    CriterionScore1 FLOAT NULL, -- Task Response / Fluency
    CriterionScore2 FLOAT NULL, -- Coherence & Cohesion / Pronunciation
    CriterionScore3 FLOAT NULL, -- Lexical Resource
    CriterionScore4 FLOAT NULL, -- Grammatical Range & Accuracy
    FeedbackComments NVARCHAR(MAX) NULL,
    DetailedJsonAnalysis NVARCHAR(MAX) NULL,
    IsApproved BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
GO

-- 10. Tạo Bảng ProctoringLogs (Nhật ký Giám sát Kiosk Mode)
CREATE TABLE dbo.ProctoringLogs (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    SessionId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES dbo.ExamSessions(Id) ON DELETE CASCADE,
    Timestamp DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ViolationType INT NOT NULL, -- 1: TabSwitch, 2: FaceAbsent, 3: MultipleFaces, 4: NoiseDetected, 5: HotkeyBlocked
    EvidenceSnapshotUrl NVARCHAR(MAX) NULL,
    Description NVARCHAR(MAX) NULL
);
GO

-- 10b. Tạo Bảng ExamDrafts (Bản nháp Autosave - Offline-First)
CREATE TABLE dbo.ExamDrafts (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    SessionId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES dbo.ExamSessions(Id) ON DELETE CASCADE,
    SkillType INT NOT NULL, -- 1: Listening, 2: Reading, 3: Writing, 4: Speaking
    DraftContent NVARCHAR(MAX) NULL,
    DraftAnswersJson NVARCHAR(MAX) NULL,
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
GO

CREATE UNIQUE INDEX IX_ExamDrafts_SessionId_SkillType ON dbo.ExamDrafts (SessionId, SkillType);
GO

-- 10c. Tạo Bảng SpeakingPrompts (Kịch bản vấn đáp Speaking: câu hỏi + text gợi ý)
CREATE TABLE dbo.SpeakingPrompts (
    Id UNIQUEIDENTIFIER NOT NULL PRIMARY KEY DEFAULT NEWID(),
    SectionId UNIQUEIDENTIFIER NOT NULL FOREIGN KEY REFERENCES dbo.ExamSections(Id) ON DELETE CASCADE,
    Part INT NOT NULL, -- 1: Part1Introduction, 2: Part2LongTurn, 3: Part3Discussion
    PromptOrder INT NOT NULL,
    QuestionText NVARCHAR(MAX) NOT NULL,
    CandidatePromptText NVARCHAR(MAX) NOT NULL,
    PreparationSeconds INT NOT NULL DEFAULT 0,
    ResponseSeconds INT NOT NULL DEFAULT 60
);
GO

CREATE INDEX IX_SpeakingPrompts_SectionId_Part_PromptOrder ON dbo.SpeakingPrompts (SectionId, Part, PromptOrder);
GO

-- =========================================================================
-- 11. SEED DATA MẪU (TÀI KHOẢN & ĐỀ THI IELTS ACADEMIC 4 KỸ NĂNG)
-- =========================================================================

-- Người dùng mẫu
DECLARE @AdminId UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';
DECLARE @TeacherId UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222222';
DECLARE @StudentId UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333333';

INSERT INTO dbo.Users (Id, Username, PasswordHash, FullName, Email, Role) VALUES
(@AdminId, 'admin', 'Admin@123', N'Hệ Thống Khảo Thí SmartIELTS (Administrator)', 'admin@ailes.fpt.edu.vn', 3),
(@TeacherId, 'teacher', 'Teacher@123', N'ThS. Nguyễn Văn Giảng (Examiner)', 'teacher@ailes.fpt.edu.vn', 2),
(@StudentId, 'student', 'Student@123', N'Lê Hoàng Long (Thí sinh IELTS)', 'longlhse160000@fpt.edu.vn', 1);

-- Đề thi mẫu
DECLARE @ExamId UNIQUEIDENTIFIER = 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa';
INSERT INTO dbo.Exams (Id, Title, Description, ExamType, TotalDurationMinutes, IsActive) VALUES
(@ExamId, N'IELTS Academic Official Mock Test 2026 - Cambridge Standard', 
 N'Bài thi thử IELTS 4 kỹ năng hoàn chỉnh mô phỏng kỳ thi trên máy tính, hỗ trợ AI chấm tự động và Giám thị Kiosk.', 1, 165, 1);

-- Section 1: Listening
DECLARE @SecListeningId UNIQUEIDENTIFIER = 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb';
INSERT INTO dbo.ExamSections (Id, ExamId, SkillType, SectionOrder, DurationMinutes, MediaUrl, PassageText) VALUES
(@SecListeningId, @ExamId, 1, 1, 30, 'media/listening_section1_hotel_reservation.wav', 
 N'IELTS Listening Section 1: Telephone inquiry between Customer and Hotel Receptionist regarding reservation details.');

INSERT INTO dbo.Questions (Id, SectionId, QuestionNumber, QuestionType, QuestionText, OptionsJson, CorrectAnswer, MaxScore) VALUES
(NEWID(), @SecListeningId, 1, 2, N'Customer Family Name: John ____________', NULL, 'Anderson', 1.0),
(NEWID(), @SecListeningId, 2, 2, N'Check-in Arrival Date: ____________', NULL, '25 October / October 25', 1.0),
(NEWID(), @SecListeningId, 3, 2, N'Number of single rooms requested: ____________', NULL, '3 / three', 1.0),
(NEWID(), @SecListeningId, 4, 1, N'Preferred method of settlement:', '["Credit Card","Bank Wire Transfer","Cash on Arrival"]', 'Credit Card', 1.0),
(NEWID(), @SecListeningId, 5, 2, N'Preferred interior suite theme ____________:', NULL, 'color / colour', 1.0);

-- Section 2: Reading
DECLARE @SecReadingId UNIQUEIDENTIFIER = 'cccccccc-cccc-cccc-cccc-cccccccccccc';
INSERT INTO dbo.ExamSections (Id, ExamId, SkillType, SectionOrder, DurationMinutes, MediaUrl, PassageText) VALUES
(@SecReadingId, @ExamId, 2, 2, 60, NULL, 
 N'THE EVOLUTION AND ETHICAL HORIZONS OF ARTIFICIAL INTELLIGENCE

Paragraph A: In 1950, British mathematician Alan Turing published his seminal work proposing what is now famously recognized as the Turing Test. Turing posited that if an observer could not reliably distinguish whether they were conversing with a human or a machine, the machine had demonstrated cognitive intelligence.

Paragraph B: The renaissance of machine intelligence was fundamentally catalyzed in the 2010s by three converging breakthroughs: the proliferation of high-performance Graphics Processing Units (GPUs), the democratization of immense web-scale datasets, and the innovation of deep transformer architectures. Modern machine learning systems do not rely merely on handcrafted heuristic rules; instead, they learn intricate hierarchical patterns directly from raw data.

Paragraph C: Nevertheless, ethical dilemmas accompany these technological triumphs. Algorithmic bias, data privacy infringements, and the potential displacement of skilled labor present formidable challenges.');

INSERT INTO dbo.Questions (Id, SectionId, QuestionNumber, QuestionType, QuestionText, OptionsJson, CorrectAnswer, MaxScore) VALUES
(NEWID(), @SecReadingId, 1, 3, N'Alan Turing proposed a benchmark test for machine intelligence in 1950.', '["True","False","Not Given"]', 'True', 1.0),
(NEWID(), @SecReadingId, 2, 3, N'The 1980s was widely characterized as the golden era of immediate commercial profitability for deep neural networks.', '["True","False","Not Given"]', 'False', 1.0),
(NEWID(), @SecReadingId, 3, 3, N'Global governments have finalized a legally binding, uniform treaty governing generative AI intellectual property.', '["True","False","Not Given"]', 'Not Given', 1.0),
(NEWID(), @SecReadingId, 4, 1, N'According to Paragraph B, what was the primary technological catalyst for modern deep learning advances?', '["High-performance GPUs and massive web-scale datasets","Handcrafted rule-based expert systems","Strict government copyright subsidies"]', 'High-performance GPUs and massive web-scale datasets', 1.0),
(NEWID(), @SecReadingId, 5, 2, N'Modern deep neural networks discern complex hierarchical features directly from ____________.', NULL, 'raw data', 1.0);

-- Section 3: Writing
DECLARE @SecWritingId UNIQUEIDENTIFIER = 'dddddddd-dddd-dddd-dddd-dddddddddddd';
INSERT INTO dbo.ExamSections (Id, ExamId, SkillType, SectionOrder, DurationMinutes, MediaUrl, PassageText) VALUES
(@SecWritingId, @ExamId, 3, 3, 60, NULL, 
 N'WRITING TASK 2

You should spend about 40 minutes on this task.
Write about the following topic:

"Some educational experts argue that within the next few decades, artificial intelligence systems and automated tutors will largely supplant human schoolteachers in standard classroom instruction.
To what extent do you agree or disagree with this perspective?"

Give reasons for your answer and include any relevant examples from your own knowledge or experience.
Write at least 250 words.');

-- Section 4: Speaking
DECLARE @SecSpeakingId UNIQUEIDENTIFIER = 'eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee';
INSERT INTO dbo.ExamSections (Id, ExamId, SkillType, SectionOrder, DurationMinutes, MediaUrl, PassageText) VALUES
(@SecSpeakingId, @ExamId, 4, 4, 15, NULL, 
 N'SPEAKING PART 3: TECHNOLOGY & SOCIETY

1. How has modern automation transformed the workplace compared to previous generations?
2. What ethical measures should educational institutions implement when students utilize artificial intelligence for research?
3. Do you foresee artificial intelligence exacerbating socioeconomic disparities, and how might governments address this?');
GO

PRINT N'Hoàn tất nạp cấu trúc CSDL và Dữ liệu mẫu vào SmartIELTS_DB thành công!';
GO
