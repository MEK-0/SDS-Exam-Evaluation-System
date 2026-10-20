// API sağlık kontrolü endpoint'ini sunar.
using Microsoft.AspNetCore.Mvc;
using SDS.Application.Health;

namespace SDS.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController(IHealthService healthService) : ControllerBase
{
    [HttpGet]
    public ActionResult<HealthStatus> Get() => Ok(healthService.GetStatus());
}
