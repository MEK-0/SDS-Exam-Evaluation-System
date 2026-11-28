// Kitapçık soru numarasını mantıksal soruya ve kabul edilen şıklara bağlar.
namespace SDS.Domain.Entities;

public sealed class BookletQuestion
{
    public Guid Id { get; set; }
    public Guid ExamBookletId { get; set; }
    public Guid ExamQuestionId { get; set; }
    public int DisplayQuestionNumber { get; set; }
    public string AcceptedAnswers { get; set; } = string.Empty;
    public decimal? Points { get; set; }
    public ExamQuestionStatus Status { get; set; } = ExamQuestionStatus.Active;
    public QuestionCancellationPolicy CancellationPolicy { get; set; } = QuestionCancellationPolicy.AwardFullPoints;
    public ExamBooklet ExamBooklet { get; set; } = null!;
    public ExamQuestion ExamQuestion { get; set; } = null!;
}

public enum QuestionCancellationPolicy
{
    AwardFullPoints,
    DeductAndScale
}
