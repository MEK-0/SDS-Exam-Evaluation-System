// Not hesaplama, düzeltme ve proje notu işlemlerinin API modellerini tanımlar.
using SDS.Domain.Entities;

namespace SDS.Application.Grades;

public sealed record GradeRowResponse(Guid ResultId, Guid ImportedStudentId, string StudentNumber, string StudentName, decimal? RawScore, decimal? AdjustedScore, decimal AdjustmentAmount, decimal? ProjectScore, GradeStatus Status, DateTime UpdatedAt, string? LetterGrade);
public sealed record GradeSummaryResponse(int TotalStudentCount, int GradedStudentCount, int AbsentStudentCount, decimal? AverageScore, decimal? MedianScore, decimal? StandardDeviation, decimal? HighestScore, decimal? LowestScore);
public sealed record GradesResponse(IReadOnlyCollection<GradeRowResponse> Rows, GradeSummaryResponse Summary, DateTime? CalculatedAt);
public sealed record GradeOperationResponse(GradesResponse Grades, IReadOnlyCollection<string> Issues);

public sealed class UpdateGradeRequest
{
    public decimal AdjustedScore { get; set; }
}

public sealed class BonusRequest
{
    public List<Guid> ImportedStudentIds { get; set; } = [];
    public decimal Amount { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
}

public sealed record ProjectGradeImportResponse(GradesResponse Grades, int ImportedCount, IReadOnlyCollection<string> Issues);

public interface IGradeService
{
    Task<GradesResponse?> GetAsync(Guid examId, CancellationToken cancellationToken);
    Task<GradesResponse?> CalculateAsync(Guid examId, CancellationToken cancellationToken);
    Task<GradesResponse?> UpdateAsync(Guid examId, Guid importedStudentId, UpdateGradeRequest request, CancellationToken cancellationToken);
    Task<GradesResponse?> AddBonusAsync(Guid examId, BonusRequest request, CancellationToken cancellationToken);
    Task<ProjectGradeImportResponse?> ImportProjectGradesAsync(Guid examId, Stream content, string fileName, CancellationToken cancellationToken);
    Task<GradeSummaryResponse?> GetSummaryAsync(Guid examId, CancellationToken cancellationToken);
}
