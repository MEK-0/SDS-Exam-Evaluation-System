// Uygulamanın sağlık kontrolü sonucunu üretir.
namespace SDS.Application.Health;

public sealed class HealthService : IHealthService
{
    public HealthStatus GetStatus() => new("Sağlıklı", "SDS API", DateTime.UtcNow);
}
