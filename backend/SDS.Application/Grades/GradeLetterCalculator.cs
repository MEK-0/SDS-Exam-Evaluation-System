// Merkezi harf notu aralıklarını hesaplar.
namespace SDS.Application.Grades;

public static class GradeLetterCalculator
{
    public static string? Calculate(decimal? score)
    {
        if (!score.HasValue) return null;
        return score.Value switch
        {
            >= 88 => "AA",
            >= 80 => "BA",
            >= 73 => "BB",
            >= 66 => "CB",
            >= 60 => "CC",
            >= 50 => "DC",
            _ => "FF"
        };
    }
}
