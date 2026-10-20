// Bir sınav için yüklenen optik ve sınıf listesi verilerinin import kaydını tanımlar.
namespace SDS.Domain.Entities;

public sealed class ExamImport
{
    public Guid Id { get; set; }
    public Guid ExamId { get; set; }
    public string OpticalFileName { get; set; } = string.Empty;
    public string ClassListFileName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int TotalOpticalRecordCount { get; set; }
    public int ValidOpticalRecordCount { get; set; }
    public int InvalidOpticalRecordCount { get; set; }
    public Exam Exam { get; set; } = null!;
    public ICollection<ImportedStudent> ImportedStudents { get; set; } = new List<ImportedStudent>();
    public ICollection<OpticalExamRecord> OpticalExamRecords { get; set; } = new List<OpticalExamRecord>();
    public ICollection<ImportValidationIssue> ValidationIssues { get; set; } = new List<ImportValidationIssue>();
}
