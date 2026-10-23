// Yönetici işlemleri için uygulama servisi sözleşmesini tanımlar.
namespace SDS.Application.Admin;

public interface IAdminService
{
    Task<IReadOnlyCollection<ManagedUserResponse>> GetUsersAsync(string? search, CancellationToken cancellationToken);
    Task<CreateManagedUserResponse> CreateUserAsync(CreateManagedUserRequest request, CancellationToken cancellationToken);
    Task<ManagedUserResponse?> UpdateUserAsync(Guid userId, UpdateManagedUserRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ManagedCourseResponse>> GetUserCoursesAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> AssignCourseAsync(Guid userId, Guid courseId, CancellationToken cancellationToken);
    Task<bool> RemoveCourseAsync(Guid userId, Guid courseId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<PasswordResetRequestResponse>> GetPasswordResetRequestsAsync(string? status, CancellationToken cancellationToken);
    Task<PasswordResetApprovalResponse> ApprovePasswordResetAsync(Guid requestId, CancellationToken cancellationToken);
    Task<PasswordResetRequestResponse?> RejectPasswordResetAsync(Guid requestId, CancellationToken cancellationToken);
    Task<GradingPolicyResponse> GetGradingPolicyAsync(CancellationToken cancellationToken);
    Task<GradingPolicyResponse> UpdateGradingPolicyAsync(UpdateGradingPolicyRequest request, CancellationToken cancellationToken);
    Task<OpticalSystemSettingsResponse> GetOpticalSettingsAsync(CancellationToken cancellationToken);
    Task<OpticalSystemSettingsResponse> UpdateOpticalSettingsAsync(UpdateOpticalSystemSettingsRequest request, CancellationToken cancellationToken);
    Task<bool> CreatePasswordResetRequestAsync(string username, CancellationToken cancellationToken);
}
