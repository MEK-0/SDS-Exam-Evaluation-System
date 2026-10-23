// Yönetici panelinin güvenli API endpointlerini sunar.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SDS.Application.Admin;
using SDS.Application.Common;
using SDS.Application.Security;

namespace SDS.Api.Controllers;

[ApiController]
[Authorize(Roles = RoleNames.Management)]
[Route("api/admin")]
public sealed class AdminController(IAdminService adminService) : ControllerBase
{
    [HttpGet("users")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<ManagedUserResponse>>>> Users([FromQuery] string? search, CancellationToken cancellationToken) => Ok(ApiResponse<IReadOnlyCollection<ManagedUserResponse>>.Ok(await adminService.GetUsersAsync(search, cancellationToken)));

    [HttpPost("users")]
    public async Task<ActionResult<ApiResponse<CreateManagedUserResponse>>> CreateUser(CreateManagedUserRequest request, CancellationToken cancellationToken) => Ok(ApiResponse<CreateManagedUserResponse>.Ok(await adminService.CreateUserAsync(request, cancellationToken)));

    [HttpPut("users/{id:guid}")]
    public async Task<ActionResult<ApiResponse<ManagedUserResponse>>> UpdateUser(Guid id, UpdateManagedUserRequest request, CancellationToken cancellationToken)
    {
        var response = await adminService.UpdateUserAsync(id, request, cancellationToken);
        return response is null ? NotFound(ApiResponse<ManagedUserResponse>.Fail("Kullanıcı bulunamadı.")) : Ok(ApiResponse<ManagedUserResponse>.Ok(response));
    }

    [HttpGet("users/{id:guid}/courses")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<ManagedCourseResponse>>>> UserCourses(Guid id, CancellationToken cancellationToken) => Ok(ApiResponse<IReadOnlyCollection<ManagedCourseResponse>>.Ok(await adminService.GetUserCoursesAsync(id, cancellationToken)));

    [HttpPost("users/{userId:guid}/courses/{courseId:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> AssignCourse(Guid userId, Guid courseId, CancellationToken cancellationToken) => await adminService.AssignCourseAsync(userId, courseId, cancellationToken) ? Ok(ApiResponse<object>.Ok(new { }, "Ders atandı.")) : NotFound(ApiResponse<object>.Fail("Kullanıcı veya ders bulunamadı."));

    [HttpDelete("users/{userId:guid}/courses/{courseId:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> RemoveCourse(Guid userId, Guid courseId, CancellationToken cancellationToken) => await adminService.RemoveCourseAsync(userId, courseId, cancellationToken) ? Ok(ApiResponse<object>.Ok(new { }, "Ders ataması kaldırıldı.")) : NotFound(ApiResponse<object>.Fail("Ders ataması bulunamadı."));

    [HttpGet("password-reset-requests")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<PasswordResetRequestResponse>>>> PasswordResetRequests([FromQuery] string? status, CancellationToken cancellationToken) => Ok(ApiResponse<IReadOnlyCollection<PasswordResetRequestResponse>>.Ok(await adminService.GetPasswordResetRequestsAsync(status, cancellationToken)));

    [HttpPost("password-reset-requests/{id:guid}/approve")]
    public async Task<ActionResult<ApiResponse<PasswordResetApprovalResponse>>> ApprovePasswordReset(Guid id, CancellationToken cancellationToken) => Ok(ApiResponse<PasswordResetApprovalResponse>.Ok(await adminService.ApprovePasswordResetAsync(id, cancellationToken)));

    [HttpPost("password-reset-requests/{id:guid}/reject")]
    public async Task<ActionResult<ApiResponse<PasswordResetRequestResponse>>> RejectPasswordReset(Guid id, CancellationToken cancellationToken)
    {
        var response = await adminService.RejectPasswordResetAsync(id, cancellationToken);
        return response is null ? NotFound(ApiResponse<PasswordResetRequestResponse>.Fail("Talep bulunamadı.")) : Ok(ApiResponse<PasswordResetRequestResponse>.Ok(response));
    }

    [HttpGet("grading-policy")]
    public async Task<ActionResult<ApiResponse<GradingPolicyResponse>>> GetGradingPolicy(CancellationToken cancellationToken) => Ok(ApiResponse<GradingPolicyResponse>.Ok(await adminService.GetGradingPolicyAsync(cancellationToken)));

    [HttpPut("grading-policy")]
    public async Task<ActionResult<ApiResponse<GradingPolicyResponse>>> UpdateGradingPolicy(UpdateGradingPolicyRequest request, CancellationToken cancellationToken) => Ok(ApiResponse<GradingPolicyResponse>.Ok(await adminService.UpdateGradingPolicyAsync(request, cancellationToken)));

    [HttpGet("optical-form-settings")]
    public async Task<ActionResult<ApiResponse<OpticalSystemSettingsResponse>>> GetOpticalSettings(CancellationToken cancellationToken) => Ok(ApiResponse<OpticalSystemSettingsResponse>.Ok(await adminService.GetOpticalSettingsAsync(cancellationToken)));

    [HttpPut("optical-form-settings")]
    public async Task<ActionResult<ApiResponse<OpticalSystemSettingsResponse>>> UpdateOpticalSettings(UpdateOpticalSystemSettingsRequest request, CancellationToken cancellationToken) => Ok(ApiResponse<OpticalSystemSettingsResponse>.Ok(await adminService.UpdateOpticalSettingsAsync(request, cancellationToken)));
}
