// Dönem değerlendirme raporunun API modellerini ve hesaplama sözleşmesini tanımlar.
namespace SDS.Application.TermAnalysis;

public sealed record TermAnalysisResponse(TermAnalysisSummary Summary, IReadOnlyCollection<TermStudentResponse> Students, IReadOnlyCollection<TermLetterResponse> LetterDistribution, IReadOnlyCollection<TermScoreDistributionResponse> ScoreDistribution);
public sealed record TermAnalysisSummary(int TotalStudents, int ParticipatingStudents, decimal? WeightedAverage, decimal? SuccessRate, decimal? MidtermAverage, decimal? FinalAverage, decimal? MakeUpAverage, string EffectiveExamLabel, decimal MidtermWeight, decimal FinalWeight);
public sealed record TermStudentResponse(string StudentNumber, string StudentName, decimal? MidtermScore, decimal? FinalScore, decimal? MakeUpScore, decimal? EffectiveExamScore, decimal? WeightedScore, string? LetterGrade, string Status);
public sealed record TermLetterResponse(string Letter, int Count);
public sealed record TermScoreDistributionResponse(string Label, int Count);
public interface ITermAnalysisService
{
    Task<TermAnalysisResponse?> GetAsync(Guid courseId, CancellationToken cancellationToken);
    Task<byte[]?> ExportAsync(Guid courseId, CancellationToken cancellationToken);
}
