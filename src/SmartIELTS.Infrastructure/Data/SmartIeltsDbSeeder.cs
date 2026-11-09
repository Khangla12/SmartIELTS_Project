using System.Text.Json;
using SmartIELTS.Domain.Common;
using SmartIELTS.Domain.Entities;
using SmartIELTS.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace SmartIELTS.Infrastructure.Data;

public static class SmartIeltsDbSeeder
{
    public static async Task SeedAsync(SmartIeltsDbContext context)
    {
        // Bổ sung kịch bản Speaking cho database cũ (thêm sau khi đã seed lần đầu).
        await SeedSpeakingPromptsIfMissingAsync(context);

        if (await context.Users.AnyAsync())
        {
            return; // Already seeded
        }

        // 1. Seed Users
        var admin = new User
        {
            Username = "admin",
            PasswordHash = PasswordHasher.Hash("Admin@123"),
            FullName = "Hệ Thống Khảo Thí SmartIELTS (Administrator)",
            Email = "admin@ailes.fpt.edu.vn",
            Role = UserRole.Admin
        };

        var teacher = new User
        {
            Username = "teacher",
            PasswordHash = PasswordHasher.Hash("Teacher@123"),
            FullName = "ThS. Nguyễn Văn Giảng (Examiner)",
            Email = "teacher@ailes.fpt.edu.vn",
            Role = UserRole.Teacher
        };

        var student = new User
        {
            Username = "student",
            PasswordHash = PasswordHasher.Hash("Student@123"),
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
            MediaUrl = "media/listening_section1_hotel_reservation.wav",
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

        // Kịch bản vấn đáp: mỗi câu gồm câu hỏi giám khảo + text gợi ý cho thí sinh đọc trước khi trả lời.
        secSpeaking.SpeakingPrompts.Add(new SpeakingPrompt
        {
            Part = SpeakingPart.Part1Introduction,
            PromptOrder = 1,
            QuestionText = "Let's talk about where you live. Do you live in a house or an apartment?",
            CandidatePromptText =
                "Hãy đọc và trả lời bằng tiếng Anh (2-3 câu):\n\n" +
                "• Bạn sống ở nhà riêng hay căn hộ?\n" +
                "• Nêu 1 lý do bạn thích / không thích.\n\n" +
                "Mẫu: \"I live in a small apartment in the city centre, mainly because it's convenient for commuting.\"\n" +
                "Từ vựng gợi ý: spacious, cosy, neighbourhood, commute, furnished.",
            ResponseSeconds = 40
        });

        secSpeaking.SpeakingPrompts.Add(new SpeakingPrompt
        {
            Part = SpeakingPart.Part1Introduction,
            PromptOrder = 2,
            QuestionText = "What do you usually do in your free time?",
            CandidatePromptText =
                "Hãy đọc và trả lời (2-3 câu):\n\n" +
                "• 2 hoạt động bạn thường làm khi rảnh\n" +
                "• Vì sao bạn thích chúng?\n\n" +
                "Mẫu: \"In my free time, I usually read and go jogging, which helps me unwind after work.\"\n" +
                "Từ vựng gợi ý: unwind, take up, recreational, relieve stress.",
            ResponseSeconds = 40
        });

        secSpeaking.SpeakingPrompts.Add(new SpeakingPrompt
        {
            Part = SpeakingPart.Part1Introduction,
            PromptOrder = 3,
            QuestionText = "Do you prefer studying alone or with other people? Why?",
            CandidatePromptText =
                "Hãy đọc và trả lời (2-3 câu):\n\n" +
                "• Chọn 1 hình thức\n" +
                "• So sánh ngắn với hình thức còn lại\n\n" +
                "Mẫu: \"I lean towards studying alone because I can focus better, although group study helps when I get stuck.\"\n" +
                "Từ vựng gợi ý: concentrate, distract, collaborative, discipline.",
            ResponseSeconds = 40
        });

        secSpeaking.SpeakingPrompts.Add(new SpeakingPrompt
        {
            Part = SpeakingPart.Part2LongTurn,
            PromptOrder = 1,
            QuestionText =
                "Now, I'd like you to describe a skill you would like to learn in the future.\n" +
                "You should say: what the skill is; why you want to learn it; how you would learn it; and explain how this skill would benefit you.",
            CandidatePromptText =
                "CUE CARD — Đọc kỹ và chuẩn bị 1 phút, sau đó nói liên tục 1.5-2 phút.\n\n" +
                "Describe a skill you would like to learn in the future.\n" +
                "You should say:\n" +
                "  • what the skill is\n" +
                "  • why you want to learn it\n" +
                "  • how you would learn it\n" +
                "  • and explain how this skill would benefit you\n\n" +
                "Dàn ý gợi ý:\n" +
                "1) Mở bài: \"The skill I'd most like to master is ...\"\n" +
                "2) Lý do: career / personal interest\n" +
                "3) Cách học: online course, mentor, daily practice\n" +
                "4) Lợi ích: \"This would allow me to ... which is invaluable because ...\"\n" +
                "Từ vựng: master, proficiency, hands-on, self-taught, perseverance, rewarding.",
            PreparationSeconds = 60,
            ResponseSeconds = 120
        });

        secSpeaking.SpeakingPrompts.Add(new SpeakingPrompt
        {
            Part = SpeakingPart.Part3Discussion,
            PromptOrder = 1,
            QuestionText = "Why do you think some people are more willing to learn new skills than others?",
            CandidatePromptText =
                "PART 3 — Thảo luận mở rộng. Trả lời 2-3 ý kèm ví dụ (60-100 từ).\n\n" +
                "Gợi ý triển khai:\n" +
                "• Yếu tố 1: tính cách (mindset, resilience)\n" +
                "• Yếu tố 2: môi trường (supportive environment, exposure)\n" +
                "• Ví dụ cụ thể\n\n" +
                "Từ vựng: intrinsic motivation, perseverance, role model.",
            ResponseSeconds = 90
        });

        secSpeaking.SpeakingPrompts.Add(new SpeakingPrompt
        {
            Part = SpeakingPart.Part3Discussion,
            PromptOrder = 2,
            QuestionText = "How has technology changed the way people acquire new skills compared to the past?",
            CandidatePromptText =
                "Trả lời theo cấu trúc so sánh quá khứ / hiện tại:\n\n" +
                "• Quá khứ: \"In the past, learners relied heavily on ...\"\n" +
                "• Hiện tại: \"... whereas nowadays ...\"\n" +
                "• Kết luận ngắn\n\n" +
                "Từ vựng: accessible, democratise, self-paced, obsolete.",
            ResponseSeconds = 90
        });

        secSpeaking.SpeakingPrompts.Add(new SpeakingPrompt
        {
            Part = SpeakingPart.Part3Discussion,
            PromptOrder = 3,
            QuestionText = "Should employers pay for their staff to learn new skills? Why?",
            CandidatePromptText =
                "Nêu quan điểm rõ ràng + lợi ích cho cả hai phía:\n\n" +
                "• Quan điểm: \"I firmly believe that ...\"\n" +
                "• Lợi ích cho công ty: productivity, retention\n" +
                "• Lợi ích cho nhân viên: career growth\n\n" +
                "Từ vựng: invest in, upskill, mutual benefit, long-term payoff.",
            ResponseSeconds = 90
        });

        exam.Sections.Add(secListening);
        exam.Sections.Add(secReading);
        exam.Sections.Add(secWriting);
        exam.Sections.Add(secSpeaking);

        await context.Exams.AddAsync(exam);
        await context.SaveChangesAsync();

        // 3. Seed Sample Session with Proctoring Logs and Pending Review (for Teacher / Proctor Dashboard)
        var sampleSession = new ExamSession
        {
            UserId = student.Id,
            ExamId = exam.Id,
            StartTime = DateTime.UtcNow.AddMinutes(-40),
            Status = SessionStatus.InProgress
        };
        await context.ExamSessions.AddAsync(sampleSession);
        await context.SaveChangesAsync();

        var log1 = new ProctoringLog
        {
            SessionId = sampleSession.Id,
            Timestamp = DateTime.UtcNow.AddMinutes(-30),
            ViolationType = ViolationType.HotkeyBlocked,
            Description = "Hệ thống Kiosk đã chặn phím nóng [Alt + Tab (Chuyển cửa sổ)]"
        };
        var log2 = new ProctoringLog
        {
            SessionId = sampleSession.Id,
            Timestamp = DateTime.UtcNow.AddMinutes(-15),
            ViolationType = ViolationType.HotkeyBlocked,
            Description = "Hệ thống Kiosk đã chặn phím nóng [Windows Key (Start Menu)]"
        };
        await context.ProctoringLogs.AddRangeAsync(log1, log2);

        var writingSubmission = new CandidateSubmission
        {
            SessionId = sampleSession.Id,
            SkillType = SkillType.Writing,
            SubmittedAt = DateTime.UtcNow.AddMinutes(-5),
            EssayContent = "In contemporary society, artificial intelligence has emerged as a transformative force across numerous sectors, including education. While some commentators argue that automated systems will completely supplant human educators, it is my firm conviction that teachers play an indispensable, multifaceted role that technology can only augment rather than extinguish.\n\nFirst and foremost, human teachers provide emotional intelligence, empathetic mentoring, and moral guidance that algorithmic systems fundamentally lack. Furthermore, pedagogy is not merely the mechanical transmission of raw information; it requires profound understanding of diverse student temperaments and motivational psychology. Consequently, teachers serve as an essential catalyst for holistic human development.\n\nIn conclusion, although artificial intelligence offers unprecedented personalized learning materials, it will never replace the profound bond and moral inspiration provided by human instructors.",
            RawScore = 7.0,
            BandScore = 7.0
        };

        var evalReport = new EvaluationReport
        {
            Submission = writingSubmission,
            EvaluatedBy = EvaluatorType.AiEngine,
            CriterionScore1 = 7.5,
            CriterionScore2 = 7.0,
            CriterionScore3 = 7.0,
            CriterionScore4 = 6.5,
            FeedbackComments = "Bài viết có bố cục 4 đoạn rõ ràng, lập luận đúng trọng tâm. Sử dụng vốn từ học thuật phong phú: 'transformative force', 'indispensable', 'multifaceted', 'empathetic mentoring'. Khuyến nghị giáo viên xem xét nâng điểm Lexical Resource lên 7.5.",
            DetailedJsonAnalysis = JsonSerializer.Serialize(new
            {
                Highlights = new[] { "transformative force", "indispensable", "multifaceted", "empathetic mentoring" },
                ModelAnswerBand8 = "Undeniably, artificial intelligence has spearheaded a profound pedagogical revolution across global educational ecosystems..."
            }),
            IsApproved = false // Chờ Giám khảo phê duyệt Human-in-the-loop
        };
        writingSubmission.EvaluationReports.Add(evalReport);

        await context.CandidateSubmissions.AddAsync(writingSubmission);
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Thêm kịch bản Speaking cho database đã tồn tại (tính năng bổ sung sau khi seed lần đầu).
    /// Chỉ chạy khi bảng SpeakingPrompts đang rỗng và đã có đề + phần thi Speaking.
    /// </summary>
    private static async Task SeedSpeakingPromptsIfMissingAsync(SmartIeltsDbContext context)
    {
        try
        {
            if (await context.SpeakingPrompts.AnyAsync()) return;

            var speakingSection = await context.ExamSections
                .FirstOrDefaultAsync(s => s.SkillType == SkillType.Speaking);

            if (speakingSection == null) return;

            // QUAN TRỌNG: thêm trực tiếp vào DbSet, KHÔNG thêm qua navigation của entity đã tracked.
            // Entity có Id = Guid.NewGuid() sẵn nên nếu add qua navigation EF sẽ coi là "đã tồn tại"
            // và sinh UPDATE vào row không có -> 0 row affected.
            var prompts = BuildDefaultSpeakingPrompts(speakingSection.Id);
            await context.SpeakingPrompts.AddRangeAsync(prompts);
            await context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Không chặn khởi động nếu bảng chưa được tạo (schema guard sẽ xử lý).
            Console.WriteLine($"[SmartIELTS] Không seed được SpeakingPrompts: {ex.Message}");
        }
    }

    /// <summary>Bộ kịch bản Speaking mặc định (3 Part, có text gợi ý cho thí sinh đọc).</summary>
    private static List<SpeakingPrompt> BuildDefaultSpeakingPrompts(Guid sectionId) => new()
    {
        new SpeakingPrompt
        {
            SectionId = sectionId,
            Part = SpeakingPart.Part1Introduction,
            PromptOrder = 1,
            QuestionText = "Let's talk about where you live. Do you live in a house or an apartment?",
            CandidatePromptText =
                "Hãy đọc và trả lời bằng tiếng Anh (2-3 câu):\n\n" +
                "• Bạn sống ở nhà riêng hay căn hộ?\n" +
                "• Nêu 1 lý do bạn thích / không thích.\n\n" +
                "Mẫu: \"I live in a small apartment in the city centre, mainly because it's convenient for commuting.\"\n" +
                "Từ vựng gợi ý: spacious, cosy, neighbourhood, commute, furnished.",
            ResponseSeconds = 40
        },
        new SpeakingPrompt
        {
            SectionId = sectionId,
            Part = SpeakingPart.Part1Introduction,
            PromptOrder = 2,
            QuestionText = "What do you usually do in your free time?",
            CandidatePromptText =
                "Hãy đọc và trả lời (2-3 câu):\n\n" +
                "• 2 hoạt động bạn thường làm khi rảnh\n" +
                "• Vì sao bạn thích chúng?\n\n" +
                "Mẫu: \"In my free time, I usually read and go jogging, which helps me unwind after work.\"\n" +
                "Từ vựng gợi ý: unwind, take up, recreational, relieve stress.",
            ResponseSeconds = 40
        },
        new SpeakingPrompt
        {
            SectionId = sectionId,
            Part = SpeakingPart.Part1Introduction,
            PromptOrder = 3,
            QuestionText = "Do you prefer studying alone or with other people? Why?",
            CandidatePromptText =
                "Hãy đọc và trả lời (2-3 câu):\n\n" +
                "• Chọn 1 hình thức\n" +
                "• So sánh ngắn với hình thức còn lại\n\n" +
                "Mẫu: \"I lean towards studying alone because I can focus better, although group study helps when I get stuck.\"\n" +
                "Từ vựng gợi ý: concentrate, distract, collaborative, discipline.",
            ResponseSeconds = 40
        },
        new SpeakingPrompt
        {
            SectionId = sectionId,
            Part = SpeakingPart.Part2LongTurn,
            PromptOrder = 1,
            QuestionText =
                "Now, I'd like you to describe a skill you would like to learn in the future.\n" +
                "You should say: what the skill is; why you want to learn it; how you would learn it; and explain how this skill would benefit you.",
            CandidatePromptText =
                "CUE CARD — Đọc kỹ và chuẩn bị 1 phút, sau đó nói liên tục 1.5-2 phút.\n\n" +
                "Describe a skill you would like to learn in the future.\n" +
                "You should say:\n" +
                "  • what the skill is\n" +
                "  • why you want to learn it\n" +
                "  • how you would learn it\n" +
                "  • and explain how this skill would benefit you\n\n" +
                "Dàn ý gợi ý:\n" +
                "1) Mở bài: \"The skill I'd most like to master is ...\"\n" +
                "2) Lý do: career / personal interest\n" +
                "3) Cách học: online course, mentor, daily practice\n" +
                "4) Lợi ích: \"This would allow me to ... which is invaluable because ...\"\n" +
                "Từ vựng: master, proficiency, hands-on, self-taught, perseverance, rewarding.",
            PreparationSeconds = 60,
            ResponseSeconds = 120
        },
        new SpeakingPrompt
        {
            SectionId = sectionId,
            Part = SpeakingPart.Part3Discussion,
            PromptOrder = 1,
            QuestionText = "Why do you think some people are more willing to learn new skills than others?",
            CandidatePromptText =
                "PART 3 — Thảo luận mở rộng. Trả lời 2-3 ý kèm ví dụ (60-100 từ).\n\n" +
                "Gợi ý triển khai:\n" +
                "• Yếu tố 1: tính cách (mindset, resilience)\n" +
                "• Yếu tố 2: môi trường (supportive environment, exposure)\n" +
                "• Ví dụ cụ thể\n\n" +
                "Từ vựng: intrinsic motivation, perseverance, role model.",
            ResponseSeconds = 90
        },
        new SpeakingPrompt
        {
            SectionId = sectionId,
            Part = SpeakingPart.Part3Discussion,
            PromptOrder = 2,
            QuestionText = "How has technology changed the way people acquire new skills compared to the past?",
            CandidatePromptText =
                "Trả lời theo cấu trúc so sánh quá khứ / hiện tại:\n\n" +
                "• Quá khứ: \"In the past, learners relied heavily on ...\"\n" +
                "• Hiện tại: \"... whereas nowadays ...\"\n" +
                "• Kết luận ngắn\n\n" +
                "Từ vựng: accessible, democratise, self-paced, obsolete.",
            ResponseSeconds = 90
        },
        new SpeakingPrompt
        {
            SectionId = sectionId,
            Part = SpeakingPart.Part3Discussion,
            PromptOrder = 3,
            QuestionText = "Should employers pay for their staff to learn new skills? Why?",
            CandidatePromptText =
                "Nêu quan điểm rõ ràng + lợi ích cho cả hai phía:\n\n" +
                "• Quan điểm: \"I firmly believe that ...\"\n" +
                "• Lợi ích cho công ty: productivity, retention\n" +
                "• Lợi ích cho nhân viên: career growth\n\n" +
                "Từ vựng: invest in, upskill, mutual benefit, long-term payoff.",
            ResponseSeconds = 90
        }
    };
}