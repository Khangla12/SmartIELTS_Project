using SmartIELTS.Application.DTOs;
using SmartIELTS.Domain.Common;
using SmartIELTS.Domain.Entities;
using SmartIELTS.Domain.Enums;

namespace SmartIELTS.Application.GradingStrategies;

public class ListeningToleranceGrader : ObjectiveExamGraderBase
{
    public override SkillType Skill => SkillType.Listening;

    protected override bool IsCorrect(string? candidateAnswer, Question question)
    {
        // Listening dùng dung sai: hoa/thường, dấu câu, số dạng chữ, biến thể Anh - Mỹ, nhiều đáp án "A / B".
        return AnswerNormalizer.IsMatch(candidateAnswer, question.CorrectAnswer);
    }

    protected override object BuildDetail(Question question, string candidateAnswer, bool isCorrect)
        => new
        {
            QuestionNumber = question.QuestionNumber,
            CandidateAnswer = candidateAnswer,
            CorrectAnswer = question.CorrectAnswer,
            IsCorrect = isCorrect
        };

    protected override GradingResultDto BuildResult(ExamSection section, int correctCount, List<object> details)
    {
        int total = section.Questions.Count;
        double bandScore = BandScoreCalculator.ConvertListeningRawToBandForSection(correctCount, total);

        return new GradingResultDto
        {
            RawScore = correctCount,
            TotalQuestions = total,
            BandScore = bandScore,
            FeedbackComments = $"Bạn đã hoàn thành bài thi Listening với {correctCount}/{total} câu trả lời chính xác. Band score đạt được: {bandScore}.",
            DetailedJsonAnalysis = System.Text.Json.JsonSerializer.Serialize(details)
        };
    }

    /// <summary>Giữ lại API cũ cho các nơi đang gọi trực tiếp.</summary>
    public static bool IsAnswerTolerantMatch(string candidate, string correct)
        => AnswerNormalizer.IsMatch(candidate, correct);
}
