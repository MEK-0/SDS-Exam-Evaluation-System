// Sınav sorusu ile ders öğrenme çıktısı arasındaki ilişkiyi tanımlar.
namespace SDS.Domain.Entities;

public sealed class QuestionLearningOutcome
{
    public Guid Id { get; set; }
    public Guid ExamId { get; set; }
    public Guid ExamQuestionId { get; set; }
    public Guid LearningOutcomeId { get; set; }
    public Exam Exam { get; set; } = null!;
    public ExamQuestion ExamQuestion { get; set; } = null!;
    public LearningOutcome LearningOutcome { get; set; } = null!;
}
