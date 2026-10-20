// Sınav cevap anahtarındaki mantıksal soru bilgilerini tanımlar.
namespace SDS.Domain.Entities;

public sealed class ExamQuestion
{
    public Guid Id { get; set; }
    public Guid ExamAnswerKeyId { get; set; }
    public int CanonicalQuestionNumber { get; set; }
    public decimal Points { get; set; }
    public ExamQuestionStatus Status { get; set; } = ExamQuestionStatus.Active;
    public ExamAnswerKey ExamAnswerKey { get; set; } = null!;
    public ICollection<BookletQuestion> BookletQuestions { get; set; } = new List<BookletQuestion>();
}

public enum ExamQuestionStatus
{
    Active,
    Cancelled
}
