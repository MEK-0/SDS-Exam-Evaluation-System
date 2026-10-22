// Öğrenci proje notunun sınavdan bağımsız doğrulanmış kaydını tanımlar.
namespace SDS.Domain.Entities;

public sealed class ProjectGradeImport
{
    public Guid Id { get; set; }
    public Guid ExamId { get; set; }
    public Guid ImportedStudentId { get; set; }
    public decimal ProjectScore { get; set; }
    public string SourceFileName { get; set; } = string.Empty;
    public int RowNumber { get; set; }
    public DateTime ImportedAt { get; set; }
    public Exam Exam { get; set; } = null!;
    public ImportedStudent ImportedStudent { get; set; } = null!;
}
