// Yönetici panelinin istek ve yanıt modellerini tanımlar.
using System.ComponentModel.DataAnnotations;
using SDS.Domain.Entities;

namespace SDS.Application.Admin;

public sealed class CreateManagedUserRequest
{
    [Required, MaxLength(80)] public string FirstName { get; set; } = string.Empty;
    [Required, MaxLength(80)] public string LastName { get; set; } = string.Empty;
    [Required, EmailAddress, MaxLength(160)] public string Username { get; set; } = string.Empty;
    [MaxLength(32)] public string? PersonnelNumber { get; set; }
    [Required] public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class UpdateManagedUserRequest
{
    [Required, MaxLength(80)] public string FirstName { get; set; } = string.Empty;
    [Required, MaxLength(80)] public string LastName { get; set; } = string.Empty;
    [Required, EmailAddress, MaxLength(160)] public string Username { get; set; } = string.Empty;
    [MaxLength(32)] public string? PersonnelNumber { get; set; }
    [Required] public UserRole Role { get; set; }
    public bool IsActive { get; set; }
}

public sealed class UpdateGradingPolicyRequest
{
    [Range(0, 100)] public decimal AaMinimum { get; set; }
    [Range(0, 100)] public decimal BaMinimum { get; set; }
    [Range(0, 100)] public decimal BbMinimum { get; set; }
    [Range(0, 100)] public decimal CbMinimum { get; set; }
    [Range(0, 100)] public decimal CcMinimum { get; set; }
    [Range(0, 100)] public decimal DcMinimum { get; set; }
    [Range(0, 100)] public decimal MidtermWeight { get; set; }
    [Range(0, 100)] public decimal FinalWeight { get; set; }
    [Range(0, 100)] public decimal MakeUpWeight { get; set; }
}

public sealed class UpdateOpticalSystemSettingsRequest
{
    [Range(0, int.MaxValue)] public int StudentNumberStart { get; set; }
    [Range(1, int.MaxValue)] public int StudentNumberLength { get; set; }
    [Range(0, int.MaxValue)] public int? StudentNameStart { get; set; }
    [Range(1, int.MaxValue)] public int? StudentNameLength { get; set; }
    [Range(0, int.MaxValue)] public int BookletTypeStart { get; set; }
    [Range(1, int.MaxValue)] public int BookletTypeLength { get; set; }
    [Range(0, int.MaxValue)] public int? ExamTypeStart { get; set; }
    [Range(1, int.MaxValue)] public int? ExamTypeLength { get; set; }
    [Range(0, int.MaxValue)] public int AnswersStart { get; set; }
    [Range(1, int.MaxValue)] public int AnswersLength { get; set; }
    [Required, MaxLength(64)] public string EncodingName { get; set; } = "utf-8";
}

public sealed record ManagedUserResponse(Guid Id, string Username, string DisplayName, string? PersonnelNumber, string Role, bool IsActive, bool MustChangePassword);
public sealed record ManagedCourseResponse(Guid Id, string Code, string Name, bool IsAssigned);
public sealed record CreateManagedUserResponse(ManagedUserResponse User, string TemporaryPassword);
public sealed record PasswordResetRequestResponse(Guid Id, Guid UserId, string DisplayName, string Username, string Status, DateTime RequestedAt, DateTime? ProcessedAt);
public sealed record PasswordResetApprovalResponse(PasswordResetRequestResponse Request, string TemporaryPassword);
public sealed record GradingPolicyResponse(Guid Id, decimal AaMinimum, decimal BaMinimum, decimal BbMinimum, decimal CbMinimum, decimal CcMinimum, decimal DcMinimum, decimal MidtermWeight, decimal FinalWeight, decimal MakeUpWeight, DateTime UpdatedAt);
public sealed record OpticalSystemSettingsResponse(Guid Id, int StudentNumberStart, int StudentNumberLength, int? StudentNameStart, int? StudentNameLength, int BookletTypeStart, int BookletTypeLength, int? ExamTypeStart, int? ExamTypeLength, int AnswersStart, int AnswersLength, string EncodingName, DateTime UpdatedAt);
