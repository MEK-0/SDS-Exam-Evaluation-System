// Öğrenme çıktısı ve sınav ilişkilendirme API modellerini tanımlar.
using System.ComponentModel.DataAnnotations;

namespace SDS.Application.LearningOutcomes;

public sealed record LearningOutcomeResponse(Guid Id, Guid CourseId, string Description, int DisplayOrder, DateTime UpdatedAt);
public sealed record BookletQuestionMappingResponse(string ReferenceBookletCode, string BookletCode, IReadOnlyCollection<BookletQuestionMappingItem> Items);
public sealed record BookletQuestionMappingItem(int ReferenceQuestionNumber, int BookletQuestionNumber);
public sealed record LearningOutcomeQuestionMapResponse(Guid LearningOutcomeId, IReadOnlyCollection<int> QuestionNumbers);
public sealed record LearningOutcomePageResponse(IReadOnlyCollection<LearningOutcomeResponse> Outcomes, IReadOnlyCollection<BookletQuestionMappingResponse> BookletMappings, IReadOnlyCollection<LearningOutcomeQuestionMapResponse> QuestionMappings, int QuestionCount, bool IsMultipleChoice, bool IsComplete);

public sealed class CreateLearningOutcomeRequest
{
    [Required, StringLength(2000, MinimumLength = 1)]
    public string Description { get; set; } = string.Empty;
}

public sealed class SaveBookletQuestionMappingsRequest
{
    public List<BookletMappingInput> Mappings { get; set; } = [];
}

public sealed class BookletMappingInput
{
    public string ReferenceBookletCode { get; set; } = string.Empty;
    public string BookletCode { get; set; } = string.Empty;
    public List<BookletQuestionMappingItem> Items { get; set; } = [];
}

public sealed class SaveLearningOutcomeMappingsRequest
{
    public List<LearningOutcomeQuestionMapInput> Mappings { get; set; } = [];
}

public sealed class LearningOutcomeQuestionMapInput
{
    public Guid LearningOutcomeId { get; set; }
    public List<int> QuestionNumbers { get; set; } = [];
}

public sealed class CompleteExamRequest
{
    public SaveBookletQuestionMappingsRequest BookletMappings { get; set; } = new();
    public SaveLearningOutcomeMappingsRequest LearningOutcomeMappings { get; set; } = new();
}

public interface ILearningOutcomeService
{
    Task<IReadOnlyCollection<LearningOutcomeResponse>?> GetCourseOutcomesAsync(Guid courseId, CancellationToken cancellationToken);
    Task<LearningOutcomeResponse?> CreateOutcomeAsync(Guid courseId, CreateLearningOutcomeRequest request, CancellationToken cancellationToken);
    Task<LearningOutcomePageResponse?> GetExamPageAsync(Guid examId, CancellationToken cancellationToken);
    Task<LearningOutcomePageResponse?> SaveBookletMappingsAsync(Guid examId, SaveBookletQuestionMappingsRequest request, CancellationToken cancellationToken);
    Task<LearningOutcomePageResponse?> SaveOutcomeMappingsAsync(Guid examId, SaveLearningOutcomeMappingsRequest request, CancellationToken cancellationToken);
    Task<LearningOutcomePageResponse?> CompleteExamAsync(Guid examId, CompleteExamRequest request, CancellationToken cancellationToken);
}
