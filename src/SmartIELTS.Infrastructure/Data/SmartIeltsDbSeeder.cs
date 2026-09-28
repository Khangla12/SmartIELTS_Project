using System.Text.Json;
using SmartIELTS.Domain.Entities;
using SmartIELTS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace SmartIELTS.Infrastructure.Data;

public static class SmartIeltsDbSeeder
{
    public static async Task SeedAsync(SmartIeltsDbContext context)
    {
        if (await context.Users.AnyAsync())
        {
            return; // Already seeded
        }

        // 1. Seed Users
        var admin = new User
        {
            Username = "admin",
            PasswordHash = "Admin@123",
            FullName = "Hệ Thống Khảo Thí SmartIELTS (Administrator)",
            Email = "admin@ailes.fpt.edu.vn",
            Role = UserRole.Admin
        };

        var teacher = new User
        {
            Username = "teacher",
            PasswordHash = "Teacher@123",
            FullName = "ThS. Nguyễn Văn Giảng (Examiner)",
            Email = "teacher@ailes.fpt.edu.vn",
            Role = UserRole.Teacher
        };

        var student = new User
        {
            Username = "student",
            PasswordHash = "Student@123",
            FullName = "Lê Hoàng Long (Thí sinh IELTS)",
            Email = "longlhse160000@fpt.edu.vn",
            Role = UserRole.Candidate
        };

        await context.Users.AddRangeAsync(admin, teacher, student);

        // 2. Seed Full IELTS Academic Exam
        var exam = new Exam
        {
            Title = "IELTS Academic Official Mock Test 2026 - Cambridge Standard",
            Description = "Bài thi thử IELTS 4 kỹ năng hoàn chỉnh mô phỏng kỳ thi trên máy tính (Computer-Delivered IELTS), hỗ trợ AI chấm tự động và Giám thị Kiosk.",
            ExamType = ExamType.Academic,
            TotalDurationMinutes = 165,
            IsActive = true
        };

        // Section 1: Listening
        var secListening = new ExamSection
        {
            Exam = exam,
            SkillType = SkillType.Listening,
            SectionOrder = 1,
            DurationMinutes = 30,
            MediaUrl = "https://assets.ailes.edu.vn/audio/cambridge_mock_test1.mp3",
            PassageText = "IELTS Listening Section 1: Telephone inquiry between Customer and Hotel Receptionist regarding reservation details."
        };

        secListening.Questions.Add(new Question
        {
            QuestionNumber = 1,
            QuestionType = QuestionType.FillBlank,
            QuestionText = "Customer Family Name: John ____________",
            CorrectAnswer = "Anderson",
            MaxScore = 1.0
        });

        secListening.Questions.Add(new Question
        {
            QuestionNumber = 2,
            QuestionType = QuestionType.FillBlank,
            QuestionText = "Check-in Arrival Date: ____________",
            CorrectAnswer = "25 October / October 25",
            MaxScore = 1.0
        });

        secListening.Questions.Add(new Question
        {
            QuestionNumber = 3,
            QuestionType = QuestionType.FillBlank,
            QuestionText = "Number of single rooms requested: ____________",
            CorrectAnswer = "3 / three",
            MaxScore = 1.0
        });

        secListening.Questions.Add(new Question
        {
            QuestionNumber = 4,
            QuestionType = QuestionType.MultipleChoice,
            QuestionText = "Preferred method of settlement:",
            OptionsJson = JsonSerializer.Serialize(new[] { "Credit Card", "Bank Wire Transfer", "Cash on Arrival" }),
            CorrectAnswer = "Credit Card",
            MaxScore = 1.0
        });

        secListening.Questions.Add(new Question
        {
            QuestionNumber = 5,
            QuestionType = QuestionType.FillBlank,
            QuestionText = "Preferred interior suite theme ____________:",
            CorrectAnswer = "color / colour",
            MaxScore = 1.0
        });

        // Section 2: Reading
        var secReading = new ExamSection
        {
            Exam = exam,
            SkillType = SkillType.Reading,
            SectionOrder = 2,
            DurationMinutes = 60,
            PassageText = "THE EVOLUTION AND ETHICAL HORIZONS OF ARTIFICIAL INTELLIGENCE\n\n" +
                "Paragraph A: In 1950, British mathematician Alan Turing published his seminal work proposing what is now famously recognized as the Turing Test. Turing posited that if an observer could not reliably distinguish whether they were conversing with a human or a machine, the machine had demonstrated cognitive intelligence. For decades following the Dartmouth Conference of 1956, artificial intelligence encountered alternating phases of boundless enthusiasm and debilitating \"AI winters\" marked by funding shortages and computational limitations.\n\n" +
                "Paragraph B: The renaissance of machine intelligence was fundamentally catalyzed in the 2010s by three converging breakthroughs: the proliferation of high-performance Graphics Processing Units (GPUs), the democratization of immense web-scale datasets, and the innovation of deep transformer architectures. Modern machine learning systems do not rely merely on handcrafted heuristic rules; instead, they learn intricate hierarchical patterns directly from raw data.\n\n" +
                "Paragraph C: Nevertheless, ethical dilemmas accompany these technological triumphs. Algorithmic bias, data privacy infringements, and the potential displacement of skilled labor present formidable challenges. Scholars and regulatory bodies worldwide are vigorously debating comprehensive governance frameworks, though universal international consensus on copyright and intellectual property remains elusive."
        };

        secReading.Questions.Add(new Question
        {
            QuestionNumber = 1,
            QuestionType = QuestionType.TrueFalseNotGiven,
            QuestionText = "Alan Turing proposed a benchmark test for machine intelligence in 1950.",
            OptionsJson = JsonSerializer.Serialize(new[] { "True", "False", "Not Given" }),
            CorrectAnswer = "True",
            MaxScore = 1.0
        });

        secReading.Questions.Add(new Question
        {
            QuestionNumber = 2,
            QuestionType = QuestionType.TrueFalseNotGiven,
            QuestionText = "The 1980s was widely characterized as the golden era of immediate commercial profitability for deep neural networks.",
            OptionsJson = JsonSerializer.Serialize(new[] { "True", "False", "Not Given" }),
            CorrectAnswer = "False",
            MaxScore = 1.0
        });

        secReading.Questions.Add(new Question
        {
            QuestionNumber = 3,
            QuestionType = QuestionType.TrueFalseNotGiven,
            QuestionText = "Global governments have finalized a legally binding, uniform treaty governing generative AI intellectual property.",
            OptionsJson = JsonSerializer.Serialize(new[] { "True", "False", "Not Given" }),
            CorrectAnswer = "Not Given",
            MaxScore = 1.0
        });

        secReading.Questions.Add(new Question
        {
            QuestionNumber = 4,
            QuestionType = QuestionType.MultipleChoice,
            QuestionText = "According to Paragraph B, what was the primary technological catalyst for modern deep learning advances?",
            OptionsJson = JsonSerializer.Serialize(new[] { "High-performance GPUs and massive web-scale datasets", "Handcrafted rule-based expert systems", "Strict government copyright subsidies" }),
            CorrectAnswer = "High-performance GPUs and massive web-scale datasets",
            MaxScore = 1.0
        });

        secReading.Questions.Add(new Question
        {
            QuestionNumber = 5,
            QuestionType = QuestionType.FillBlank,
            QuestionText = "Modern deep neural networks discern complex hierarchical features directly from ____________.",
            CorrectAnswer = "raw data",
            MaxScore = 1.0
        });

        // Section 3: Writing
        var secWriting = new ExamSection
        {
            Exam = exam,
            SkillType = SkillType.Writing,
            SectionOrder = 3,
            DurationMinutes = 60,
            PassageText = "WRITING TASK 2\n\nYou should spend about 40 minutes on this task.\nWrite about the following topic:\n\n\"Some educational experts argue that within the next few decades, artificial intelligence systems and automated tutors will largely supplant human schoolteachers in standard classroom instruction.\nTo what extent do you agree or disagree with this perspective?\"\n\nGive reasons for your answer and include any relevant examples from your own knowledge or experience.\nWrite at least 250 words."
        };

        // Section 4: Speaking
        var secSpeaking = new ExamSection
        {
            Exam = exam,
            SkillType = SkillType.Speaking,
            SectionOrder = 4,
            DurationMinutes = 15,
            PassageText = "SPEAKING PART 3: TECHNOLOGY & SOCIETY\n\n1. How has modern automation transformed the workplace compared to previous generations?\n2. What ethical measures should educational institutions implement when students utilize artificial intelligence for research?\n3. Do you foresee artificial intelligence exacerbating socioeconomic disparities, and how might governments address this?"
        };

        exam.Sections.Add(secListening);
        exam.Sections.Add(secReading);
        exam.Sections.Add(secWriting);
        exam.Sections.Add(secSpeaking);

        await context.Exams.AddAsync(exam);
        await context.SaveChangesAsync();
    }
}
