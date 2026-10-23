// Kimlik doğrulama istek ve yanıt modellerini tanımlar.
using System.ComponentModel.DataAnnotations;

namespace SDS.Application.Auth;

public sealed class LoginRequest
{
    [Required]
    public string Username { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public sealed class ChangePasswordRequest
{
    [Required]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string NewPassword { get; set; } = string.Empty;
}

public sealed class ForgotPasswordRequest
{
    [Required] public string Username { get; set; } = string.Empty;
}

public sealed record LoginResponse(string AccessToken, DateTime ExpiresAt, bool MustChangePassword, UserResponse User);
public sealed record UserResponse(Guid Id, string Username, string DisplayName, string Role, bool MustChangePassword);
