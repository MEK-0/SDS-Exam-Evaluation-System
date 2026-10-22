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
}
