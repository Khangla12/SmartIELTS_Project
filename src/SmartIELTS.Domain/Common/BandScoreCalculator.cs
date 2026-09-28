namespace SmartIELTS.Domain.Common;

public static class BandScoreCalculator
{
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
            _ => 2.0
        };
    }

    public static double ConvertReadingRawToBand(int rawScore, bool isAcademic = true)
    {
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
            _ => 2.0
        };
    }

    public static double CalculateOverallBand(double l, double r, double w, double s)
    {
        double avg = (l + r + w + s) / 4.0;
        double decimalPart = avg - Math.Floor(avg);

        if (decimalPart < 0.25)
            return Math.Floor(avg);
        if (decimalPart < 0.75)
            return Math.Floor(avg) + 0.5;
        return Math.Ceiling(avg);
    }
}
