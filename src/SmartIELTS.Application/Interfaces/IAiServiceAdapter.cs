namespace SmartIELTS.Application.Interfaces;

public class AiWritingEvaluationResult
{
    public double TaskResponse { get; set; }
    public double CoherenceCohesion { get; set; }
    public double LexicalResource { get; set; }
    public double GrammaticalRangeAccuracy { get; set; }
    public double OverallBand { get; set; }
    public string Feedback { get; set; } = string.Empty;
    public List<TextHighlightError> Highlights { get; set; } = new();
    public string ModelAnswerBand8 { get; set; } = string.Empty;
}

public class TextHighlightError
{
    public string ErrorType { get; set; } = "Grammar"; // Grammar (Red), Vocabulary (Yellow), Highlight (Green)
    public string OriginalText { get; set; } = string.Empty;
    public string Suggestion { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public string SeverityColor { get; set; } = "Red"; // "Red", "Yellow", "Green"
}

public class AiSpeakingEvaluationResult
{
    public double FluencyCoherence { get; set; }
    public double Pronunciation { get; set; }
    public double LexicalResource { get; set; }
    public double GrammaticalRangeAccuracy { get; set; }
    public double OverallBand { get; set; }
    public string Feedback { get; set; } = string.Empty;
    public List<string> PhonemeIssues { get; set; } = new();
    public string AdaptiveFollowUpQuestion { get; set; } = string.Empty;
}

public interface IAiServiceAdapter
{
    Task<AiWritingEvaluationResult> EvaluateWritingAsync(string topic, string essayText);
    Task<AiSpeakingEvaluationResult> EvaluateSpeakingAsync(string topic, string transcriptText, string? audioUrl);
}
