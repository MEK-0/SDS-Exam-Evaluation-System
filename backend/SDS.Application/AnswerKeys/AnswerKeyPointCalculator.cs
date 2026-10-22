// Sınav sorularının eşit puan dağılımını merkezi olarak hesaplar.
namespace SDS.Application.AnswerKeys;

public static class AnswerKeyPointCalculator
{
    public static IReadOnlyList<decimal> Calculate(decimal maximumScore, int questionCount)
    {
        if (maximumScore <= 0 || questionCount <= 0) throw new ArgumentException("Puan ve soru sayısı pozitif olmalıdır.");
        var basePoint = decimal.Round(maximumScore / questionCount, 4, MidpointRounding.AwayFromZero);
        var points = Enumerable.Repeat(basePoint, questionCount).ToArray();
        points[^1] = maximumScore - points.Take(questionCount - 1).Sum();
        return points;
    }
}
