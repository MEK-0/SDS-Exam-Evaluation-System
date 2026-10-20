// Kitapçık soru numarasını mantıksal soruya ve kabul edilen şıklara bağlar.
namespace SDS.Domain.Entities;

public sealed class BookletQuestion
{
    public Guid Id { get; set; }
    public Guid ExamBookletId { get; set; }
    public Guid ExamQuestionId { get; set; }
    public int DisplayQuestionNumber { get; set; }
    public string AcceptedAnswers { get; set; } = string.Empty;
    public ExamBooklet ExamBooklet { get; set; } = null!;
    public ExamQuestion ExamQuestion { get; set; } = null!;
}
