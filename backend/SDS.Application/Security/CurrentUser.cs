// İstek sahibinin kimlik ve rol bilgisini uygulama katmanına taşır.
namespace SDS.Application.Security;

public sealed record CurrentUser(Guid? Id, string? Role)
{
    public bool IsAuthenticated => Id.HasValue;
    public bool IsAdministrator => Role is RoleNames.Admin or RoleNames.AdminInstructor;
}

public interface ICurrentUser
{
    CurrentUser Get();
}
