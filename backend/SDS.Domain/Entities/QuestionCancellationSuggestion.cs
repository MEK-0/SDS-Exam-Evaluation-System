// Bir kitapçıkta iptal edilen sorunun diğer kitapçıklardaki karşılığı için iptal önerisini ve kararını tutar.
namespace SDS.Domain.Entities;

public sealed class QuestionCancellationSuggestion
{
    public Guid Id { get; set; }
    public Guid ExamId { get; set; }
    public string SourceBookletCode { get; set; } = string.Empty;
    public int SourceQuestionNumber { get; set; }
    public string TargetBookletCode { get; set; } = string.Empty;
    public int TargetQuestionNumber { get; set; }
    public QuestionCancellationPolicy Policy { get; set; } = QuestionCancellationPolicy.AwardFullPoints;
    public CancellationSuggestionStatus Status { get; set; } = CancellationSuggestionStatus.Pending;
    public DateTime CreatedAt { get; set; }
    public DateTime? DecidedAt { get; set; }
    public Guid? DecidedByUserId { get; set; }
    public Exam Exam { get; set; } = null!;
}

public enum CancellationSuggestionStatus
{
    Pending,
    Approved,
    Rejected
}
