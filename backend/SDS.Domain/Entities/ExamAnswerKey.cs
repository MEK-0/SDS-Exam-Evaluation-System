// Sınavın cevap anahtarı ayarlarını ve doğrulama durumunu tanımlar.
namespace SDS.Domain.Entities;

public sealed class ExamAnswerKey
{
    public Guid Id { get; set; }
    public Guid ExamId { get; set; }
    public decimal MaximumScore { get; set; }
    public int QuestionCount { get; set; }
    public bool DynamicScoring { get; set; }
    public bool AllowMultipleAnswers { get; set; }
    public CancelledQuestionPolicy CancelledQuestionPolicy { get; set; } = CancelledQuestionPolicy.ExcludeFromTotal;
    public AnswerKeyStatus Status { get; set; } = AnswerKeyStatus.Draft;
    public DateTime UpdatedAt { get; set; }
    public Exam Exam { get; set; } = null!;
    public ICollection<ExamBooklet> Booklets { get; set; } = new List<ExamBooklet>();
    public ICollection<ExamQuestion> Questions { get; set; } = new List<ExamQuestion>();
}

public enum AnswerKeyStatus
{
    Draft,
    Validated
}

public enum CancelledQuestionPolicy
{
    ExcludeFromTotal,
    RedistributeToActiveQuestions
}
