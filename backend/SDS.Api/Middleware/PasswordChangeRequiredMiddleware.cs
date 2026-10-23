// Geçici parola kullanan kullanıcıların zorunlu şifre değişikliğini uygular.
using Microsoft.EntityFrameworkCore;
using SDS.Application.Security;
using SDS.Infrastructure.Persistence;

namespace SDS.Api.Middleware;

public sealed class PasswordChangeRequiredMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, SdsDbContext dbContext, ICurrentUser currentUser)
    {
        if (context.User.Identity?.IsAuthenticated == true && context.Request.Path.StartsWithSegments("/api") && !context.Request.Path.StartsWithSegments("/api/auth/change-password") && !context.Request.Path.StartsWithSegments("/api/auth/me"))
        {
            var userId = currentUser.Get().Id;
            if (userId.HasValue && await dbContext.Users.AsNoTracking().AnyAsync(item => item.Id == userId.Value && item.MustChangePassword, context.RequestAborted))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { success = false, data = (object?)null, message = "Devam etmek için önce şifrenizi değiştirmeniz gerekir.", errors = Array.Empty<string>() }, context.RequestAborted);
                return;
            }
        }
        await next(context);
    }
}
