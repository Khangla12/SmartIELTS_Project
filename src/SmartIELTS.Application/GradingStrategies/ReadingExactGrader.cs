using System.Text.Json;
using SmartIELTS.Application.DTOs;
using SmartIELTS.Domain.Common;
using SmartIELTS.Domain.Entities;
using SmartIELTS.Domain.Enums;

namespace SmartIELTS.Application.GradingStrategies;

public class ReadingExactGrader : IExamGrader
{
    public SkillType Skill => SkillType.Reading;

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
            catch { /* Ignore error */ }
        }

        int correctCount = 0;
        var detailedResults = new List<object>();

        foreach (var q in section.Questions.OrderBy(x => x.QuestionNumber))
        {
            string key = q.Id.ToString();
            candidateAnswers.TryGetValue(key, out var rawCandidateAnswer);
            rawCandidateAnswer ??= string.Empty;

            bool isCorrect = string.Equals(rawCandidateAnswer.Trim(), q.CorrectAnswer.Trim(), StringComparison.OrdinalIgnoreCase);
            if (isCorrect)
            {
                correctCount++;
            }

            detailedResults.Add(new
            {
                QuestionNumber = q.QuestionNumber,
                CandidateAnswer = rawCandidateAnswer,
                CorrectAnswer = q.CorrectAnswer,
                IsCorrect = isCorrect,
                Explanation = isCorrect 
                    ? "Đúng: Câu trả lời khớp hoàn toàn với thông tin từ bài đọc."
                    : $"Sai: Đáp án chính xác là '{q.CorrectAnswer}'. Đoạn văn chứa bằng chứng đối chiếu trong bài đọc chứng minh quan điểm này."
            });
        }

        double bandScore = BandScoreCalculator.ConvertReadingRawToBand(correctCount, isAcademic: true);

        var result = new GradingResultDto
        {
            RawScore = correctCount,
            BandScore = bandScore,
            FeedbackComments = $"Bạn đã hoàn thành bài thi Reading với {correctCount}/{section.Questions.Count} câu chính xác. Band score đạt được: {bandScore}.",
            DetailedJsonAnalysis = JsonSerializer.Serialize(detailedResults)
        };

        return Task.FromResult(result);
    }
}
