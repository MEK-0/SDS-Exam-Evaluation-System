// Açık uçlu sınav ayarları ve ham not giriş modellerini tanımlar.
using System.ComponentModel.DataAnnotations;
using SDS.Application.Grades;

namespace SDS.Application.OpenEnded;

public sealed record OpenEndedSettingsResponse(Guid ExamId, int QuestionCount, decimal MaximumScore, DateTime UpdatedAt);

public sealed class SaveOpenEndedSettingsRequest
{
    [Range(1, 200)]
    public int QuestionCount { get; set; }

    public decimal MaximumScore { get; set; }
}

public sealed class OpenEndedGradeRequest
{
    [Range(typeof(decimal), "0", "1000000")]
    public decimal? RawScore { get; set; }

    public bool IsAbsent { get; set; }
}

public sealed record OpenEndedGradeImportResponse(GradesResponse Grades, int ImportedCount, IReadOnlyCollection<string> Issues);

public interface IOpenEndedService
{
    Task<OpenEndedSettingsResponse?> GetSettingsAsync(Guid examId, CancellationToken cancellationToken);
    Task<OpenEndedSettingsResponse?> SaveSettingsAsync(Guid examId, SaveOpenEndedSettingsRequest request, CancellationToken cancellationToken);
    Task<GradesResponse?> SaveGradeAsync(Guid examId, Guid studentId, OpenEndedGradeRequest request, CancellationToken cancellationToken);
    Task<OpenEndedGradeImportResponse?> ImportGradesAsync(Guid examId, Stream content, string fileName, CancellationToken cancellationToken);
}
