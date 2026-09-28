using System.Text.Json;
using SmartIELTS.Application.DTOs;
using SmartIELTS.Application.Interfaces;
using SmartIELTS.Domain.Entities;
using SmartIELTS.Domain.Enums;

namespace SmartIELTS.Application.GradingStrategies;

public class WritingAiGrader : IExamGrader
{
    public SkillType Skill => SkillType.Writing;

    private readonly IAiServiceAdapter _aiAdapter;

    public WritingAiGrader(IAiServiceAdapter aiAdapter)
    {
        _aiAdapter = aiAdapter;
    }

    public async Task<GradingResultDto> GradeAsync(CandidateSubmission submission, ExamSection section)
    {
        string essay = submission.EssayContent ?? string.Empty;
        string prompt = section.PassageText ?? "IELTS Writing Task 2 Essay";

        var aiResult = await _aiAdapter.EvaluateWritingAsync(prompt, essay);

        return new GradingResultDto
        {
            RawScore = aiResult.OverallBand,
            BandScore = aiResult.OverallBand,
            CriterionScore1 = aiResult.TaskResponse,
            CriterionScore2 = aiResult.CoherenceCohesion,
            CriterionScore3 = aiResult.LexicalResource,
            CriterionScore4 = aiResult.GrammaticalRangeAccuracy,
            FeedbackComments = aiResult.Feedback,
            DetailedJsonAnalysis = JsonSerializer.Serialize(new
            {
                Highlights = aiResult.Highlights,
                ModelAnswerBand8 = aiResult.ModelAnswerBand8
            })
        };
    }
}

public class SpeakingSpeechGrader : IExamGrader
{
    public SkillType Skill => SkillType.Speaking;

    private readonly IAiServiceAdapter _aiAdapter;

    public SpeakingSpeechGrader(IAiServiceAdapter aiAdapter)
    {
        _aiAdapter = aiAdapter;
    }

    public async Task<GradingResultDto> GradeAsync(CandidateSubmission submission, ExamSection section)
    {
        string prompt = section.PassageText ?? "IELTS Speaking Part 3 Discussion";
        string transcript = submission.EssayContent ?? "Candidate response transcript";

        var aiResult = await _aiAdapter.EvaluateSpeakingAsync(prompt, transcript, submission.AudioRecordUrl);

        return new GradingResultDto
        {
            RawScore = aiResult.OverallBand,
            BandScore = aiResult.OverallBand,
            CriterionScore1 = aiResult.FluencyCoherence,
            CriterionScore2 = aiResult.Pronunciation,
            CriterionScore3 = aiResult.LexicalResource,
            CriterionScore4 = aiResult.GrammaticalRangeAccuracy,
            FeedbackComments = aiResult.Feedback,
            DetailedJsonAnalysis = JsonSerializer.Serialize(new
            {
                PhonemeIssues = aiResult.PhonemeIssues,
                AdaptiveFollowUpQuestion = aiResult.AdaptiveFollowUpQuestion
            })
        };
    }
}
