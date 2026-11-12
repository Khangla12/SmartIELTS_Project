using SmartIELTS.Application.DTOs;
using SmartIELTS.Domain.Common;
using SmartIELTS.Domain.Entities;
using SmartIELTS.Domain.Enums;

namespace SmartIELTS.Application.GradingStrategies;

public class ReadingExactGrader : ObjectiveExamGraderBase
{
    public override SkillType Skill => SkillType.Reading;

    protected override bool IsCorrect(string? candidateAnswer, Question question)
    {
        // Reading: khớp chính xác nhưng không phân biệt hoa/thường và bỏ qua khác biệt khoảng trắng
        // (ví dụ "NotGiven" vẫn được tính đúng với "Not Given").
        bool ignoreSpacing = question.QuestionType == QuestionType.TrueFalseNotGiven;
        return AnswerNormalizer.IsMatch(candidateAnswer, question.CorrectAnswer, ignoreSpacing);
    }

    protected override object BuildDetail(Question question, string candidateAnswer, bool isCorrect)
        => new
        {
            QuestionNumber = question.QuestionNumber,
            CandidateAnswer = candidateAnswer,
            CorrectAnswer = question.CorrectAnswer,
            IsCorrect = isCorrect,
            Explanation = isCorrect
                ? "Đúng: Câu trả lời khớp hoàn toàn với thông tin từ bài đọc."
                : $"Sai: Đáp án chính xác là '{question.CorrectAnswer}'. Đoạn văn chứa bằng chứng đối chiếu trong bài đọc chứng minh quan điểm này."
        };

    protected override GradingResultDto BuildResult(ExamSection section, int correctCount, List<object> details)
    {
        int total = section.Questions.Count;

        // Reading Academic và General Training dùng bảng quy đổi KHÁC nhau.
        // Lấy ExamType từ đề thi (collection đã Include ở use case gọi vào).
        bool isAcademic = section.Exam?.ExamType != ExamType.GeneralTraining;

        double bandScore = BandScoreCalculator.ConvertReadingRawToBandForSection(correctCount, total, isAcademic);

        return new GradingResultDto
        {
            RawScore = correctCount,
            TotalQuestions = total,
            BandScore = bandScore,
            FeedbackComments = $"Bạn đã hoàn thành bài thi Reading ({section.Exam?.ExamType ?? ExamType.Academic}) " +
                               $"với {correctCount}/{total} câu chính xác. Band score đạt được: {bandScore}.",
            DetailedJsonAnalysis = System.Text.Json.JsonSerializer.Serialize(details)
        };
    }
}
