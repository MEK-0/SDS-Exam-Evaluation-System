// Kimlik doğrulama ve JWT token üretimini gerçekleştirir.
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using SDS.Application.Auth;
using SDS.Domain.Entities;
using SDS.Infrastructure.Persistence;

namespace SDS.Infrastructure.Services;

public sealed class AuthService(SdsDbContext dbContext, IConfiguration configuration) : IAuthService
{
    private readonly PasswordHasher<User> passwordHasher = new();

    public async Task<LoginResponse?> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(item => item.Username == request.Username.Trim(), cancellationToken);
        if (user is null || !user.IsActive || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed) return null;
        var expiresAt = DateTime.UtcNow.AddMinutes(GetExpirationMinutes());
        var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()), new Claim(ClaimTypes.Name, user.Username), new Claim(ClaimTypes.Role, user.Role.ToString()) };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(GetRequiredSetting("Jwt:Key")));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(GetRequiredSetting("Jwt:Issuer"), GetRequiredSetting("Jwt:Audience"), claims, expires: expiresAt, signingCredentials: credentials);
        return new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), expiresAt, user.MustChangePassword, ToResponse(user));
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null || passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed) return false;
        user.PasswordHash = passwordHasher.HashPassword(user, request.NewPassword);
        user.MustChangePassword = false;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<UserResponse?> GetCurrentUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        return user is null ? null : ToResponse(user);
    }

    private string GetRequiredSetting(string key) => configuration[key] ?? throw new InvalidOperationException($"{key} yapılandırması bulunamadı.");
    private int GetExpirationMinutes() => int.TryParse(configuration["Jwt:ExpirationMinutes"], out var value) ? value : 30;
    private static UserResponse ToResponse(User user) => new(user.Id, user.Username, user.DisplayName, user.Role.ToString());
}
