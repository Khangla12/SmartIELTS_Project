namespace SmartIELTS.Domain.Common;

public static class BandScoreCalculator
{
    /// <summary>
    /// Số câu của một đề IELTS hoàn chỉnh (Listening / Reading đều 40 câu).
    /// Đề demo trong hệ thống chỉ có 5 câu/section nên cần quy đổi trước khi tra bảng band.
    /// </summary>
    public const int FullTestQuestionCount = 40;

    /// <summary>Quy đổi số câu đúng của một section ngắn về thang 40 câu.</summary>
    public static int ScaleToFullTest(int correctCount, int totalQuestions, int fullTestQuestions = FullTestQuestionCount)
    {
        if (totalQuestions <= 0) return 0;

        int clampedCorrect = Math.Clamp(correctCount, 0, totalQuestions);
        int scaled = (int)Math.Round(
            clampedCorrect * (double)fullTestQuestions / totalQuestions,
            MidpointRounding.AwayFromZero);

        return Math.Clamp(scaled, 0, fullTestQuestions);
    }

    public static double ConvertListeningRawToBand(int rawScore)
    {
        return rawScore switch
        {
            >= 39 => 9.0,
            >= 37 => 8.5,
            >= 35 => 8.0,
            >= 32 => 7.5,
            >= 30 => 7.0,
            >= 26 => 6.5,
            >= 23 => 6.0,
            >= 18 => 5.5,
            >= 16 => 5.0,
            >= 13 => 4.5,
            >= 10 => 4.0,
            >= 7 => 3.5,
            >= 5 => 3.0,
            >= 3 => 2.5,
            >= 1 => 2.0,
            _ => 1.0   // 0 câu đúng: IELTS không có band 0 (tối thiểu 1.0 cho bài đã nộp)
        };
    }

    /// <summary>
    /// Quy đổi Reading. Bảng Academic và General Training KHÁC nhau:
    /// GT khắt khe hơn (cần nhiều câu đúng hơn để đạt cùng band).
    /// </summary>
    public static double ConvertReadingRawToBand(int rawScore, bool isAcademic = true)
    {
        if (!isAcademic)
        {
            // Bảng General Training (Cambridge).
            return rawScore switch
            {
                >= 40 => 9.0,
                >= 39 => 8.5,
                >= 37 => 8.0,
                >= 36 => 7.5,
                >= 34 => 7.0,
                >= 32 => 6.5,
                >= 30 => 6.0,
                >= 27 => 5.5,
                >= 23 => 5.0,
                >= 19 => 4.5,
                >= 15 => 4.0,
                >= 12 => 3.5,
                >= 9 => 3.0,
                >= 6 => 2.5,
                >= 3 => 2.0,
                >= 1 => 1.5,
                _ => 1.0
            };
        }

        // Bảng Academic.
        return rawScore switch
        {
            >= 39 => 9.0,
            >= 37 => 8.5,
            >= 35 => 8.0,
            >= 33 => 7.5,
            >= 30 => 7.0,
            >= 27 => 6.5,
            >= 23 => 6.0,
            >= 19 => 5.5,
            >= 15 => 5.0,
            >= 13 => 4.5,
            >= 10 => 4.0,
            >= 8 => 3.5,
            >= 6 => 3.0,
            >= 4 => 2.5,
            >= 2 => 2.0,
            >= 1 => 1.5,
            _ => 1.0
        };
    }

    /// <summary>Tra band Listening từ số câu đúng của một section bất kỳ (đã tự quy đổi về thang 40).</summary>
    public static double ConvertListeningRawToBandForSection(int correctCount, int totalQuestions)
        => ConvertListeningRawToBand(ScaleToFullTest(correctCount, totalQuestions));

    /// <summary>Tra band Reading từ số câu đúng của một section bất kỳ (đã tự quy đổi về thang 40).</summary>
    public static double ConvertReadingRawToBandForSection(int correctCount, int totalQuestions, bool isAcademic = true)
        => ConvertReadingRawToBand(ScaleToFullTest(correctCount, totalQuestions), isAcademic);

    public static double RoundToHalfBand(double score)
    {
        double decimalPart = score - Math.Floor(score);

        if (decimalPart < 0.25)
            return Math.Floor(score);
        if (decimalPart < 0.75)
            return Math.Floor(score) + 0.5;
        return Math.Ceiling(score);
    }

    public static double CalculateOverallBand(double l, double r, double w, double s)
    {
        double avg = (l + r + w + s) / 4.0;
        return RoundToHalfBand(avg);
    }
}
