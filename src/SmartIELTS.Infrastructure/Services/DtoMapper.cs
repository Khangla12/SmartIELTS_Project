using SmartIELTS.Application.DTOs;
using SmartIELTS.Domain.Entities;
using SmartIELTS.Domain.Enums;

namespace SmartIELTS.Infrastructure.Services;

internal static class DtoMapper
{
    public static ExamDto ToExamDto(Exam exam) => new()
    {
        Id = exam.Id,
        Title = exam.Title,
        Description = exam.Description,
        ExamType = exam.ExamType,
        TotalDurationMinutes = exam.TotalDurationMinutes,
        Sections = exam.Sections
            .OrderBy(s => s.SectionOrder)
            .Select(ToSectionDto)
            .ToList()
    };

    public static ExamSectionDto ToSectionDto(ExamSection section) => new()
    {
        Id = section.Id,
        SkillType = section.SkillType,
        SectionOrder = section.SectionOrder,
        DurationMinutes = section.DurationMinutes,
        MediaUrl = section.MediaUrl,
        PassageText = section.PassageText,
        // Không trả CorrectAnswer ra ngoài: DTO cố tình không chứa trường này.
        Questions = section.Questions
            .OrderBy(q => q.QuestionNumber)
            .Select(q => new QuestionDto
            {
                Id = q.Id,
                QuestionNumber = q.QuestionNumber,
                QuestionType = q.QuestionType,
                QuestionText = q.QuestionText,
                OptionsJson = q.OptionsJson,
                MaxScore = q.MaxScore
            })
            .ToList()
    };

    public static SessionResultDto ToSessionResultDto(ExamSession session)
    {
        double? BandOf(SkillType skill) => session.Submissions
            .FirstOrDefault(s => s.SkillType == skill)?.BandScore;

        var l = BandOf(SkillType.Listening);
        var r = BandOf(SkillType.Reading);
        var w = BandOf(SkillType.Writing);
        var s = BandOf(SkillType.Speaking);

        return new SessionResultDto
        {
            SessionId = session.Id,
            CandidateName = session.User?.FullName ?? "Thí sinh",
            ExamTitle = session.Exam?.Title ?? "Kỳ thi IELTS",
            Status = session.Status,
            OverallBandScore = session.OverallBandScore,
            ListeningBand = l,
            ReadingBand = r,
            WritingBand = w,
            SpeakingBand = s,
            Submissions = session.Submissions.Select(sub => new SkillSubmissionDto
            {
                SkillType = sub.SkillType,
                RawScore = sub.RawScore,
                BandScore = sub.BandScore,
                EssayContent = sub.EssayContent,
                Reports = sub.EvaluationReports.Select(rep => new EvaluationReportDto
                {
                    Id = rep.Id,
                    EvaluatedBy = rep.EvaluatedBy,
                    CriterionScore1 = rep.CriterionScore1,
                    CriterionScore2 = rep.CriterionScore2,
                    CriterionScore3 = rep.CriterionScore3,
                    CriterionScore4 = rep.CriterionScore4,
                    FeedbackComments = rep.FeedbackComments,
                    DetailedJsonAnalysis = rep.DetailedJsonAnalysis,
                    IsApproved = rep.IsApproved
                }).ToList()
            }).ToList()
        };
    }
}
