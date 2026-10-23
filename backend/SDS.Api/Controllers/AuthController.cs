// Kimlik doğrulama endpointlerini sunar.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SDS.Application.Auth;
using SDS.Application.Common;
using SDS.Application.Security;
using SDS.Application.Admin;

namespace SDS.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService, IAdminService adminService, ICurrentUser currentUser) : ControllerBase
{
    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var response = await authService.LoginAsync(request, cancellationToken);
        return response is null ? Unauthorized(ApiResponse<LoginResponse>.Fail("Kullanıcı adı veya şifre hatalı.")) : Ok(ApiResponse<LoginResponse>.Ok(response));
    }

    [AllowAnonymous]
    [HttpPost("forgot-password")]
    public async Task<ActionResult<ApiResponse<object>>> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        await adminService.CreatePasswordResetRequestAsync(request.Username, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { }, "Talebiniz alındı. Hesabınız varsa yönetici incelemesinden sonra işlem yapılacaktır."));
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<ActionResult<ApiResponse<object>>> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var user = currentUser.Get();
        if (!user.Id.HasValue) return Unauthorized(ApiResponse<object>.Fail("Oturum bulunamadı."));
        return await authService.ChangePasswordAsync(user.Id.Value, request, cancellationToken) ? Ok(ApiResponse<object>.Ok(new { }, "Şifreniz güncellendi.")) : BadRequest(ApiResponse<object>.Fail("Mevcut şifre hatalı."));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<ApiResponse<UserResponse>>> Me(CancellationToken cancellationToken)
    {
        var user = currentUser.Get();
        if (!user.Id.HasValue) return Unauthorized(ApiResponse<UserResponse>.Fail("Oturum bulunamadı."));
        var response = await authService.GetCurrentUserAsync(user.Id.Value, cancellationToken);
        return response is null ? NotFound(ApiResponse<UserResponse>.Fail("Kullanıcı bulunamadı.")) : Ok(ApiResponse<UserResponse>.Ok(response));
    }
}
