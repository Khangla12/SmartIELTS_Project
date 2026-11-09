using SmartIELTS.Application.Interfaces;
using SmartIELTS.Domain.Enums;
using SmartIELTS.Infrastructure.Adapters;
using Xunit;

namespace SmartIELTS.UnitTests;

public class SpeakingInterviewTests
{
    private static SpeakingSessionState StateWith(params (SpeakingPart Part, string Question, string Answer)[] turns)
    {
        var state = new SpeakingSessionState();
        int i = 1;
        foreach (var (part, question, answer) in turns)
        {
            state.Turns.Add(new SpeakingTurn
            {
                TurnNumber = i++,
                Part = part,
                Question = question,
                Answer = answer
            });
        }

        if (state.Turns.Count > 0) state.CurrentPart = state.Turns[^1].Part;
        return state;
    }

    [Fact]
    public async Task Short_answer_gets_lower_band_than_detailed_answer()
    {
        var adapter = new IntelligentAiServiceAdapter();

        var shortState = new SpeakingSessionState { CurrentPart = SpeakingPart.Part1Introduction, CurrentQuestion = "Where do you live?" };
        var shortResult = await adapter.EvaluateSpeakingTurnAsync(shortState, "Apartment.", null);

        var longState = new SpeakingSessionState { CurrentPart = SpeakingPart.Part1Introduction, CurrentQuestion = "Where do you live?" };
        var longResult = await adapter.EvaluateSpeakingTurnAsync(
            longState,
            "I live in a small apartment in the city centre, mainly because it is convenient for commuting. " +
            "Although the space is limited, I find it cosy and well furnished, and the neighbourhood is quite peaceful.",
            null);

        Assert.True(longResult.TurnBand > shortResult.TurnBand,
            $"Câu trả lời chi tiết phải điểm cao hơn (short={shortResult.TurnBand}, long={longResult.TurnBand})");
    }

    [Fact]
    public async Task Detailed_answer_scores_higher_word_count()
    {
        var adapter = new IntelligentAiServiceAdapter();
        var state = new SpeakingSessionState { CurrentPart = SpeakingPart.Part1Introduction };

        var result = await adapter.EvaluateSpeakingTurnAsync(state,
            "In my free time I usually read and go jogging. For example, jogging helps me unwind after work.", null);

        Assert.True(result.WordCount >= 15);
        Assert.False(string.IsNullOrWhiteSpace(result.LengthAssessment));
    }

    [Fact]
    public async Task Grammar_issue_is_detected_with_red_highlight()
    {
        var adapter = new IntelligentAiServiceAdapter();
        var state = new SpeakingSessionState { CurrentPart = SpeakingPart.Part1Introduction };

        var result = await adapter.EvaluateSpeakingTurnAsync(
            state, "My brother he have a lot of things to do every weekend.", null);

        Assert.Contains(result.Highlights, h => h.SeverityColor == "Red");
    }

    [Fact]
    public async Task Good_connectors_produce_green_highlight()
    {
        var adapter = new IntelligentAiServiceAdapter();
        var state = new SpeakingSessionState { CurrentPart = SpeakingPart.Part3Discussion };

        var result = await adapter.EvaluateSpeakingTurnAsync(
            state, "For instance, from my perspective technology is beneficial. On the other hand, it can be distracting.", null);

        Assert.Contains(result.Highlights, h => h.SeverityColor == "Green");
    }

    [Fact]
    public async Task Part1_progresses_to_part2_scenario()
    {
        var adapter = new IntelligentAiServiceAdapter();

        // Đã trả lời 2 câu Part 1 -> lượt hiện tại là câu 3 -> câu tiếp theo phải thuộc Part 2 (cue card).
        var state = StateWith(
            (SpeakingPart.Part1Introduction, "Q1", "answer one"),
            (SpeakingPart.Part1Introduction, "Q2", "answer two"));
        state.CurrentPart = SpeakingPart.Part1Introduction;

        var result = await adapter.EvaluateSpeakingTurnAsync(state, "answer three about my apartment", null);

        Assert.Contains("describe a skill", result.NextQuestion!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Adaptive_question_follows_the_topic_of_the_answer()
    {
        var adapter = new IntelligentAiServiceAdapter();

        // Đã qua Part 1 (3 câu) + Part 2; lượt hiện tại là câu Part 3 ĐẦU TIÊN trả lời về education.
        var state = StateWith(
            (SpeakingPart.Part1Introduction, "Q1", "a1"),
            (SpeakingPart.Part1Introduction, "Q2", "a2"),
            (SpeakingPart.Part1Introduction, "Q3", "a3"),
            (SpeakingPart.Part2LongTurn, "Cue card", "a4"));
        state.CurrentPart = SpeakingPart.Part3Discussion;

        var result = await adapter.EvaluateSpeakingTurnAsync(
            state,
            "Because education and schools give students opportunities, teachers matter a lot.", null);

        Assert.Contains("education", result.NextQuestion!, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Second_part3_question_returns_to_the_scripted_set()
    {
        var adapter = new IntelligentAiServiceAdapter();

        // Đã trả lời câu Part 3 đầu tiên -> câu hỏi kế tiếp lấy từ kịch bản.
        var state = StateWith(
            (SpeakingPart.Part1Introduction, "Q1", "a1"),
            (SpeakingPart.Part1Introduction, "Q2", "a2"),
            (SpeakingPart.Part1Introduction, "Q3", "a3"),
            (SpeakingPart.Part2LongTurn, "Cue", "a4"),
            (SpeakingPart.Part3Discussion, "P3-1", "a5"));
        state.CurrentPart = SpeakingPart.Part3Discussion;

        var result = await adapter.EvaluateSpeakingTurnAsync(state, "a6 second part3 answer", null);

        Assert.False(string.IsNullOrWhiteSpace(result.NextQuestion));
    }

    [Fact]
    public async Task Interview_finishes_after_three_part3_answers()
    {
        var adapter = new IntelligentAiServiceAdapter();

        var state = StateWith(
            (SpeakingPart.Part1Introduction, "Q1", "a1"),
            (SpeakingPart.Part1Introduction, "Q2", "a2"),
            (SpeakingPart.Part1Introduction, "Q3", "a3"),
            (SpeakingPart.Part2LongTurn, "Cue", "a4"),
            (SpeakingPart.Part3Discussion, "P3-1", "a5"),
            (SpeakingPart.Part3Discussion, "P3-2", "a6"));
        state.CurrentPart = SpeakingPart.Part3Discussion;

        var result = await adapter.EvaluateSpeakingTurnAsync(state, "a7 final answer", null);

        Assert.True(string.IsNullOrWhiteSpace(result.NextQuestion), "Sau 3 câu Part 3 phải kết thúc buổi vấn đáp.");
    }

    [Fact]
    public async Task Every_turn_returns_a_model_answer_and_feedback()
    {
        var adapter = new IntelligentAiServiceAdapter();
        var state = new SpeakingSessionState { CurrentPart = SpeakingPart.Part3Discussion };

        var result = await adapter.EvaluateSpeakingTurnAsync(state, "I think technology is beneficial.", null);

        Assert.False(string.IsNullOrWhiteSpace(result.ModelAnswer));
        Assert.False(string.IsNullOrWhiteSpace(result.TurnFeedback));
    }

    [Fact]
    public async Task Chinese_or_garbage_answer_does_not_crash()
    {
        var adapter = new IntelligentAiServiceAdapter();
        var state = new SpeakingSessionState { CurrentPart = SpeakingPart.Part1Introduction };

        var result = await adapter.EvaluateSpeakingTurnAsync(state, "我 sống ở Hà Nội", null);

        Assert.True(result.TurnBand >= 3.0 && result.TurnBand <= 9.0);
    }
}
