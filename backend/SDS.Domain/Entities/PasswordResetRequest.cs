// Şifre sıfırlama talebinin durumunu ve işlem geçmişini tanımlar.
namespace SDS.Domain.Entities;

public sealed class PasswordResetRequest
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public PasswordResetRequestStatus Status { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public Guid? ProcessedByUserId { get; set; }
    public User User { get; set; } = null!;
    public User? ProcessedByUser { get; set; }
}

public enum PasswordResetRequestStatus
{
    Pending,
    Approved,
    Rejected
}
