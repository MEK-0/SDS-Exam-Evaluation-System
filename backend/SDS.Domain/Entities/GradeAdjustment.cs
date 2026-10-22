// Sınav sonucundaki manuel puan ve bonus değişikliklerini denetim kaydıyla tanımlar.
namespace SDS.Domain.Entities;

public sealed class GradeAdjustment
{
    public Guid Id { get; set; }
    public Guid ExamResultId { get; set; }
    public Guid UserId { get; set; }
    public GradeAdjustmentType Type { get; set; }
    public decimal Amount { get; set; }
    public decimal PreviousScore { get; set; }
    public decimal NewScore { get; set; }
    public string? IdempotencyKey { get; set; }
    public DateTime CreatedAt { get; set; }
    public ExamResult ExamResult { get; set; } = null!;
    public User User { get; set; } = null!;
}

public enum GradeAdjustmentType
{
    ManualOverride,
    Bonus
}
