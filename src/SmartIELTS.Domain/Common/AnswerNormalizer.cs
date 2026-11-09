using System.Text.RegularExpressions;

namespace SmartIELTS.Domain.Common;

/// <summary>
/// Chuẩn hóa đáp án điền từ của thí sinh trước khi so khớp:
/// bỏ hoa/thường, dấu câu, gộp khoảng trắng, quy đổi số dạng chữ và biến thể Anh - Mỹ.
/// </summary>
public static class AnswerNormalizer
{
    private static readonly Dictionary<string, string> SpellingVariants = new(StringComparer.OrdinalIgnoreCase)
    {
        { "colour", "color" },
        { "flavour", "flavor" },
        { "theatre", "theater" },
        { "centre", "center" },
        { "programme", "program" },
        { "organise", "organize" },
        { "realise", "realize" },
        { "analyse", "analyze" },
        { "travelled", "traveled" },
        { "grey", "gray" },
        { "licence", "license" },
        { "defence", "defense" }
    };

    private static readonly Dictionary<string, string> WordToNumberMap = new(StringComparer.OrdinalIgnoreCase)
    {
        { "zero", "0" }, { "one", "1" }, { "two", "2" }, { "three", "3" },
        { "four", "4" }, { "five", "5" }, { "six", "6" }, { "seven", "7" },
        { "eight", "8" }, { "nine", "9" }, { "ten", "10" }
    };

    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        string result = input.Trim().ToLowerInvariant();
        result = Regex.Replace(result, @"[^\w\s/|;]", string.Empty);
        result = Regex.Replace(result, @"\s+", " ");

        var words = result.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < words.Length; i++)
        {
            if (WordToNumberMap.TryGetValue(words[i], out var digit))
            {
                words[i] = digit;
            }
            else if (SpellingVariants.TryGetValue(words[i], out var standardWord))
            {
                words[i] = standardWord;
            }
        }

        return string.Join(" ", words);
    }

    /// <summary>
    /// So khớp một đáp án thí sinh với đáp án chuẩn.
    /// Đáp án chuẩn có thể chứa nhiều biến thể ngăn cách bởi '/', '|' hoặc ';'.
    /// </summary>
    /// <param name="ignoreSpacing">
    /// Bỏ qua khác biệt khoảng trắng, dùng cho dạng câu True/False/Not Given ("NotGiven" == "Not Given").
    /// </param>
    public static bool IsMatch(string? candidateAnswer, string? correctAnswer, bool ignoreSpacing = false)
    {
        string normalizedCandidate = Normalize(candidateAnswer);
        if (normalizedCandidate.Length == 0) return false;

        string normalizedCorrect = Normalize(correctAnswer);
        if (normalizedCorrect.Length == 0) return false;

        foreach (var variant in normalizedCorrect.Split(new[] { '/', '|', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string normalizedVariant = Normalize(variant);
            if (normalizedVariant == normalizedCandidate) return true;

            if (ignoreSpacing && RemoveSpaces(normalizedVariant) == RemoveSpaces(normalizedCandidate)) return true;
        }

        return false;
    }

    private static string RemoveSpaces(string value) => value.Replace(" ", string.Empty);
}
