using SmartIELTS.Domain.Enums;

namespace SmartIELTS.Application.Speaking;

/// <summary>Một cửa ải trong buổi vấn đáp.</summary>
public class SpeakingStep
{
    public SpeakingPart Part { get; init; }
    public string Question { get; init; } = string.Empty;
    public string Hints { get; init; } = string.Empty;
}

/// <summary>
/// Kịch bản vấn đáp chuẩn IELTS: 3 câu Part 1 → 1 cue card Part 2 → 3 câu Part 3 = đúng 7 lượt.
/// <para>
/// QUAN TRỌNG: luồng hỏi do CODE quyết định, KHÔNG để AI quyết.
/// Trước đây AI tự sinh câu tiếp theo nên đếm sai số lượt (Part 3 thành 4-5 câu),
/// khiến thí sinh thấy "8 lượt rồi vẫn còn lượt 9".
/// AI chỉ chấm điểm, không điều khiển luồng.
/// </para>
/// </summary>
public static class SpeakingInterviewScript
{
    public const int TotalSteps = 7;

    private static readonly SpeakingStep[] Steps =
    {
        // ---------------- PART 1: 3 câu ----------------
        new()
        {
            Part = SpeakingPart.Part1Introduction,
            Question = "Let's talk about where you live. Do you live in a house or an apartment?",
            Hints = "Hãy đọc và trả lời bằng tiếng Anh (2-3 câu):\n\n" +
                    "• Bạn sống ở nhà riêng hay căn hộ?\n" +
                    "• Nêu 1 lý do bạn thích / không thích.\n\n" +
                    "Mẫu: \"I live in a small apartment in the city centre, mainly because it's convenient for commuting.\"\n" +
                    "Từ vựng gợi ý: spacious, cosy, neighbourhood, commute, furnished."
        },
        new()
        {
            Part = SpeakingPart.Part1Introduction,
            Question = "What do you usually do in your free time?",
            Hints = "Hãy đọc và trả lời (2-3 câu):\n\n" +
                    "• 2 hoạt động bạn thường làm khi rảnh\n" +
                    "• Vì sao bạn thích chúng?\n\n" +
                    "Mẫu: \"In my free time, I usually read and go jogging, which helps me unwind after work.\"\n" +
                    "Từ vựng gợi ý: unwind, take up, recreational, relieve stress."
        },
        new()
        {
            Part = SpeakingPart.Part1Introduction,
            Question = "Do you prefer studying alone or with other people? Why?",
            Hints = "Hãy đọc và trả lời (2-3 câu):\n\n" +
                    "• Chọn 1 hình thức\n" +
                    "• So sánh ngắn với hình thức còn lại\n\n" +
                    "Mẫu: \"I lean towards studying alone because I can focus better, although group study helps when I get stuck.\"\n" +
                    "Từ vựng gợi ý: concentrate, distract, collaborative, discipline."
        },

        // ---------------- PART 2: 1 cue card ----------------
        new()
        {
            Part = SpeakingPart.Part2LongTurn,
            Question = "Now, I'd like you to describe a skill you would like to learn in the future.\n" +
                       "You should say: what the skill is; why you want to learn it; how you would learn it; " +
                       "and explain how this skill would benefit you.",
            Hints = "CUE CARD — Đọc kỹ và chuẩn bị 1 phút, sau đó nói liên tục 1.5-2 phút.\n\n" +
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
                    "Từ vựng: master, proficiency, hands-on, self-taught, perseverance, rewarding."
        },

        // ---------------- PART 3: 3 câu ----------------
        new()
        {
            Part = SpeakingPart.Part3Discussion,
            Question = "Why do you think some people are more willing to learn new skills than others?",
            Hints = "PART 3 — Thảo luận mở rộng. Trả lời 2-3 ý kèm ví dụ (60-100 từ).\n\n" +
                    "Gợi ý triển khai:\n" +
                    "• Yếu tố 1: tính cách (mindset, resilience)\n" +
                    "• Yếu tố 2: môi trường (supportive environment, exposure)\n" +
                    "• Ví dụ cụ thể\n\n" +
                    "Từ vựng: intrinsic motivation, perseverance, role model."
        },
        new()
        {
            Part = SpeakingPart.Part3Discussion,
            Question = "How has technology changed the way people acquire new skills compared to the past?",
            Hints = "Trả lời theo cấu trúc so sánh quá khứ / hiện tại:\n\n" +
                    "• Quá khứ: \"In the past, learners relied heavily on ...\"\n" +
                    "• Hiện tại: \"... whereas nowadays ...\"\n" +
                    "• Kết luận ngắn\n\n" +
                    "Từ vựng: accessible, democratise, self-paced, obsolete."
        },
        new()
        {
            Part = SpeakingPart.Part3Discussion,
            Question = "Should employers pay for their staff to learn new skills? Why?",
            Hints = "Nêu quan điểm rõ ràng + lợi ích cho cả hai phía:\n\n" +
                    "• Quan điểm: \"I firmly believe that ...\"\n" +
                    "• Lợi ích cho công ty: productivity, retention\n" +
                    "• Lợi ích cho nhân viên: career growth\n\n" +
                    "Từ vựng: invest in, upskill, mutual benefit, long-term payoff."
        }
    };

    /// <summary>Lấy bước thứ <paramref name="index"/> (0-based). Null nếu đã hết.</summary>
    public static SpeakingStep? GetStep(int index)
        => index >= 0 && index < Steps.Length ? Steps[index] : null;

    /// <summary>Tổng thời gian trả lời ước tính (giây) theo từng Part.</summary>
    public static int ResponseSeconds(SpeakingPart part) => part switch
    {
        SpeakingPart.Part1Introduction => 40,
        SpeakingPart.Part2LongTurn => 120,
        SpeakingPart.Part3Discussion => 90,
        _ => 60
    };

    public static int PreparationSeconds(SpeakingPart part)
        => part == SpeakingPart.Part2LongTurn ? 60 : 0;
}
