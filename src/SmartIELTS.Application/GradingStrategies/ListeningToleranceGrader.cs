using System.Text.Json;
using System.Text.RegularExpressions;
using SmartIELTS.Application.DTOs;
using SmartIELTS.Domain.Common;
using SmartIELTS.Domain.Entities;
using SmartIELTS.Domain.Enums;

namespace SmartIELTS.Application.GradingStrategies;

public class ListeningToleranceGrader : IExamGrader
{
    public SkillType Skill => SkillType.Listening;

    private static readonly Dictionary<string, string> SpellingVariants = new(StringComparer.OrdinalIgnoreCase)
    {
        { "colour", "color" },
        { "flavour", "flavor" },
        { "theatre", "theater" },
        { "centre", "center" },
        { "programme", "program" },
        { "organise", "organize" },
        { "realise", "realize" },
        { "analyse", "analyze" },
        { "travelled", "traveled" },
        { "grey", "gray" }
    };

    private static readonly Dictionary<string, string> WordToNumberMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "zero", "0" }, { "one", "1" }, { "two", "2" }, { "three", "3" },
        { "four", "4" }, { "five", "5" }, { "six", "6" }, { "seven", "7" },
        { "eight", "8" }, { "nine", "9" }, { "ten", "10" }
    };

    public Task<GradingResultDto> GradeAsync(CandidateSubmission submission, ExamSection section)
    {
        Dictionary<string, string> candidateAnswers = new();
        if (!string.IsNullOrWhiteSpace(submission.AnswersJson))
        {
            try
            {
                candidateAnswers = JsonSerializer.Deserialize<Dictionary<string, string>>(submission.AnswersJson) 
                                   ?? new Dictionary<string, string>();
            }
            catch { /* Ignore parse error */ }
        }

        int correctCount = 0;
        var detailedResults = new List<object>();

        foreach (var q in section.Questions.OrderBy(x => x.QuestionNumber))
        {
            string key = q.Id.ToString();
            candidateAnswers.TryGetValue(key, out var rawCandidateAnswer);
            rawCandidateAnswer ??= string.Empty;

            bool isCorrect = IsAnswerTolerantMatch(rawCandidateAnswer, q.CorrectAnswer);
            if (isCorrect)
            {
                correctCount++;
            }

            detailedResults.Add(new
            {
                QuestionNumber = q.QuestionNumber,
                CandidateAnswer = rawCandidateAnswer,
                CorrectAnswer = q.CorrectAnswer,
                IsCorrect = isCorrect
            });
        }

        double bandScore = BandScoreCalculator.ConvertListeningRawToBand(correctCount);

        var result = new GradingResultDto
        {
            RawScore = correctCount,
            BandScore = bandScore,
            FeedbackComments = $"Bạn đã hoàn thành bài thi Listening với {correctCount}/{section.Questions.Count} câu trả lời chính xác. Band score đạt được: {bandScore}.",
            DetailedJsonAnalysis = JsonSerializer.Serialize(detailedResults)
        };

        return Task.FromResult(result);
    }

    public static bool IsAnswerTolerantMatch(string candidate, string correct)
    {
        string normCandidate = NormalizeAnswer(candidate);
        string normCorrect = NormalizeAnswer(correct);

        if (string.Equals(normCandidate, normCorrect, StringComparison.OrdinalIgnoreCase))
            return true;

        // Check slash variants (e.g. "car / automobile")
        var acceptedVariants = normCorrect.Split(new[] { '/', '|', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                          .Select(NormalizeAnswer);
        return acceptedVariants.Any(v => string.Equals(v, normCandidate, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeAnswer(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        // Lowercase & trim
        string result = input.Trim().ToLowerInvariant();

        // Remove punctuation
        result = Regex.Replace(result, @"[^\w\s/]", "");

        // Collapse multiple whitespace
        result = Regex.Replace(result, @"\s+", " ");

        // Convert word numbers (e.g. "three" -> "3")
        var words = result.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            if (WordToNumberMap.TryGetValue(words[i], out var digit))
            {
                words[i] = digit;
            }
            else if (SpellingVariants.TryGetValue(words[i], out var standardWord))
            {
                words[i] = standardWord;
            }
        }

        return string.Join(" ", words);
    }
}
