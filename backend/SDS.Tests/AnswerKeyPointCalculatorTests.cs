// Eşit soru puanı hesaplayıcısının toplam ve hassasiyet kurallarını doğrular.
using System.Linq;
using SDS.Application.AnswerKeys;
using Xunit;

namespace SDS.Tests;

public sealed class AnswerKeyPointCalculatorTests
{
    [Theory]
    [InlineData(20, 5)]
    [InlineData(25, 4)]
    [InlineData(40, 2.5)]
    [InlineData(50, 2)]
    public void Calculate_ReturnsEqualQuestionPoints(decimal questionCount, decimal expectedPoint)
    {
        var points = AnswerKeyPointCalculator.Calculate(100, (int)questionCount);
        Assert.All(points, point => Assert.Equal(expectedPoint, point));
        Assert.Equal(100, points.Sum());
    }

    [Fact]
    public void Calculate_PreservesTotalForRepeatingDecimal()
    {
        var points = AnswerKeyPointCalculator.Calculate(100, 3);
        Assert.Equal(100, points.Sum());
        Assert.Equal(33.3334m, points[^1]);
    }
}
