using System.Text.Json;
using SmartIELTS.Application.GradingStrategies;
using SmartIELTS.Application.Interfaces;
using SmartIELTS.Domain.Entities;
using SmartIELTS.Domain.Enums;
using Xunit;

namespace SmartIELTS.UnitTests;

public class GradingStrategyTests
{
    private static ExamSection BuildListeningSection() => new()
    {
        SkillType = SkillType.Listening,
        Questions = new List<Question>
        {
            new() { QuestionNumber = 1, CorrectAnswer = "Anderson" },
            new() { QuestionNumber = 2, CorrectAnswer = "25 October / October 25" },
            new() { QuestionNumber = 3, CorrectAnswer = "3 / three" },
            new() { QuestionNumber = 4, CorrectAnswer = "Credit Card" },
            new() { QuestionNumber = 5, CorrectAnswer = "color / colour" }
        }
    };

    private static CandidateSubmission SubmissionWith(ExamSection section, params string[] answers)
    {
        var dict = new Dictionary<string, string>();
        var questions = section.Questions.OrderBy(q => q.QuestionNumber).ToList();
        for (int i = 0; i < questions.Count && i < answers.Length; i++)
        {
            dict[questions[i].Id.ToString()] = answers[i];
        }

        return new CandidateSubmission
        {
            SkillType = section.SkillType,
            AnswersJson = JsonSerializer.Serialize(dict)
        };
    }

    [Fact]
    public async Task Listening_grades_five_correct_answers_to_band_nine()
    {
        var section = BuildListeningSection();
        var submission = SubmissionWith(section, "Anderson", "October 25", "three", "credit card", "COLOUR.");

        var result = await new ListeningToleranceGrader().GradeAsync(submission, section);

        Assert.Equal(5, result.RawScore);
        Assert.Equal(5, result.TotalQuestions);
        Assert.Equal(9.0, result.BandScore);
    }

    [Fact]
    public async Task Listening_counts_wrong_answers_as_incorrect()
    {
        var section = BuildListeningSection();
        var submission = SubmissionWith(section, "Smith", "24 October", "three", "", "colour");

        var result = await new ListeningToleranceGrader().GradeAsync(submission, section);

        Assert.Equal(2, result.RawScore);
    }

    [Fact]
    public async Task Listening_tolerates_malformed_answers_json()
    {
        var section = BuildListeningSection();
        var submission = new CandidateSubmission { AnswersJson = "{not valid json" };

        var result = await new ListeningToleranceGrader().GradeAsync(submission, section);

        Assert.Equal(0, result.RawScore);
    }

    [Fact]
    public async Task Reading_grades_ignores_case_and_spacing_for_true_false_not_given()
    {
        var section = new ExamSection
        {
            SkillType = SkillType.Reading,
            Questions = new List<Question>
            {
                new() { QuestionNumber = 1, QuestionType = QuestionType.TrueFalseNotGiven, CorrectAnswer = "True" },
                new() { QuestionNumber = 2, QuestionType = QuestionType.TrueFalseNotGiven, CorrectAnswer = "Not Given" },
                new() { QuestionNumber = 3, QuestionType = QuestionType.FillBlank, CorrectAnswer = "raw data" }
            }
        };

        var submission = SubmissionWith(section, "TRUE", "NotGiven", "  RAW DATA  ");

        var result = await new ReadingExactGrader().GradeAsync(submission, section);

        Assert.Equal(3, result.RawScore);
        Assert.Equal(9.0, result.BandScore);
    }

    [Fact]
    public void Factory_resolves_every_registered_skill()
    {
        var adapter = new StubAiAdapter();
        var graders = new IExamGrader[]
        {
            new ListeningToleranceGrader(),
            new ReadingExactGrader(),
            new WritingAiGrader(adapter),
            new SpeakingSpeechGrader(adapter)
        };

        var factory = new GradingStrategyFactory(graders);

        Assert.IsType<ListeningToleranceGrader>(factory.GetGrader(SkillType.Listening));
        Assert.IsType<ReadingExactGrader>(factory.GetGrader(SkillType.Reading));
        Assert.IsType<WritingAiGrader>(factory.GetGrader(SkillType.Writing));
        Assert.IsType<SpeakingSpeechGrader>(factory.GetGrader(SkillType.Speaking));
    }

    [Fact]
    public void Factory_throws_for_unregistered_skill()
    {
        var factory = new GradingStrategyFactory(Array.Empty<IExamGrader>());
        Assert.Throws<NotSupportedException>(() => factory.GetGrader(SkillType.Writing));
    }

    private sealed class StubAiAdapter : IAiServiceAdapter
    {
        public Task<AiWritingEvaluationResult> EvaluateWritingAsync(string topic, string essayText)
            => Task.FromResult(new AiWritingEvaluationResult());

        public Task<AiSpeakingEvaluationResult> EvaluateSpeakingAsync(string topic, string transcriptText, string? audioUrl)
            => Task.FromResult(new AiSpeakingEvaluationResult());

        public Task<AiSpeakingEvaluationResult> EvaluateSpeakingTurnAsync(
            SpeakingSessionState state, string answer, string? audioUrl, CancellationToken ct = default)
            => Task.FromResult(new AiSpeakingEvaluationResult());
    }
}
