// Harf notu sınırlarının merkezi hesaplama kuralını doğrular.
using SDS.Application.Grades;
using Xunit;

namespace SDS.Tests;

public sealed class GradeLetterCalculatorTests
{
    [Theory]
    [InlineData(100, "AA")]
    [InlineData(88, "AA")]
    [InlineData(87, "BA")]
    [InlineData(80, "BA")]
    [InlineData(79, "BB")]
    [InlineData(73, "BB")]
    [InlineData(72, "CB")]
    [InlineData(66, "CB")]
    [InlineData(65, "CC")]
    [InlineData(60, "CC")]
    [InlineData(59, "DC")]
    [InlineData(50, "DC")]
    [InlineData(49, "FF")]
    [InlineData(0, "FF")]
    public void CalculatesExpectedLetterGrade(decimal score, string expected)
    {
        Assert.Equal(expected, GradeLetterCalculator.Calculate(score));
    }

    [Fact]
    public void ReturnsNullForMissingScore()
    {
        Assert.Null(GradeLetterCalculator.Calculate(null));
    }

    [Fact]
    public void KK7_H12_LetterUsesPolicyThresholds()
    {
        var thresholds = new GradeThresholds(95, 85, 80, 70, 60, 50);
        Assert.Equal("BA", GradeLetterCalculator.Calculate(90, thresholds));
        Assert.Equal("AA", GradeLetterCalculator.Calculate(95, thresholds));
        Assert.Equal("CB", GradeLetterCalculator.Calculate(70, thresholds));
        Assert.Equal("BB", GradeLetterCalculator.Calculate(79.99m, new GradeThresholds(90, 85, 75, 70, 60, 50)));
        Assert.Equal("FF", GradeLetterCalculator.Calculate(49.99m, thresholds));
        Assert.Null(GradeLetterCalculator.Calculate(null, thresholds));
    }
}
