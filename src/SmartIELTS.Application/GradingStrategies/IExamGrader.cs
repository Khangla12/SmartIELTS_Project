using SmartIELTS.Application.DTOs;
using SmartIELTS.Domain.Entities;
using SmartIELTS.Domain.Enums;

namespace SmartIELTS.Application.GradingStrategies;

public interface IExamGrader
{
    SkillType Skill { get; }
    Task<GradingResultDto> GradeAsync(CandidateSubmission submission, ExamSection section);
}

public interface IGradingStrategyFactory
{
    IExamGrader GetGrader(SkillType skillType);
}

public class GradingStrategyFactory : IGradingStrategyFactory
{
    private readonly IEnumerable<IExamGrader> _graders;

    public GradingStrategyFactory(IEnumerable<IExamGrader> graders)
    {
        _graders = graders;
    }

    public IExamGrader GetGrader(SkillType skillType)
    {
        var grader = _graders.FirstOrDefault(g => g.Skill == skillType);
        if (grader == null)
        {
            throw new NotSupportedException($"No grading strategy registered for skill {skillType}");
        }
        return grader;
    }
}
