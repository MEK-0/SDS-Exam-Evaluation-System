// Dönem puanı için etkin sınavı ve ağırlıklı puanı hesaplar.
namespace SDS.Application.TermAnalysis;

public static class TermAnalysisCalculator
{
    public static (decimal? Effective, string Label, decimal? Weighted) Calculate(decimal? midterm, decimal? final, decimal? makeUp, decimal midtermWeight = 40, decimal finalWeight = 60)
    {
        var effective = makeUp ?? final;
        var label = makeUp.HasValue ? "Bütünleme" : "Final";
        decimal? weighted = midterm.HasValue && effective.HasValue ? decimal.Round(midterm.Value * midtermWeight / 100 + effective.Value * finalWeight / 100, 2, MidpointRounding.AwayFromZero) : null;
        return (effective, effective.HasValue ? label : "-", weighted);
    }
}
