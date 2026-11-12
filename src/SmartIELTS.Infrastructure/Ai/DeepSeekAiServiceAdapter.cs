using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SmartIELTS.Application.Interfaces;
using SmartIELTS.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace SmartIELTS.Infrastructure.Ai;

/// <summary>
/// Bộ chấm điểm dùng LLM thật qua API tương thích OpenAI (đã kiểm thử với DeepSeek).
/// <para>
/// Đặc điểm:
/// - Trả về JSON có cấu trúc (<c>response_format: json_object</c>) để parse an toàn.
/// - Chấm đúng 4 tiêu chí Cambridge, kèm nhận xét tiếng Việt và lỗi cụ thể có màu.
/// - Nếu gọi lỗi và <see cref="AiGradingOptions.FallbackToRuleBased"/> = true,
///   tự chuyển sang <see cref="Adapters.IntelligentAiServiceAdapter"/> để không làm hỏng bài thi.
/// </para>
/// </summary>
public class DeepSeekAiServiceAdapter : IAiServiceAdapter
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly AiGradingOptions _options;
    private readonly IAiServiceAdapter _fallback;
    private readonly ILogger<DeepSeekAiServiceAdapter>? _logger;

    public DeepSeekAiServiceAdapter(
        AiGradingOptions options,
        IAiServiceAdapter fallback,
        ILogger<DeepSeekAiServiceAdapter>? logger = null)
    {
        _options = options;
        _fallback = fallback;
        _logger = logger;

        _http = new HttpClient
        {
            BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/"),
            Timeout = TimeSpan.FromSeconds(Math.Max(10, options.TimeoutSeconds))
        };

        string key = options.ResolveApiKey();
        if (!string.IsNullOrWhiteSpace(key))
        {
            _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }
    }

    /// <summary>
    /// Số token tối đa cho phần trả lời.
    /// <para>
    /// LƯU Ý QUAN TRỌNG: model reasoning (DeepSeek v4.1, o1-style...) tiêu token vào
    /// <c>reasoning_content</c> TRƯỚC khi sinh <c>content</c>. Với bài chấm Writing đầy đủ,
    /// phần reasoning tốn ~2.300–3.000 token. Nếu đặt max_tokens thấp (1500), API trả
    /// HTTP 200 nhưng <c>content</c> rỗng và <c>finish_reason=length</c>.
    /// Đã kiểm chứng: 3000 -> rỗng, 6000 -> OK.
    /// </para>
    /// </summary>
    private const int WritingMaxTokens = 8000;
    private const int SpeakingMaxTokens = 6000;

    /// <summary>
    /// Nếu content rỗng nhưng vẫn còn token, thử gọi lại 1 lần với max_tokens cao hơn.
    /// </summary>
    private const int EmptyContentRetryMultiplier = 2;

    // =====================================================================
    // WRITING
    // =====================================================================

    public async Task<AiWritingEvaluationResult> EvaluateWritingAsync(string topic, string essayText)
    {
        essayText ??= string.Empty;
        int wordCount = CountWords(essayText);

        // Bài quá ngắn: không cần gọi AI, tiết kiệm thời gian và tránh kết quả vô nghĩa.
        if (wordCount < 20)
        {
            return await SafeFallbackWritingAsync(topic, essayText);
        }

        string prompt = BuildWritingPrompt(topic, essayText, wordCount);

        try
        {
            string json = await ChatAsync(prompt, WritingMaxTokens);
            var parsed = JsonSerializer.Deserialize<WritingAiResponse>(json, JsonOpts);

            if (parsed == null) return await SafeFallbackWritingAsync(topic, essayText);

            return new AiWritingEvaluationResult
            {
                TaskResponse = ClampBand(parsed.TaskResponse),
                CoherenceCohesion = ClampBand(parsed.CoherenceCohesion),
                LexicalResource = ClampBand(parsed.LexicalResource),
                GrammaticalRangeAccuracy = ClampBand(parsed.GrammaticalRangeAccuracy),
                OverallBand = ClampBand(parsed.OverallBand),
                Feedback = parsed.Feedback ?? "AI đã chấm bài.",
                ModelAnswerBand8 = parsed.ModelAnswer ?? string.Empty,
                Highlights = (parsed.Highlights ?? new List<WritingHighlight>())
                    .Select(h => new TextHighlightError
                    {
                        ErrorType = h.ErrorType ?? "Grammar",
                        OriginalText = h.OriginalText ?? string.Empty,
                        Suggestion = h.Suggestion ?? string.Empty,
                        Explanation = h.Explanation ?? string.Empty,
                        SeverityColor = NormalizeColor(h.Severity)
                    }).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[SmartIELTS] Chấm Writing bằng AI thất bại, dùng rule-based.");
            return await SafeFallbackWritingAsync(topic, essayText);
        }
    }

    // =====================================================================
    // SPEAKING (một lượt — giám khảo ảo)
    // =====================================================================

    public async Task<AiSpeakingEvaluationResult> EvaluateSpeakingTurnAsync(
        SpeakingSessionState state, string answer, string? audioUrl, CancellationToken ct = default)
    {
        answer ??= string.Empty;
        int wordCount = CountWords(answer);

        if (wordCount < 3)
        {
            return await SafeFallbackTurnAsync(state, answer, audioUrl, ct);
        }

        string prompt = BuildSpeakingTurnPrompt(state, answer, wordCount);

        try
        {
            string json = await ChatAsync(prompt, SpeakingMaxTokens, ct);
            var parsed = JsonSerializer.Deserialize<SpeakingAiResponse>(json, JsonOpts);

            if (parsed == null) return await SafeFallbackTurnAsync(state, answer, audioUrl, ct);

            return new AiSpeakingEvaluationResult
            {
                FluencyCoherence = ClampBand(parsed.FluencyCoherence),
                Pronunciation = ClampBand(parsed.Pronunciation),
                LexicalResource = ClampBand(parsed.LexicalResource),
                GrammaticalRangeAccuracy = ClampBand(parsed.GrammaticalRangeAccuracy),
                OverallBand = ClampBand(parsed.OverallBand),
                TurnBand = ClampBand(parsed.OverallBand),
                TurnFeedback = parsed.TurnFeedback ?? string.Empty,
                Feedback = parsed.TurnFeedback ?? string.Empty,
                WordCount = wordCount,
                LengthAssessment = parsed.LengthAssessment ?? string.Empty,
                ModelAnswer = parsed.ModelAnswer ?? string.Empty,
                NextQuestion = parsed.NextQuestion,
                NextQuestionHints = parsed.NextQuestionHints,
                AdaptiveFollowUpQuestion = parsed.NextQuestion ?? string.Empty,
                PhonemeIssues = parsed.PhonemeIssues ?? new List<string>(),
                Highlights = (parsed.Highlights ?? new List<WritingHighlight>())
                    .Select(h => new TextHighlightError
                    {
                        ErrorType = h.ErrorType ?? "Grammar",
                        OriginalText = h.OriginalText ?? string.Empty,
                        Suggestion = h.Suggestion ?? string.Empty,
                        Explanation = h.Explanation ?? string.Empty,
                        SeverityColor = NormalizeColor(h.Severity)
                    }).ToList()
            };
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[SmartIELTS] Chấm Speaking bằng AI thất bại, dùng rule-based.");
            return await SafeFallbackTurnAsync(state, answer, audioUrl, ct);
        }
    }

    // =====================================================================
    // SPEAKING (toàn bài — dùng khi nộp một lần)
    // =====================================================================

    public async Task<AiSpeakingEvaluationResult> EvaluateSpeakingAsync(string topic, string transcriptText, string? audioUrl)
    {
        transcriptText ??= string.Empty;

        try
        {
            string prompt = BuildSpeakingFinalPrompt(topic, transcriptText);
            string json = await ChatAsync(prompt, SpeakingMaxTokens);
            var parsed = JsonSerializer.Deserialize<SpeakingAiResponse>(json, JsonOpts);

            if (parsed == null) return await _fallback.EvaluateSpeakingAsync(topic, transcriptText, audioUrl);

            return new AiSpeakingEvaluationResult
            {
                FluencyCoherence = ClampBand(parsed.FluencyCoherence),
                Pronunciation = ClampBand(parsed.Pronunciation),
                LexicalResource = ClampBand(parsed.LexicalResource),
                GrammaticalRangeAccuracy = ClampBand(parsed.GrammaticalRangeAccuracy),
                OverallBand = ClampBand(parsed.OverallBand),
                TurnBand = ClampBand(parsed.OverallBand),
                Feedback = parsed.TurnFeedback ?? "AI đã chấm bài.",
                WordCount = CountWords(transcriptText),
                LengthAssessment = parsed.LengthAssessment ?? string.Empty,
                PhonemeIssues = parsed.PhonemeIssues ?? new List<string>(),
                AdaptiveFollowUpQuestion = parsed.NextQuestion ?? string.Empty
            };
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "[SmartIELTS] Chấm Speaking (toàn bài) thất bại, dùng rule-based.");
            return await _fallback.EvaluateSpeakingAsync(topic, transcriptText, audioUrl);
        }
    }

    // =====================================================================
    // PROMPTS
    // =====================================================================

    private static string BuildWritingPrompt(string topic, string essay, int wordCount) => $$"""
You are a certified IELTS Writing examiner. Grade the essay below strictly according to the official Cambridge IELTS band descriptors.

TASK PROMPT:
{{topic}}

ESSAY ({{wordCount}} words):
{{essay}}

SCORING RULES:
- Score each criterion from 1.0 to 9.0 in steps of 0.5, using real IELTS band descriptors.
- Task Response: penalise answers under 250 words, off-topic content, or missing position.
- Do NOT reward length alone; a padded essay with weak ideas must score low on TR.
- A genuinely excellent 250+ word essay with clear position, full development, wide lexical range and accurate complex grammar MUST be able to reach 8.0-9.0.
- Be discriminating: weak essays should land 4.0-5.5, average 6.0-6.5, good 7.0-7.5, excellent 8.0+.

Return ONLY valid JSON (no markdown, no code fence) in exactly this shape:
{
  "taskResponse": 0.0,
  "coherenceCohesion": 0.0,
  "lexicalResource": 0.0,
  "grammaticalRangeAccuracy": 0.0,
  "overallBand": 0.0,
  "feedback": "Nhận xét chi tiết bằng tiếng Việt, 3-5 câu, nêu điểm mạnh và điểm cần cải thiện cụ thể.",
  "highlights": [
    {
      "errorType": "Grammar | Vocabulary | Collocation | Academic Highlight",
      "originalText": "exact substring copied from the essay",
      "suggestion": "the corrected or improved version",
      "explanation": "giải thích ngắn bằng tiếng Việt",
      "severity": "Red | Yellow | Green"
    }
  ],
  "modelAnswer": "A Band 8.0+ model answer in English (200-280 words) written for the SAME task prompt."
}

Rules for highlights: 3-8 items. Use "Red" for real grammar/accuracy errors, "Yellow" for awkward or basic vocabulary, "Green" for genuinely strong academic language. originalText MUST be copied verbatim from the essay so it can be located and highlighted.
""";

    private static string BuildSpeakingTurnPrompt(SpeakingSessionState state, string answer, int wordCount) => $$"""
You are an IELTS Speaking examiner conducting a live interview. Evaluate the candidate's latest answer, then ask the next question.

PART: {{DescribePart(state.CurrentPart)}}
QUESTION ASKED: {{state.CurrentQuestion}}
PREVIOUS QUESTIONS (for context, do not repeat them): {{DescribePreviousQuestions(state)}}

CANDIDATE'S ANSWER:
{{answer}}

RULES:
- Score Fluency & Coherence, Lexical Resource, Grammatical Range & Accuracy from 1.0-9.0 (step 0.5).
- Pronunciation: estimate from the transcript only (you cannot hear audio). Cap it at 7.0 and mention it is an estimate.
- Be discriminating: a one-word answer scores 4.0-4.5; a fluent, developed answer scores 7.0-8.0.
- Next question must follow the real IELTS flow:
  * Part 1: ask a simple personal question (3 questions total before moving on).
  * Part 2: if the cue card was just answered, move to Part 3.
  * Part 3: ask an abstract, challenging follow-up that builds DIRECTLY on what the candidate just said.
- Set "nextQuestion" to null ONLY when the interview should end (after 3 Part 3 answers).

Return ONLY valid JSON (no markdown, no code fence):
{
  "fluencyCoherence": 0.0,
  "pronunciation": 0.0,
  "lexicalResource": 0.0,
  "grammaticalRangeAccuracy": 0.0,
  "overallBand": 0.0,
  "turnFeedback": "Nhận xét 2-3 câu bằng tiếng Việt về câu trả lời này.",
  "lengthAssessment": "Quá ngắn | Hơi ngắn, nên phát triển thêm ý | Độ dài phù hợp | Rất chi tiết",
  "nextQuestion": "The next question in English, or null to end the interview.",
  "nextQuestionHints": "Gợi ý tiếng Việt: dàn ý + từ vựng nên dùng cho câu hỏi tiếp theo.",
  "modelAnswer": "A Band 8.0+ sample answer in English for the question just asked.",
  "phonemeIssues": ["1-3 lỗi phát âm thường gặp của người Việt, bằng tiếng Việt"],
  "highlights": [
    { "errorType": "Grammar", "originalText": "exact substring from the answer", "suggestion": "correction", "explanation": "giải thích", "severity": "Red" }
  ]
}
""";

    private static string BuildSpeakingFinalPrompt(string topic, string transcript) => $$"""
You are an IELTS Speaking examiner marking a full recorded interview transcript.

SECTION / TOPIC: {{topic}}

FULL TRANSCRIPT:
{{transcript}}

Score Fluency & Coherence, Lexical Resource, Grammatical Range & Accuracy from 1.0-9.0 (step 0.5).
For Pronunciation, estimate from transcript only and cap at 7.0, stating it is an estimate.

Return ONLY valid JSON (no markdown, no code fence):
{
  "fluencyCoherence": 0.0,
  "pronunciation": 0.0,
  "lexicalResource": 0.0,
  "grammaticalRangeAccuracy": 0.0,
  "overallBand": 0.0,
  "turnFeedback": "Nhận xét tổng hợp 3-5 câu bằng tiếng Việt.",
  "lengthAssessment": "Đánh giá độ dài bằng tiếng Việt",
  "phonemeIssues": ["2-3 lỗi phát âm thường gặp, bằng tiếng Việt"],
  "nextQuestion": "Một câu hỏi phản biện thích ứng dựa trên nội dung vừa nói."
}
""";

    private static string DescribePart(SpeakingPart part) => part switch
    {
        SpeakingPart.Part1Introduction => "Part 1 (Introduction & Interview)",
        SpeakingPart.Part2LongTurn => "Part 2 (Long Turn / Cue Card)",
        SpeakingPart.Part3Discussion => "Part 3 (Two-way Discussion)",
        _ => part.ToString()
    };

    private static string DescribePreviousQuestions(SpeakingSessionState state)
    {
        if (state.Turns.Count == 0) return "(none)";
        return string.Join(" | ", state.Turns.TakeLast(5).Select(t => t.Question));
    }

    // =====================================================================
    // HTTP
    // =====================================================================

    private async Task<string> ChatAsync(string prompt, int maxTokens, CancellationToken ct = default)
    {
        string text = await ChatOnceAsync(prompt, maxTokens, ct);

        // Model reasoning đôi khi vẫn tiêu hết token vào reasoning_content -> content rỗng.
        // Thử lại 1 lần với ngân sách token lớn hơn trước khi bỏ cuộc.
        if (string.IsNullOrWhiteSpace(text))
        {
            int retryTokens = Math.Min(maxTokens * EmptyContentRetryMultiplier, 16000);
            _logger?.LogInformation("[SmartIELTS] Content rỗng với max_tokens={Max}, thử lại với {Retry}.",
                maxTokens, retryTokens);

            text = await ChatOnceAsync(prompt, retryTokens, ct);
        }

        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException(
                "AI trả về nội dung rỗng (có thể do model reasoning tiêu hết token). Hãy tăng TimeoutSeconds hoặc thử lại.");

        return ExtractJson(text);
    }

    private async Task<string> ChatOnceAsync(string prompt, int maxTokens, CancellationToken ct)
    {
        var payload = new
        {
            model = _options.Model,
            messages = new[]
            {
                new { role = "system", content = "You are a strict but fair IELTS examiner. Always reply with valid JSON only." },
                new { role = "user", content = prompt }
            },
            temperature = _options.Temperature,
            max_tokens = maxTokens,
            response_format = new { type = "json_object" },
            stream = false
        };

        string body = JsonSerializer.Serialize(payload);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync("chat/completions", content, ct);

        string raw = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"AI API trả về {(int)response.StatusCode}: {Truncate(raw, 300)}");
        }

        using var doc = JsonDocument.Parse(raw);
        var choice = doc.RootElement.GetProperty("choices")[0];

        string? finishReason = choice.TryGetProperty("finish_reason", out var fr) ? fr.GetString() : null;
        string? text = choice.GetProperty("message").GetProperty("content").GetString();

        if (string.IsNullOrWhiteSpace(text))
        {
            // Log rõ để dễ debug khi demo: hết token hay model trả rỗng thật.
            string? reasoning = choice.GetProperty("message").TryGetProperty("reasoning_content", out var rc)
                ? rc.GetString() : null;

            _logger?.LogWarning("[SmartIELTS] AI content rỗng. finish_reason={Finish}, reasoning_len={Len}",
                finishReason, reasoning?.Length ?? 0);
        }

        return text ?? string.Empty;
    }

    /// <summary>Model đôi khi bọc JSON trong ```json ... ``` — cắt lấy object ngoài cùng.</summary>
    private static string ExtractJson(string text)
    {
        string trimmed = text.Trim();

        if (trimmed.StartsWith("```"))
        {
            int firstNewline = trimmed.IndexOf('\n');
            if (firstNewline >= 0) trimmed = trimmed[(firstNewline + 1)..];
            int lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
            if (lastFence > 0) trimmed = trimmed[..lastFence];
            trimmed = trimmed.Trim();
        }

        int start = trimmed.IndexOf('{');
        int end = trimmed.LastIndexOf('}');
        if (start >= 0 && end > start) trimmed = trimmed[start..(end + 1)];

        return trimmed;
    }

    // =====================================================================
    // HELPERS
    // =====================================================================

    private async Task<AiWritingEvaluationResult> SafeFallbackWritingAsync(string topic, string essay)
    {
        if (_options.FallbackToRuleBased)
            return await _fallback.EvaluateWritingAsync(topic, essay);

        throw new InvalidOperationException("Bộ chấm AI thất bại và FallbackToRuleBased = false.");
    }

    private async Task<AiSpeakingEvaluationResult> SafeFallbackTurnAsync(
        SpeakingSessionState state, string answer, string? audioUrl, CancellationToken ct)
    {
        if (_options.FallbackToRuleBased)
            return await _fallback.EvaluateSpeakingTurnAsync(state, answer, audioUrl, ct);

        throw new InvalidOperationException("Bộ chấm AI thất bại và FallbackToRuleBased = false.");
    }

    private static double ClampBand(double value)
    {
        if (double.IsNaN(value)) return 0;
        double clamped = Math.Clamp(value, 1.0, 9.0);
        // Làm tròn về bội số 0.5 theo chuẩn IELTS
        return Math.Round(clamped * 2, MidpointRounding.AwayFromZero) / 2;
    }

    private static string NormalizeColor(string? severity) => severity?.Trim().ToLowerInvariant() switch
    {
        "red" => "Red",
        "yellow" => "Yellow",
        "green" => "Green",
        _ => "Yellow"
    };

    private static int CountWords(string text)
        => text.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries).Length;

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value[..max] + "...";

    // =====================================================================
    // RESPONSE MODELS (khớp đúng JSON yêu cầu)
    // =====================================================================

    private sealed class WritingAiResponse
    {
        public double TaskResponse { get; set; }
        public double CoherenceCohesion { get; set; }
        public double LexicalResource { get; set; }
        public double GrammaticalRangeAccuracy { get; set; }
        public double OverallBand { get; set; }
        public string? Feedback { get; set; }
        public List<WritingHighlight>? Highlights { get; set; }
        public string? ModelAnswer { get; set; }
    }

    private sealed class SpeakingAiResponse
    {
        public double FluencyCoherence { get; set; }
        public double Pronunciation { get; set; }
        public double LexicalResource { get; set; }
        public double GrammaticalRangeAccuracy { get; set; }
        public double OverallBand { get; set; }
        public string? TurnFeedback { get; set; }
        public string? LengthAssessment { get; set; }
        public string? NextQuestion { get; set; }
        public string? NextQuestionHints { get; set; }
        public string? ModelAnswer { get; set; }
        public List<string>? PhonemeIssues { get; set; }
        public List<WritingHighlight>? Highlights { get; set; }
    }

    private sealed class WritingHighlight
    {
        public string? ErrorType { get; set; }
        public string? OriginalText { get; set; }
        public string? Suggestion { get; set; }
        public string? Explanation { get; set; }
        public string? Severity { get; set; }
    }
}
