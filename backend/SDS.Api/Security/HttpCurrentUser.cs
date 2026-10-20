// HTTP isteğindeki JWT claim bilgilerini uygulama kimliğine dönüştürür.
using System.Security.Claims;
using SDS.Application.Security;

namespace SDS.Api.Security;

public sealed class HttpCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    public CurrentUser Get()
    {
        var principal = httpContextAccessor.HttpContext?.User;
        var idValue = principal?.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal?.FindFirstValue("sub");
        return Guid.TryParse(idValue, out var id) ? new CurrentUser(id, principal?.FindFirstValue(ClaimTypes.Role)) : new CurrentUser(null, null);
    }
}
