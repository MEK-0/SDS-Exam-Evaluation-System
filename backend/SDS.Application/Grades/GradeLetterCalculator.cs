// Harf notunu yönetimde kaydedilen eşiklerle hesaplar.
namespace SDS.Application.Grades;

public sealed record GradeThresholds(decimal Aa, decimal Ba, decimal Bb, decimal Cb, decimal Cc, decimal Dc)
{
    public static GradeThresholds SeedDefault { get; } = new(90, 85, 80, 70, 60, 50);
}

public static class GradeLetterCalculator
{
    public static string? Calculate(decimal? score, GradeThresholds thresholds)
    {
        if (!score.HasValue) return null;
        var value = score.Value;
        if (value >= thresholds.Aa) return "AA";
        if (value >= thresholds.Ba) return "BA";
        if (value >= thresholds.Bb) return "BB";
        if (value >= thresholds.Cb) return "CB";
        if (value >= thresholds.Cc) return "CC";
        if (value >= thresholds.Dc) return "DC";
        return "FF";
    }
}
