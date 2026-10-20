// Sağlık servisi davranışını doğrular.
using SDS.Application.Health;
using Xunit;

namespace SDS.Tests;

public sealed class HealthServiceTests
{
    [Fact]
    public void GetStatus_ReturnsHealthyStatus()
    {
        var result = new HealthService().GetStatus();
        Assert.Equal("Sağlıklı", result.Status);
        Assert.Equal("SDS API", result.Service);
    }
}
