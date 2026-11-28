// Cevap anahtarı istek, yanıt ve doğrulama modellerini tanımlar.
using SDS.Domain.Entities;

namespace SDS.Application.AnswerKeys;

public sealed class UpdateAnswerKeyRequest
{
    public decimal MaximumScore { get; set; }
    public int QuestionCount { get; set; }
    public bool DynamicScoring { get; set; }
    public bool AllowMultipleAnswers { get; set; }
    public CancelledQuestionPolicy CancelledQuestionPolicy { get; set; } = CancelledQuestionPolicy.ExcludeFromTotal;
    public List<AnswerKeyQuestionRequest> Questions { get; set; } = [];
    public List<AnswerKeyBookletRequest> Booklets { get; set; } = [];
}

public sealed class AnswerKeyQuestionRequest
{
    public int CanonicalQuestionNumber { get; set; }
    public decimal Points { get; set; }
    public ExamQuestionStatus Status { get; set; } = ExamQuestionStatus.Active;
    public QuestionScoreSource Source { get; set; } = QuestionScoreSource.Optical;
}

public sealed class AnswerKeyBookletRequest
{
    public string BookletCode { get; set; } = string.Empty;
    public List<BookletQuestionRequest> Questions { get; set; } = [];
}

public sealed class BookletQuestionRequest
{
    public int ExamQuestionNumber { get; set; }
    public int DisplayQuestionNumber { get; set; }
    public List<string> AcceptedAnswers { get; set; } = [];
    public ExamQuestionStatus? Status { get; set; }
    public QuestionCancellationPolicy? CancellationPolicy { get; set; }
}

public sealed record AnswerKeyResponse(Guid Id, Guid ExamId, decimal MaximumScore, int QuestionCount, bool DynamicScoring, bool AllowMultipleAnswers, CancelledQuestionPolicy CancelledQuestionPolicy, AnswerKeyStatus Status, DateTime UpdatedAt, bool IsComplete, IReadOnlyCollection<AnswerKeyQuestionResponse> Questions, IReadOnlyCollection<AnswerKeyBookletResponse> Booklets, IReadOnlyCollection<string> ValidationIssues, IReadOnlyCollection<CancellationSuggestionResponse> CancellationSuggestions, int PendingCancellationSuggestionCount);
public sealed record AnswerKeyQuestionResponse(Guid Id, int CanonicalQuestionNumber, decimal Points, ExamQuestionStatus Status, QuestionScoreSource Source);
public sealed record AnswerKeyBookletResponse(Guid Id, string BookletCode, int QuestionCount, IReadOnlyCollection<BookletQuestionResponse> Questions);
public sealed record BookletQuestionResponse(Guid Id, Guid ExamQuestionId, int ExamQuestionNumber, int DisplayQuestionNumber, IReadOnlyCollection<string> AcceptedAnswers, decimal Points, ExamQuestionStatus Status, QuestionCancellationPolicy CancellationPolicy);
public sealed record CancellationSuggestionResponse(Guid Id, string SourceBookletCode, int SourceQuestionNumber, string TargetBookletCode, int TargetQuestionNumber, QuestionCancellationPolicy Policy, CancellationSuggestionStatus Status, DateTime CreatedAt, DateTime? DecidedAt);
public sealed record CancellationSuggestionsResponse(IReadOnlyCollection<CancellationSuggestionResponse> Items, int PendingCount);

public interface IAnswerKeyService
{
    Task<AnswerKeyResponse?> GetAsync(Guid examId, CancellationToken cancellationToken);
    Task<AnswerKeyResponse?> UpdateAsync(Guid examId, UpdateAnswerKeyRequest request, CancellationToken cancellationToken);
}

public interface ICancellationSuggestionService
{
    Task<CancellationSuggestionsResponse?> GetAsync(Guid examId, CancellationToken cancellationToken);
    Task<CancellationSuggestionsResponse?> ApproveAsync(Guid examId, Guid suggestionId, CancellationToken cancellationToken);
    Task<CancellationSuggestionsResponse?> RejectAsync(Guid examId, Guid suggestionId, CancellationToken cancellationToken);
}
