using System.Text.Json;
using SmartIELTS.Application.DTOs;
using SmartIELTS.Domain.Entities;
using SmartIELTS.Domain.Enums;

namespace SmartIELTS.Application.GradingStrategies;

/// <summary>
/// Phần dùng chung của các bộ chấm dạng trắc nghiệm / điền từ:
/// đọc AnswersJson, chuẩn hóa khóa (Guid) và đếm số câu đúng.
/// </summary>
public abstract class ObjectiveExamGraderBase : IExamGrader
{
    public abstract SkillType Skill { get; }

    protected abstract bool IsCorrect(string? candidateAnswer, Question question);

    public Task<GradingResultDto> GradeAsync(CandidateSubmission submission, ExamSection section)
    {
        var candidateAnswers = ParseAnswers(submission.AnswersJson);

        int correctCount = 0;
        var detailedResults = new List<object>();

        foreach (var q in section.Questions.OrderBy(x => x.QuestionNumber))
        {
            var (found, rawCandidateAnswer) = TryGetAnswer(candidateAnswers, q.Id);
            string answer = found ? rawCandidateAnswer : string.Empty;

            bool isCorrect = IsCorrect(answer, q);
            if (isCorrect) correctCount++;

            detailedResults.Add(BuildDetail(q, answer, isCorrect));
        }

        return Task.FromResult(BuildResult(section, correctCount, detailedResults));
    }

    protected abstract object BuildDetail(Question question, string candidateAnswer, bool isCorrect);

    protected abstract GradingResultDto BuildResult(ExamSection section, int correctCount, List<object> details);

    protected static Dictionary<string, string> ParseAnswers(string? answersJson)
    {
        if (string.IsNullOrWhiteSpace(answersJson)) return new Dictionary<string, string>();

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(answersJson)
                   ?? new Dictionary<string, string>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>();
        }
    }

    /// <summary>
    /// Khóa đáp án trong JSON có thể là Guid chuẩn hoặc Guid không có dấu gạch nối
    /// (System.Text.Json serialize Dictionary&lt;Guid, string&gt; luôn dùng dạng có gạch nối,
    /// nhưng dữ liệu cũ có thể lưu dạng "N").
    /// </summary>
    private static (bool Found, string Value) TryGetAnswer(Dictionary<string, string> answers, Guid questionId)
    {
        if (answers.Count == 0) return (false, string.Empty);

        if (answers.TryGetValue(questionId.ToString(), out var exact)) return (true, exact ?? string.Empty);

        string compactKey = questionId.ToString("N");
        foreach (var pair in answers)
        {
            if (string.Equals(pair.Key, compactKey, StringComparison.OrdinalIgnoreCase))
                return (true, pair.Value ?? string.Empty);
        }

        return (false, string.Empty);
    }
}
