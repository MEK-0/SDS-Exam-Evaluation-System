// Dönem puanı için final ve bütünleme ağırlık kurallarını doğrular.
using SDS.Application.TermAnalysis;
using Xunit;

namespace SDS.Tests;

public sealed class TermAnalysisCalculatorTests
{
    [Fact]
    public void UsesFinalWhenMakeUpIsMissing()
    {
        var result = TermAnalysisCalculator.Calculate(70, 80, null);
        Assert.Equal(80, result.Effective);
        Assert.Equal("Final", result.Label);
        Assert.Equal(76, result.Weighted);
    }

    [Fact]
    public void UsesMakeUpWhenPresent()
    {
        var result = TermAnalysisCalculator.Calculate(70, 40, 90);
        Assert.Equal(90, result.Effective);
        Assert.Equal("Bütünleme", result.Label);
        Assert.Equal(82, result.Weighted);
    }

    [Fact]
    public void LeavesWeightedScoreEmptyWhenAnExamIsMissing()
    {
        var result = TermAnalysisCalculator.Calculate(null, 80, null);
        Assert.Null(result.Weighted);
    }
}
