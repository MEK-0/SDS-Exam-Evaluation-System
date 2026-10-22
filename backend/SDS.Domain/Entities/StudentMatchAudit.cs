// Manuel öğrenci eşleştirme değişikliklerinin denetim kaydını tanımlar.
namespace SDS.Domain.Entities;

public sealed class StudentMatchAudit
{
    public Guid Id { get; set; }
    public Guid ExamId { get; set; }
    public Guid? StudentMatchId { get; set; }
    public Guid? PreviousImportedStudentId { get; set; }
    public Guid? PreviousOpticalExamRecordId { get; set; }
    public Guid? NewImportedStudentId { get; set; }
    public Guid? NewOpticalExamRecordId { get; set; }
    public Guid UserId { get; set; }
    public DateTime OccurredAt { get; set; }
    public string Action { get; set; } = string.Empty;
    public Exam Exam { get; set; } = null!;
    public StudentMatch? StudentMatch { get; set; }
    public User User { get; set; } = null!;
}
