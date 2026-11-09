using System.Text;
using System.Text.RegularExpressions;
using SmartIELTS.Application.Interfaces;
using SmartIELTS.Domain.Enums;

namespace SmartIELTS.Infrastructure.Adapters;

/// <summary>
/// Bộ chấm mô phỏng (rule-based) đặt sau Adapter Pattern.
/// CHƯA gọi dịch vụ AI/LLM bên ngoài: điểm số được suy ra từ heuristic
/// (độ dài bài, liên từ, danh sách từ vựng học thuật, mẫu ngữ pháp).
/// Muốn dùng AI thật, chỉ cần thêm một implementation khác của IAiServiceAdapter
/// và đổi đăng ký DI - phần còn lại của hệ thống không phải sửa.
///
/// Phần Speaking được thiết kế như một **giám khảo ảo có hội thoại nhiều lượt**:
/// mỗi câu trả lời được chấm riêng, rồi hệ thống chọn câu hỏi tiếp theo dựa trên
/// nội dung vừa nói (adaptive follow-up) thay vì đọc một kịch bản cố định.
/// </summary>
public class IntelligentAiServiceAdapter : IAiServiceAdapter
{
    private static readonly string[] AdvancedVocab = new[]
    {
        "unprecedented", "exponential", "paradigm", "ubiquitous", "imperative",
        "detrimental", "profound", "catalyst", "mitigate", "counterpart",
        "substantiate", "disseminate", "foster", "culminate", "predominantly"
    };

    private static readonly string[] TransitionMarkers = new[]
    {
        "furthermore", "moreover", "in contrast", "consequently", "nevertheless",
        "on the contrary", "to illustrate", "in conclusion", "subsequently", "first and foremost"
    };

    // Từ vựng Speaking thường gặp (mức B1-C1) dùng để chấm Lexical Resource.
    private static readonly string[] SpeakingVocab = new[]
    {
        "personally", "nowadays", "significant", "beneficial", "challenging",
        "opportunity", "convenient", "essential", "flexible", "efficient",
        "sustainable", "inevitable", "considerable", "straightforward", "rewarding",
        "consequently", "nevertheless", "furthermore", "whereas", "although"
    };

    // Cụm nối ý dùng cho Fluency & Coherence.
    private static readonly string[] FluencyMarkers = new[]
    {
        "for instance", "for example", "in my opinion", "i reckon", "from my perspective",
        "on the other hand", "in addition", "as a result", "that being said",
        "to be honest", "in terms of", "when it comes to", "having said that"
    };

    // Lỗi ngữ pháp phổ biến của người Việt học IELTS Speaking.
    private static readonly (string Pattern, string Suggestion, string Explanation)[] SpeakingGrammarChecks =
    {
        (@"\b(he|she|it)\s+(have|do|go)\b",
         "Chia động từ ngôi thứ 3 số ít: has / does / goes",
         "Lỗi hòa hợp chủ vị (subject-verb agreement) làm giảm điểm Grammatical Accuracy."),
        (@"\bpeople\s+is\b",
         "people are",
         "'People' là danh từ số nhiều nên dùng 'are'."),
        (@"\bi\s+(go|like|think)\s+\w+ed\b",
         "Kiểm tra thì của động từ: I went / I liked",
         "Sai thì (tense) là lỗi thường gặp khi nói về quá khứ."),
        (@"\bdiscuss\s+about\b",
         "discuss (bỏ 'about')",
         "'Discuss' là ngoại động từ, không cần giới từ 'about'."),
        (@"\binformations\b",
         "information (không đếm được, không có 's')",
         "'Information' là danh từ không đếm được."),
        (@"\bvery\s+much\s+like\b",
         "really like / enjoy",
         "Tránh cụm từ không tự nhiên trong văn nói học thuật.")
    };

    // ---------------------------------------------------------------------
    // WRITING
    // ---------------------------------------------------------------------

    public Task<AiWritingEvaluationResult> EvaluateWritingAsync(string topic, string essayText)
    {
        essayText = essayText?.Trim() ?? string.Empty;
        var words = essayText.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        int wordCount = words.Length;

        // 1. Task Response (TR)
        double trScore = 6.0;
        if (wordCount >= 250) trScore += 1.0;
        else if (wordCount < 150) trScore -= 1.5;
        else if (wordCount < 200) trScore -= 0.5;

        if (essayText.Contains("opinion", StringComparison.OrdinalIgnoreCase) ||
            essayText.Contains("believe", StringComparison.OrdinalIgnoreCase) ||
            essayText.Contains("view", StringComparison.OrdinalIgnoreCase))
        {
            trScore = Math.Min(9.0, trScore + 0.5);
        }

        // 2. Coherence and Cohesion (CC)
        double ccScore = 6.0;
        int transitionCount = TransitionMarkers.Count(m => essayText.Contains(m, StringComparison.OrdinalIgnoreCase));
        if (transitionCount >= 3) ccScore += 1.0;
        if (transitionCount >= 5) ccScore += 0.5;
        var paragraphs = essayText.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        if (paragraphs.Length >= 4) ccScore += 0.5;

        // 3. Lexical Resource (LR)
        double lrScore = 6.0;
        var foundAdvanced = AdvancedVocab.Where(v => essayText.Contains(v, StringComparison.OrdinalIgnoreCase)).ToList();
        if (foundAdvanced.Count >= 2) lrScore += 1.0;
        if (foundAdvanced.Count >= 4) lrScore += 0.5;

        // 4. Grammatical Range and Accuracy (GRA)
        double graScore = 6.5;
        if (essayText.Contains("which", StringComparison.OrdinalIgnoreCase) ||
            essayText.Contains("although", StringComparison.OrdinalIgnoreCase) ||
            essayText.Contains("whereas", StringComparison.OrdinalIgnoreCase))
        {
            graScore += 0.5;
        }

        // Round to nearest 0.5
        trScore = Math.Clamp(RoundBand(trScore), 4.0, 9.0);
        ccScore = Math.Clamp(RoundBand(ccScore), 4.0, 9.0);
        lrScore = Math.Clamp(RoundBand(lrScore), 4.0, 9.0);
        graScore = Math.Clamp(RoundBand(graScore), 4.0, 9.0);

        double overall = RoundBand((trScore + ccScore + lrScore + graScore) / 4.0);

        // Highlight tags: Red (Grammar), Yellow (Collocation), Green (Academic highlight)
        var highlights = new List<TextHighlightError>();

        // Green highlights
        foreach (var av in foundAdvanced.Take(3))
        {
            highlights.Add(new TextHighlightError
            {
                ErrorType = "Academic Highlight",
                OriginalText = av,
                Suggestion = "Xuất sắc! Từ vựng học thuật C1/C2 nâng cao",
                Explanation = "Sử dụng từ vựng chuẩn xác mang lại điểm cộng lớn cho tiêu chí Lexical Resource.",
                SeverityColor = "Green"
            });
        }

        // Yellow warning
        if (essayText.Contains("very good", StringComparison.OrdinalIgnoreCase))
        {
            highlights.Add(new TextHighlightError
            {
                ErrorType = "Collocation & Style",
                OriginalText = "very good",
                Suggestion = "exceptional / beneficial / commendable",
                Explanation = "Hạn chế dùng tính từ cơ bản 'very good' trong văn phong học thuật IELTS Writing.",
                SeverityColor = "Yellow"
            });
        }

        // Red grammar check
        if (Regex.IsMatch(essayText, @"\b(he|she|it)\s+(have|do|go)\b", RegexOptions.IgnoreCase))
        {
            var match = Regex.Match(essayText, @"\b(he|she|it)\s+(have|do|go)\b", RegexOptions.IgnoreCase);
            highlights.Add(new TextHighlightError
            {
                ErrorType = "Subject-Verb Agreement",
                OriginalText = match.Value,
                Suggestion = "Chia động từ tương ứng ở ngôi thứ 3 số ít (has/does/goes)",
                Explanation = "Lỗi ngữ pháp cơ bản về hòa hợp chủ vị làm giảm điểm Grammatical Accuracy.",
                SeverityColor = "Red"
            });
        }

        string modelAnswer = $"[Band 8.0+ Model Rewrite]\n\n" +
            $"In contemporary society, the proposition regarding \"{topic}\" has sparked intense debate among academics and policymakers. " +
            $"While certain skeptics assert that conventional approaches retain their paramount efficacy, it is my firm conviction that modern innovations provide an indispensable paradigm shift.\n\n" +
            $"First and foremost, empirical evidence substantiates that technological integration fosters unprecedented productivity. " +
            $"Furthermore, the profound implications of cognitive automation serve as a catalyst for holistic educational reforms. " +
            $"Consequently, stakeholders must calibrate strategic frameworks to mitigate collateral discrepancies.\n\n" +
            $"In conclusion, notwithstanding viable counterarguments, the multifaceted advantages indisputably prevail, paving the way for sustainable development.";

        string feedback = $"Bài viết đạt {wordCount} từ. Đạt Band {overall} theo thang quy đổi mô phỏng (rule-based, chưa gọi dịch vụ AI bên ngoài). Điểm mạnh: Cấu trúc lập luận mạch lạc, có sử dụng liên từ. Cần cải thiện: Mở rộng thêm cấu trúc câu ghép phức và đa dạng hóa vốn từ vựng học thuật C1/C2.";

        var result = new AiWritingEvaluationResult
        {
            TaskResponse = trScore,
            CoherenceCohesion = ccScore,
            LexicalResource = lrScore,
            GrammaticalRangeAccuracy = graScore,
            OverallBand = overall,
            Feedback = feedback,
            Highlights = highlights,
            ModelAnswerBand8 = modelAnswer
        };

        return Task.FromResult(result);
    }

    // ---------------------------------------------------------------------
    // SPEAKING - chấm toàn bài (API một lần)
    // ---------------------------------------------------------------------

    public Task<AiSpeakingEvaluationResult> EvaluateSpeakingAsync(string topic, string transcriptText, string? audioUrl)
    {
        transcriptText = transcriptText?.Trim() ?? string.Empty;
        var (fc, pr, lr, gra) = ScoreSpeakingText(transcriptText);

        double overall = RoundBand((fc + pr + lr + gra) / 4.0);
        int wordCount = CountWords(transcriptText);

        var result = new AiSpeakingEvaluationResult
        {
            FluencyCoherence = fc,
            Pronunciation = pr,
            LexicalResource = lr,
            GrammaticalRangeAccuracy = gra,
            OverallBand = overall,
            TurnBand = overall,
            WordCount = wordCount,
            Feedback = $"Độ trôi chảy tương đối tự nhiên (Fluency: {fc}, Pronunciation: {pr} - ước lượng rule-based từ độ dài transcript, chưa phân tích âm thanh thật). Hệ thống đã phát hiện luận điểm và sinh câu hỏi phản biện thích ứng ở Part 3.",
            PhonemeIssues = DefaultPhonemeIssues(),
            AdaptiveFollowUpQuestion = DefaultFollowUp(),
            Highlights = DetectGrammarIssues(transcriptText),
            LengthAssessment = AssessLength(wordCount, 60),
            ModelAnswer = BuildModelAnswer("How has modern automation transformed the workplace?")
        };

        return Task.FromResult(result);
    }

    // ---------------------------------------------------------------------
    // SPEAKING - chấm từng lượt + sinh câu hỏi tiếp theo (giám khảo ảo)
    // ---------------------------------------------------------------------

    public Task<AiSpeakingEvaluationResult> EvaluateSpeakingTurnAsync(
        SpeakingSessionState state,
        string answer,
        string? audioUrl,
        CancellationToken ct = default)
    {
        answer = answer?.Trim() ?? string.Empty;
        var (fc, pr, lr, gra) = ScoreSpeakingText(answer);
        double turnBand = RoundBand((fc + pr + lr + gra) / 4.0);
        int wordCount = CountWords(answer);

        var next = ChooseNextQuestion(state, answer);

        var result = new AiSpeakingEvaluationResult
        {
            FluencyCoherence = fc,
            Pronunciation = pr,
            LexicalResource = lr,
            GrammaticalRangeAccuracy = gra,
            OverallBand = turnBand,
            TurnBand = turnBand,
            WordCount = wordCount,
            LengthAssessment = AssessLength(wordCount, MinimumWordsForPart(state.CurrentPart)),
            TurnFeedback = BuildTurnFeedback(wordCount, lr, gra, state.CurrentPart),
            PhonemeIssues = DefaultPhonemeIssues(),
            Highlights = DetectGrammarIssues(answer),
            ModelAnswer = BuildModelAnswer(state.CurrentQuestion),
            NextQuestion = next?.Question,
            NextQuestionHints = next?.Hints,
            AdaptiveFollowUpQuestion = next?.Question ?? string.Empty
        };

        return Task.FromResult(result);
    }

    private record NextPrompt(string? Question, string? Hints, SpeakingPart Part);

    /// <summary>
    /// Chọn câu hỏi tiếp theo. Ưu tiên câu hỏi "bám" vào nội dung thí sinh vừa nói
    /// (adaptive follow-up) để tạo cảm giác hội thoại thật, sau đó mới tới câu hỏi kế tiếp theo kịch bản.
    /// </summary>
    private static NextPrompt? ChooseNextQuestion(SpeakingSessionState state, string answer)
    {
        string lower = answer.ToLowerInvariant();
        int turn = state.TurnCount; // số lượt đã hoàn thành (chưa tính lượt hiện tại)

        // ---- Part 1: 3 câu hỏi khởi động, sau đó chuyển Part 2 ----
        if (state.CurrentPart == SpeakingPart.Part1Introduction)
        {
            var part1 = Part1Questions;
            if (turn + 1 < part1.Length)
            {
                return new NextPrompt(part1[turn + 1].Q, part1[turn + 1].Hints, SpeakingPart.Part1Introduction);
            }

            return new NextPrompt(Part2CueCard.Question, Part2CueCard.Hints, SpeakingPart.Part2LongTurn);
        }

        // ---- Part 2: long turn, sau đó chuyển Part 3 ----
        if (state.CurrentPart == SpeakingPart.Part2LongTurn)
        {
            return new NextPrompt(Part3Questions[0].Q, Part3Questions[0].Hints, SpeakingPart.Part3Discussion);
        }

        // ---- Part 3: tối đa 3 câu, trong đó câu thứ 2 bám theo nội dung trả lời ----
        if (state.CurrentPart == SpeakingPart.Part3Discussion)
        {
            // Lượt hiện tại CHƯA nằm trong state.Turns (client gửi các lượt trước đó),
            // nên số câu Part 3 đã trả lời = số lượt Part 3 trước đó + 1.
            int answeredPart3 = state.Turns.Count(t => t.Part == SpeakingPart.Part3Discussion) + 1;

            if (answeredPart3 >= 3) return null; // kết thúc buổi vấn đáp

            // Câu hỏi phản biện động dựa trên nội dung thí sinh vừa nói.
            if (answeredPart3 == 1)
            {
                return new NextPrompt(BuildAdaptiveChallenge(lower), AdaptiveHints, SpeakingPart.Part3Discussion);
            }

            var scripted = Part3Questions[Math.Min(answeredPart3 - 1, Part3Questions.Length - 1)];
            return new NextPrompt(scripted.Q, scripted.Hints, SpeakingPart.Part3Discussion);
        }

        return null;
    }

    /// <summary>
    /// Sinh câu hỏi phản biện dựa trên chủ đề thí sinh vừa nhắc tới.
    /// Đây là phần "adaptive" thật sự: cùng một câu hỏi gốc nhưng câu hỏi tiếp theo thay đổi theo nội dung.
    /// </summary>
    private static string BuildAdaptiveChallenge(string lowerAnswer)
    {
        if (ContainsAny(lowerAnswer, "student", "school", "education", "teacher", "learn"))
            return "You mentioned education. Do you think students from rural areas, who may lack reliable internet access, would benefit equally from the technology you described? How should governments respond?";

        if (ContainsAny(lowerAnswer, "job", "work", "employ", "career", "office", "automation"))
            return "You brought up the workplace. If automation eliminates certain entry-level jobs, what responsibility do employers and governments have towards the workers affected?";

        if (ContainsAny(lowerAnswer, "health", "medical", "doctor", "hospital", "patient"))
            return "You referred to healthcare. How would you weigh the efficiency gains of automated diagnosis against the risk of losing human judgement in critical decisions?";

        if (ContainsAny(lowerAnswer, "environment", "climate", "pollution", "energy", "green"))
            return "You mentioned environmental concerns. In your view, should governments prioritise economic growth or environmental protection when the two conflict?";

        if (ContainsAny(lowerAnswer, "family", "children", "parent", "child"))
            return "You spoke about family life. How do you think the role of parents will change as children spend more time interacting with digital devices?";

        if (ContainsAny(lowerAnswer, "money", "cost", "expensive", "afford", "economy", "income"))
            return "You raised an economic point. Do you believe the benefits of this trend are distributed fairly across different income groups? Why or why not?";

        return "That is an interesting perspective. Could you think of a specific example that illustrates your point, and explain why that example is significant?";
    }

    private static bool ContainsAny(string text, params string[] keywords)
        => keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));

    private const string AdaptiveHints =
        "Gợi ý trả lời:\n" +
        "• Mở đầu: \"That's a fair point. In my view, ...\"\n" +
        "• Nêu 1 lý do chính + 1 ví dụ cụ thể (For instance, ...)\n" +
        "• Cân nhắc mặt trái: \"Admittedly, ... However, ...\"\n" +
        "• Kết: \"On balance, I would argue that ...\"\n" +
        "Từ vựng gợi ý: equitable, accessible, infrastructure, prioritise, trade-off, long-term.";

    private static readonly (string Q, string Hints)[] Part1Questions =
    {
        ("Let's talk about where you live. Do you live in a house or an apartment?",
         "Gợi ý: nêu loại nhà + 1 lý do bạn thích.\nVí dụ: \"I live in a small apartment in the city centre, mainly because it's convenient for commuting.\"\nTừ vựng: spacious, cosy, neighbourhood, commute, furnished."),

        ("What do you usually do in your free time?",
         "Gợi ý: 2 hoạt động + lý do.\nVí dụ: \"In my free time, I usually read and go jogging, which helps me unwind after work.\"\nTừ vựng: unwind, unwind after a long day, take up, recreational."),

        ("Do you prefer studying alone or with other people? Why?",
         "Gợi ý: chọn 1 + so sánh ngắn.\nVí dụ: \"I lean towards studying alone because I can focus better, although group study helps when I get stuck.\"\nTừ vựng: concentrate, distract, collaborative, discipline.")
    };

    private static readonly (string Question, string Hints) Part2CueCard = (
        "Now, I'd like you to describe a skill you would like to learn in the future.\n" +
        "You should say: what the skill is; why you want to learn it; how you would learn it; and explain how this skill would benefit you.\n" +
        "You have one minute to prepare, then speak for up to two minutes.",
        "Gợi ý dàn ý (đọc và triển khai):\n" +
        "1) Mở bài: \"The skill I'd most like to master is ...\"\n" +
        "2) Lý do: career / personal interest\n" +
        "3) Cách học: online course, mentor, daily practice\n" +
        "4) Lợi ích: \"This would allow me to ... which is invaluable because ...\"\n" +
        "Từ vựng: master, proficiency, hands-on, self-taught, perseverance, rewarding.");

    private static readonly (string Q, string Hints)[] Part3Questions =
    {
        ("Why do you think some people are more willing to learn new skills than others?",
         "Gợi ý: nêu 2 yếu tố (tính cách + môi trường) + ví dụ.\nTừ vựng: mindset, resilience, exposure, supportive environment, intrinsic motivation."),

        ("How has technology changed the way people acquire new skills compared to the past?",
         "Gợi ý: so sánh quá khứ / hiện tại.\nVí dụ: \"In the past, learners relied heavily on ... whereas nowadays ...\"\nTừ vựng: accessible, democratise, self-paced, obsolete."),

        ("Should employers pay for their staff to learn new skills? Why?",
         "Gợi ý: nêu quan điểm + lợi ích cho cả hai phía.\nTừ vựng: invest in, retention, productivity, mutual benefit, upskill.")
    };

    // ---------------------------------------------------------------------
    // Chấm điểm Speaking dùng chung
    // ---------------------------------------------------------------------

    /// <summary>Chấm 4 tiêu chí từ transcript: FC, PR, LR, GRA.</summary>
    private static (double Fc, double Pr, double Lr, double Gra) ScoreSpeakingText(string text)
    {
        int wordCount = CountWords(text);

        // --- Fluency & Coherence: độ dài + mức độ dùng cụm nối ý ---
        double fc = 5.5;
        if (wordCount >= 40) fc += 0.5;
        if (wordCount >= 80) fc += 0.5;
        if (wordCount >= 130) fc += 0.5;

        int fluencyMarkers = FluencyMarkers.Count(m => text.Contains(m, StringComparison.OrdinalIgnoreCase));
        if (fluencyMarkers >= 1) fc += 0.5;
        if (fluencyMarkers >= 3) fc += 0.5;
        if (fluencyMarkers >= 5) fc += 0.5;

        // Câu quá ngắn hoặc không có nội dung -> trừ điểm.
        if (wordCount < 15) fc -= 0.5;
        if (wordCount < 5) fc -= 1.0;

        // --- Pronunciation: ước lượng từ độ rõ ràng của transcript và độ dài ---
        // (chưa phân tích waveform nên chỉ là ước lượng; nêu rõ trong feedback)
        double pr = 6.0;
        if (wordCount >= 40) pr += 0.5;
        if (wordCount >= 100) pr += 0.5;
        if (HasOnlyAsciiWords(text)) pr += 0.0; // transcript sạch, không lẫn ký tự lạ
        if (wordCount < 5) pr -= 1.0;

        // --- Lexical Resource: từ vựng đa dạng + từ học thuật ---
        double lr = 5.5;
        var distinctRatio = DistinctWordRatio(text);
        if (distinctRatio >= 0.55) lr += 0.5;
        if (distinctRatio >= 0.70) lr += 0.5;

        int vocabHits = SpeakingVocab.Count(v => text.Contains(v, StringComparison.OrdinalIgnoreCase))
                      + AdvancedVocab.Count(v => text.Contains(v, StringComparison.OrdinalIgnoreCase));
        if (vocabHits >= 2) lr += 0.5;
        if (vocabHits >= 4) lr += 0.5;
        if (wordCount < 10) lr -= 0.5;

        // --- Grammatical Range & Accuracy: cấu trúc phức + lỗi phát hiện được ---
        double gra = 6.0;
        int complexStructures = 0;
        if (Regex.IsMatch(text, @"\b(which|that|who)\b", RegexOptions.IgnoreCase)) complexStructures++;
        if (Regex.IsMatch(text, @"\b(although|though|whereas|while)\b", RegexOptions.IgnoreCase)) complexStructures++;
        if (Regex.IsMatch(text, @"\b(if|unless|provided that)\b", RegexOptions.IgnoreCase)) complexStructures++;
        if (complexStructures >= 1) gra += 0.5;
        if (complexStructures >= 2) gra += 0.5;

        int grammarErrors = SpeakingGrammarChecks.Count(c => Regex.IsMatch(text, c.Pattern, RegexOptions.IgnoreCase));
        gra -= 0.5 * grammarErrors;

        return (
            Math.Clamp(RoundBand(fc), 3.0, 9.0),
            Math.Clamp(RoundBand(pr), 3.0, 9.0),
            Math.Clamp(RoundBand(lr), 3.0, 9.0),
            Math.Clamp(RoundBand(gra), 3.0, 9.0)
        );
    }

    private static List<TextHighlightError> DetectGrammarIssues(string text)
    {
        var issues = new List<TextHighlightError>();

        foreach (var (pattern, suggestion, explanation) in SpeakingGrammarChecks)
        {
            var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
            if (!match.Success) continue;

            issues.Add(new TextHighlightError
            {
                ErrorType = "Grammar",
                OriginalText = match.Value,
                Suggestion = suggestion,
                Explanation = explanation,
                SeverityColor = "Red"
            });
        }

        // Cảnh báo dùng từ quá đơn giản (màu vàng).
        if (Regex.IsMatch(text, @"\b(very good|very bad|nice|a lot of things)\b", RegexOptions.IgnoreCase))
        {
            var match = Regex.Match(text, @"\b(very good|very bad|nice|a lot of things)\b", RegexOptions.IgnoreCase);
            issues.Add(new TextHighlightError
            {
                ErrorType = "Vocabulary",
                OriginalText = match.Value,
                Suggestion = "exceptional / beneficial / remarkable / numerous aspects",
                Explanation = "Nâng cấp từ vựng sẽ cải thiện tiêu chí Lexical Resource.",
                SeverityColor = "Yellow"
            });
        }

        // Điểm cộng: dùng từ nối học thuật (màu xanh).
        foreach (var marker in FluencyMarkers.Where(m => text.Contains(m, StringComparison.OrdinalIgnoreCase)).Take(2))
        {
            issues.Add(new TextHighlightError
            {
                ErrorType = "Coherence Highlight",
                OriginalText = marker,
                Suggestion = "Tốt! Dùng cụm nối ý tự nhiên",
                Explanation = "Giúp câu trả lời mạch lạc, tăng điểm Fluency & Coherence.",
                SeverityColor = "Green"
            });
        }

        return issues;
    }

    private static string BuildTurnFeedback(int wordCount, double lexical, double grammar, SpeakingPart part)
    {
        var sb = new StringBuilder();

        sb.Append(wordCount switch
        {
            < 10 => $"Câu trả lời rất ngắn ({wordCount} từ). ",
            < 25 => $"Câu trả lời khá ngắn ({wordCount} từ). ",
            < 50 => $"Độ dài ổn ({wordCount} từ). ",
            _ => $"Câu trả lời dài và chi tiết ({wordCount} từ). "
        });

        if (part == SpeakingPart.Part2LongTurn && wordCount < 100)
            sb.Append("Với Part 2, bạn nên nói liên tục 1.5–2 phút (khoảng 180–220 từ). ");
        else if (part == SpeakingPart.Part3Discussion && wordCount < 50)
            sb.Append("Với Part 3, nên triển khai 2–3 ý với ví dụ (khoảng 60–100 từ). ");
        else if (part == SpeakingPart.Part1Introduction && wordCount < 25)
            sb.Append("Với Part 1, câu trả lời 2–3 câu là phù hợp. ");

        if (lexical < 6.0)
            sb.Append("Vốn từ còn cơ bản — hãy dùng thêm từ học thuật và cụm diễn đạt thay thế. ");
        if (grammar < 6.0)
            sb.Append("Chú ý chia động từ và thì khi nói; thử dùng thêm câu phức (which / although). ");

        if (sb.Length == 0) sb.Append("Phản hồi tốt, tiếp tục phát huy.");

        return sb.ToString().Trim();
    }

    private static int MinimumWordsForPart(SpeakingPart part) => part switch
    {
        SpeakingPart.Part1Introduction => 25,
        SpeakingPart.Part2LongTurn => 120,
        SpeakingPart.Part3Discussion => 60,
        _ => 40
    };

    private static string AssessLength(int wordCount, int expected) => wordCount switch
    {
        var w when w < expected / 2 => "Quá ngắn so với yêu cầu",
        var w when w < expected => "Hơi ngắn, nên phát triển thêm ý",
        var w when w <= expected * 2 => "Độ dài phù hợp",
        _ => "Rất chi tiết (tốt cho Part 2)"
    };

    private static string BuildModelAnswer(string question)
    {
        string topic = question.Trim().TrimEnd('?', '.');
        return $"[Câu trả lời mẫu Band 8.0+ — tham khảo]\n\n" +
               $"Well, if I think about {LowerFirst(topic)}, I would say there are a couple of angles worth considering. " +
               $"From my perspective, the most significant factor is the long-term impact rather than the immediate convenience. " +
               $"For instance, in my own experience, adopting a new habit gradually produced far better results than trying to change everything at once. " +
               $"That being said, I appreciate that this depends heavily on individual circumstances. " +
               $"On balance, I would argue that a balanced approach is usually the most sustainable one.";
    }

    private static string LowerFirst(string text)
        => string.IsNullOrEmpty(text) ? text : char.ToLowerInvariant(text[0]) + text[1..];

    private static List<string> DefaultPhonemeIssues() => new()
    {
        "Âm /θ/ trong 'think' và 'theory' có xu hướng phát âm thành /s/ hoặc /t/.",
        "Cần chú ý nối âm (linking sounds) giữa phụ âm cuối và nguyên âm đầu trong cụm từ 'most of all'.",
        "Trọng âm từ: 'PHOtograph' nhưng 'phoTOgraphy' — chú ý dịch trọng âm theo hậu tố."
    };

    private static string DefaultFollowUp()
        => "You mentioned the benefits of modern technology in education; however, how would you address the growing concern regarding educational inequality for students in rural areas who lack digital infrastructure?";

    private static int CountWords(string text)
        => text.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;

    private static double DistinctWordRatio(string text)
    {
        var words = text.ToLowerInvariant()
            .Split(new[] { ' ', '\r', '\n', '\t', '.', ',', ';', ':', '!', '?' }, StringSplitOptions.RemoveEmptyEntries);

        if (words.Length < 5) return 0;

        return words.Distinct().Count() / (double)words.Length;
    }

    private static bool HasOnlyAsciiWords(string text)
        => text.All(c => c < 128);

    private static double RoundBand(double score)
    {
        double decimalPart = score - Math.Floor(score);
        if (decimalPart < 0.25) return Math.Floor(score);
        if (decimalPart < 0.75) return Math.Floor(score) + 0.5;
        return Math.Ceiling(score);
    }
}
