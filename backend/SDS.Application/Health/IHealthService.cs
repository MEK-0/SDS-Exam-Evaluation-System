// Uygulamanın sağlık kontrolü için gereken sözleşmeyi tanımlar.
namespace SDS.Application.Health;

public interface IHealthService
{
    HealthStatus GetStatus();
}

public sealed record HealthStatus(string Status, string Service, DateTime UtcTime);
