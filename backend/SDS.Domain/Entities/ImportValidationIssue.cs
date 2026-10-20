// Dosya importu sırasında oluşan satır bazlı doğrulama sorununu tanımlar.
namespace SDS.Domain.Entities;

public sealed class ImportValidationIssue
{
    public Guid Id { get; set; }
    public Guid ExamImportId { get; set; }
    public string Source { get; set; } = string.Empty;
    public int? RowNumber { get; set; }
    public string Message { get; set; } = string.Empty;
    public ExamImport ExamImport { get; set; } = null!;
}
