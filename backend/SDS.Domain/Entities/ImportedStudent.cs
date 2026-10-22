// Sınıf listesinden import edilen öğrencinin sınav içi kaydını tanımlar.
namespace SDS.Domain.Entities;

public sealed class ImportedStudent
{
    public Guid Id { get; set; }
    public Guid ExamImportId { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string SourceFileName { get; set; } = string.Empty;
    public int RowNumber { get; set; }
    public ExamImport ExamImport { get; set; } = null!;
}
