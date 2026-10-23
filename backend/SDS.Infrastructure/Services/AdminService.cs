// Yönetici panelinin kullanıcı, politika ve optik ayar işlemlerini yürütür.
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SDS.Application.Admin;
using SDS.Application.Security;
using SDS.Domain.Entities;
using SDS.Infrastructure.Persistence;

namespace SDS.Infrastructure.Services;

public sealed class AdminService(SdsDbContext dbContext, ICurrentUser currentUser) : IAdminService
{
    private readonly PasswordHasher<User> passwordHasher = new();

    public async Task<IReadOnlyCollection<ManagedUserResponse>> GetUsersAsync(string? search, CancellationToken cancellationToken)
    {
        await EnsureAdministratorAsync(cancellationToken);
        var query = dbContext.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            var value = search.Trim().ToLower();
            query = query.Where(item => item.Username.ToLower().Contains(value) || item.DisplayName.ToLower().Contains(value) || (item.PersonnelNumber != null && item.PersonnelNumber.ToLower().Contains(value)));
        }
        return await query.OrderBy(item => item.DisplayName).Select(ToUserResponseExpression()).ToListAsync(cancellationToken);
    }

    public async Task<CreateManagedUserResponse> CreateUserAsync(CreateManagedUserRequest request, CancellationToken cancellationToken)
    {
        await EnsureAdministratorAsync(cancellationToken);
        var username = request.Username.Trim().ToLowerInvariant();
        if (await dbContext.Users.AnyAsync(item => item.Username == username, cancellationToken)) throw new InvalidOperationException("Bu e-posta zaten kayıtlı.");
        if (!string.IsNullOrWhiteSpace(request.PersonnelNumber) && await dbContext.Users.AnyAsync(item => item.PersonnelNumber == request.PersonnelNumber.Trim(), cancellationToken)) throw new InvalidOperationException("Bu personel numarası zaten kayıtlı.");
        var user = new User { Id = Guid.NewGuid(), Username = username, FirstName = request.FirstName.Trim(), LastName = request.LastName.Trim(), DisplayName = $"{request.FirstName.Trim()} {request.LastName.Trim()}", PersonnelNumber = string.IsNullOrWhiteSpace(request.PersonnelNumber) ? null : request.PersonnelNumber.Trim(), Role = request.Role, IsActive = request.IsActive, MustChangePassword = true, CreatedAt = DateTime.UtcNow };
        var temporaryPassword = CreateTemporaryPassword();
        user.PasswordHash = passwordHasher.HashPassword(user, temporaryPassword);
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new CreateManagedUserResponse(ToUserResponse(user), temporaryPassword);
    }

    public async Task<ManagedUserResponse?> UpdateUserAsync(Guid userId, UpdateManagedUserRequest request, CancellationToken cancellationToken)
    {
        var actor = await EnsureAdministratorAsync(cancellationToken);
        var user = await dbContext.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null) return null;
        var username = request.Username.Trim().ToLowerInvariant();
        if (await dbContext.Users.AnyAsync(item => item.Id != userId && item.Username == username, cancellationToken)) throw new InvalidOperationException("Bu e-posta zaten kayıtlı.");
        if (user.Id == actor.Id && (!request.IsActive || request.Role is not UserRole.Admin and not UserRole.AdminInstructor)) throw new InvalidOperationException("Kendi yönetici yetkinizi veya hesabınızı pasifleştiremezsiniz.");
        if (user.Role is UserRole.Admin or UserRole.AdminInstructor && (!request.IsActive || request.Role is not UserRole.Admin and not UserRole.AdminInstructor) && await dbContext.Users.CountAsync(item => item.IsActive && (item.Role == UserRole.Admin || item.Role == UserRole.AdminInstructor), cancellationToken) <= 1) throw new InvalidOperationException("Son aktif sistem yöneticisinin yetkisi kaldırılamaz.");
        if (!string.IsNullOrWhiteSpace(request.PersonnelNumber) && await dbContext.Users.AnyAsync(item => item.Id != userId && item.PersonnelNumber == request.PersonnelNumber.Trim(), cancellationToken)) throw new InvalidOperationException("Bu personel numarası zaten kayıtlı.");
        user.FirstName = request.FirstName.Trim();
        user.LastName = request.LastName.Trim();
        user.Username = username;
        user.DisplayName = $"{user.FirstName} {user.LastName}";
        user.PersonnelNumber = string.IsNullOrWhiteSpace(request.PersonnelNumber) ? null : request.PersonnelNumber.Trim();
        user.Role = request.Role;
        user.IsActive = request.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToUserResponse(user);
    }

    public async Task<IReadOnlyCollection<ManagedCourseResponse>> GetUserCoursesAsync(Guid userId, CancellationToken cancellationToken)
    {
        await EnsureAdministratorAsync(cancellationToken);
        if (!await dbContext.Users.AnyAsync(item => item.Id == userId, cancellationToken)) throw new ArgumentException("Kullanıcı bulunamadı.");
        return await dbContext.Courses.AsNoTracking().OrderBy(item => item.Code).Select(item => new ManagedCourseResponse(item.Id, item.Code, item.Name, item.Instructors.Any(instructor => instructor.UserId == userId))).ToListAsync(cancellationToken);
    }

    public async Task<bool> AssignCourseAsync(Guid userId, Guid courseId, CancellationToken cancellationToken)
    {
        await EnsureAdministratorAsync(cancellationToken);
        var user = await dbContext.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken);
        if (user is null || user.Role == UserRole.Admin) return false;
        if (!await dbContext.Courses.AnyAsync(item => item.Id == courseId, cancellationToken)) return false;
        if (!await dbContext.CourseInstructors.AnyAsync(item => item.UserId == userId && item.CourseId == courseId, cancellationToken)) dbContext.CourseInstructors.Add(new CourseInstructor { UserId = userId, CourseId = courseId });
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> RemoveCourseAsync(Guid userId, Guid courseId, CancellationToken cancellationToken)
    {
        await EnsureAdministratorAsync(cancellationToken);
        var assignment = await dbContext.CourseInstructors.SingleOrDefaultAsync(item => item.UserId == userId && item.CourseId == courseId, cancellationToken);
        if (assignment is null) return false;
        dbContext.CourseInstructors.Remove(assignment);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyCollection<PasswordResetRequestResponse>> GetPasswordResetRequestsAsync(string? status, CancellationToken cancellationToken)
    {
        await EnsureAdministratorAsync(cancellationToken);
        var query = dbContext.PasswordResetRequests.AsNoTracking().Include(item => item.User).AsQueryable();
        if (Enum.TryParse<PasswordResetRequestStatus>(status, true, out var parsed)) query = query.Where(item => item.Status == parsed);
        return await query.OrderByDescending(item => item.RequestedAt).Select(item => new PasswordResetRequestResponse(item.Id, item.UserId, item.User.DisplayName, item.User.Username, item.Status.ToString(), item.RequestedAt, item.ProcessedAt)).ToListAsync(cancellationToken);
    }

    public async Task<PasswordResetApprovalResponse> ApprovePasswordResetAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var actor = await EnsureAdministratorAsync(cancellationToken);
        var request = await dbContext.PasswordResetRequests.Include(item => item.User).SingleOrDefaultAsync(item => item.Id == requestId, cancellationToken) ?? throw new ArgumentException("Şifre sıfırlama talebi bulunamadı.");
        if (request.Status != PasswordResetRequestStatus.Pending) throw new InvalidOperationException("Bu talep daha önce sonuçlandırılmış.");
        var temporaryPassword = CreateTemporaryPassword();
        request.User.PasswordHash = passwordHasher.HashPassword(request.User, temporaryPassword);
        request.User.MustChangePassword = true;
        request.Status = PasswordResetRequestStatus.Approved;
        request.ProcessedAt = DateTime.UtcNow;
        request.ProcessedByUserId = actor.Id;
        await dbContext.SaveChangesAsync(cancellationToken);
        return new PasswordResetApprovalResponse(ToResetResponse(request), temporaryPassword);
    }

    public async Task<PasswordResetRequestResponse?> RejectPasswordResetAsync(Guid requestId, CancellationToken cancellationToken)
    {
        var actor = await EnsureAdministratorAsync(cancellationToken);
        var request = await dbContext.PasswordResetRequests.Include(item => item.User).SingleOrDefaultAsync(item => item.Id == requestId, cancellationToken);
        if (request is null) return null;
        if (request.Status != PasswordResetRequestStatus.Pending) throw new InvalidOperationException("Bu talep daha önce sonuçlandırılmış.");
        request.Status = PasswordResetRequestStatus.Rejected;
        request.ProcessedAt = DateTime.UtcNow;
        request.ProcessedByUserId = actor.Id;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToResetResponse(request);
    }

    public async Task<GradingPolicyResponse> GetGradingPolicyAsync(CancellationToken cancellationToken)
    {
        await EnsureAdministratorAsync(cancellationToken);
        var policy = await GetOrCreatePolicyAsync(cancellationToken);
        return ToPolicyResponse(policy);
    }

    public async Task<GradingPolicyResponse> UpdateGradingPolicyAsync(UpdateGradingPolicyRequest request, CancellationToken cancellationToken)
    {
        var actor = await EnsureAdministratorAsync(cancellationToken);
        ValidatePolicy(request);
        var policy = await GetOrCreatePolicyAsync(cancellationToken);
        policy.AaMinimum = request.AaMinimum; policy.BaMinimum = request.BaMinimum; policy.BbMinimum = request.BbMinimum; policy.CbMinimum = request.CbMinimum; policy.CcMinimum = request.CcMinimum; policy.DcMinimum = request.DcMinimum; policy.MidtermWeight = request.MidtermWeight; policy.FinalWeight = request.FinalWeight; policy.MakeUpWeight = request.MakeUpWeight; policy.UpdatedAt = DateTime.UtcNow; policy.UpdatedByUserId = actor.Id;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToPolicyResponse(policy);
    }

    public async Task<OpticalSystemSettingsResponse> GetOpticalSettingsAsync(CancellationToken cancellationToken)
    {
        await EnsureAdministratorAsync(cancellationToken);
        return ToOpticalResponse(await GetOrCreateOpticalSettingsAsync(cancellationToken));
    }

    public async Task<OpticalSystemSettingsResponse> UpdateOpticalSettingsAsync(UpdateOpticalSystemSettingsRequest request, CancellationToken cancellationToken)
    {
        var actor = await EnsureAdministratorAsync(cancellationToken);
        ValidateOpticalSettings(request);
        var settings = await GetOrCreateOpticalSettingsAsync(cancellationToken);
        settings.StudentNumberStart = request.StudentNumberStart; settings.StudentNumberLength = request.StudentNumberLength; settings.StudentNameStart = request.StudentNameStart; settings.StudentNameLength = request.StudentNameLength; settings.BookletTypeStart = request.BookletTypeStart; settings.BookletTypeLength = request.BookletTypeLength; settings.ExamTypeStart = request.ExamTypeStart; settings.ExamTypeLength = request.ExamTypeLength; settings.AnswersStart = request.AnswersStart; settings.AnswersLength = request.AnswersLength; settings.EncodingName = request.EncodingName.Trim(); settings.UpdatedAt = DateTime.UtcNow; settings.UpdatedByUserId = actor.Id;
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToOpticalResponse(settings);
    }

    public async Task<bool> CreatePasswordResetRequestAsync(string username, CancellationToken cancellationToken)
    {
        var normalized = username.Trim().ToLowerInvariant();
        var user = await dbContext.Users.SingleOrDefaultAsync(item => item.Username == normalized && item.IsActive, cancellationToken);
        if (user is null) return true;
        if (!await dbContext.PasswordResetRequests.AnyAsync(item => item.UserId == user.Id && item.Status == PasswordResetRequestStatus.Pending, cancellationToken)) dbContext.PasswordResetRequests.Add(new PasswordResetRequest { Id = Guid.NewGuid(), UserId = user.Id, Status = PasswordResetRequestStatus.Pending, RequestedAt = DateTime.UtcNow });
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<User> EnsureAdministratorAsync(CancellationToken cancellationToken)
    {
        var userId = currentUser.Get().Id ?? throw new UnauthorizedAccessException("Oturum bulunamadı.");
        var user = await dbContext.Users.SingleOrDefaultAsync(item => item.Id == userId && item.IsActive, cancellationToken) ?? throw new UnauthorizedAccessException("Kullanıcı bulunamadı.");
        if (user.Role is not UserRole.Admin and not UserRole.AdminInstructor) throw new UnauthorizedAccessException("Bu işlem için yönetici yetkisi gerekir.");
        return user;
    }

    private async Task<GradingPolicy> GetOrCreatePolicyAsync(CancellationToken cancellationToken)
    {
        var policy = await dbContext.GradingPolicies.SingleOrDefaultAsync(cancellationToken);
        if (policy is not null) return policy;
        policy = new GradingPolicy { Id = Guid.NewGuid(), AaMinimum = 90, BaMinimum = 85, BbMinimum = 80, CbMinimum = 70, CcMinimum = 60, DcMinimum = 50, MidtermWeight = 40, FinalWeight = 60, MakeUpWeight = 60, UpdatedAt = DateTime.UtcNow };
        dbContext.GradingPolicies.Add(policy);
        await dbContext.SaveChangesAsync(cancellationToken);
        return policy;
    }

    private async Task<OpticalSystemSettings> GetOrCreateOpticalSettingsAsync(CancellationToken cancellationToken)
    {
        var settings = await dbContext.OpticalSystemSettings.SingleOrDefaultAsync(cancellationToken);
        if (settings is not null) return settings;
        settings = new OpticalSystemSettings { Id = Guid.NewGuid(), StudentNumberStart = 0, StudentNumberLength = 9, BookletTypeStart = 9, BookletTypeLength = 1, AnswersStart = 10, AnswersLength = 25, UpdatedAt = DateTime.UtcNow };
        dbContext.OpticalSystemSettings.Add(settings);
        await dbContext.SaveChangesAsync(cancellationToken);
        return settings;
    }

    private static void ValidatePolicy(UpdateGradingPolicyRequest request)
    {
        if (!(request.AaMinimum > request.BaMinimum && request.BaMinimum > request.BbMinimum && request.BbMinimum > request.CbMinimum && request.CbMinimum > request.CcMinimum && request.CcMinimum > request.DcMinimum)) throw new ArgumentException("Harf notu aralıkları büyükten küçüğe sıralı ve çakışmasız olmalıdır.");
        if (request.MidtermWeight + request.FinalWeight != 100 || request.MakeUpWeight != request.FinalWeight) throw new ArgumentException("Vize ve final ağırlıkları toplamı 100 olmalı, bütünleme final ağırlığına eşit olmalıdır.");
    }

    private static void ValidateOpticalSettings(UpdateOpticalSystemSettingsRequest request)
    {
        if ((request.StudentNameStart.HasValue != request.StudentNameLength.HasValue) || (request.ExamTypeStart.HasValue != request.ExamTypeLength.HasValue)) throw new ArgumentException("İsteğe bağlı optik alanların başlangıç ve uzunluk değerleri birlikte girilmelidir.");
        if (request.BookletTypeStart < request.StudentNumberStart + request.StudentNumberLength && request.BookletTypeStart + request.BookletTypeLength > request.StudentNumberStart) throw new ArgumentException("Optik alanlar çakışamaz.");
        if (!System.Text.Encoding.GetEncodings().Any(item => item.Name.Equals(request.EncodingName.Trim(), StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Geçersiz dosya encoding değeri.");
    }

    private static string CreateTemporaryPassword() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(18)).Replace("+", "A").Replace("/", "b").Replace("=", "9");
    private static ManagedUserResponse ToUserResponse(User user) => new(user.Id, user.Username, user.DisplayName, user.PersonnelNumber, user.Role.ToString(), user.IsActive, user.MustChangePassword);
    private static PasswordResetRequestResponse ToResetResponse(PasswordResetRequest item) => new(item.Id, item.UserId, item.User.DisplayName, item.User.Username, item.Status.ToString(), item.RequestedAt, item.ProcessedAt);
    private static GradingPolicyResponse ToPolicyResponse(GradingPolicy item) => new(item.Id, item.AaMinimum, item.BaMinimum, item.BbMinimum, item.CbMinimum, item.CcMinimum, item.DcMinimum, item.MidtermWeight, item.FinalWeight, item.MakeUpWeight, item.UpdatedAt);
    private static OpticalSystemSettingsResponse ToOpticalResponse(OpticalSystemSettings item) => new(item.Id, item.StudentNumberStart, item.StudentNumberLength, item.StudentNameStart, item.StudentNameLength, item.BookletTypeStart, item.BookletTypeLength, item.ExamTypeStart, item.ExamTypeLength, item.AnswersStart, item.AnswersLength, item.EncodingName, item.UpdatedAt);
    private static System.Linq.Expressions.Expression<Func<User, ManagedUserResponse>> ToUserResponseExpression() => item => new ManagedUserResponse(item.Id, item.Username, item.DisplayName, item.PersonnelNumber, item.Role.ToString(), item.IsActive, item.MustChangePassword);
}
