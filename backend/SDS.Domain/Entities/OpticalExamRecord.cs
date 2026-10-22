// Optik dosyasındaki tek bir öğrencinin ayrıştırılmış cevap kaydını tanımlar.
namespace SDS.Domain.Entities;

public sealed class OpticalExamRecord
{
    public Guid Id { get; set; }
    public Guid ExamImportId { get; set; }
    public int LineNumber { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string StudentName { get; set; } = string.Empty;
    public string BookletType { get; set; } = string.Empty;
    public string Answers { get; set; } = string.Empty;
    public bool IsValid { get; set; }
    public bool IsAnswerKey { get; set; }
    public string? ErrorMessage { get; set; }
    public ExamImport ExamImport { get; set; } = null!;
}
