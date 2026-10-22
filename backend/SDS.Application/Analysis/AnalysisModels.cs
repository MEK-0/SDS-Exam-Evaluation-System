// Sınav başarı analizinin API modellerini tanımlar.
namespace SDS.Application.Analysis;

public sealed record AnalysisResponse(
    AnalysisSummaryResponse Summary,
    AnalysisGeneralResponse General,
    IReadOnlyCollection<AnalysisStudentResponse> Students,
    AnalysisStudentDetailResponse? SelectedStudent,
    IReadOnlyCollection<ScoreDistributionResponse> ScoreDistribution,
    IReadOnlyCollection<QuestionAnalysisResponse> Questions,
    IReadOnlyCollection<ChoiceDistributionResponse> Choices,
    IReadOnlyCollection<LearningOutcomeAnalysisResponse> LearningOutcomes);

public sealed record AnalysisSummaryResponse(int TotalStudents, int ParticipatingStudents, decimal? Average, decimal? Median, decimal? StandardDeviation, decimal? Minimum, decimal? Maximum, decimal? SuccessRate, decimal? SuccessThreshold, string? SuccessMessage);
public sealed record AnalysisGeneralResponse(string CourseCode, string CourseName, string ExamType, string ExamFormat, int QuestionCount, decimal MaximumScore);
public sealed record AnalysisStudentResponse(Guid StudentId, string StudentNumber, string StudentName, string Status, decimal? Score);
public sealed record AnalysisStudentDetailResponse(Guid StudentId, string StudentNumber, string StudentName, string Status, string? BookletCode, decimal? RawScore, decimal? AdjustedScore, int CorrectCount, int WrongCount, int BlankCount, IReadOnlyCollection<StudentQuestionResponse> Questions);
public sealed record StudentQuestionResponse(int QuestionNumber, string StudentAnswer, string AcceptedAnswer, bool IsCorrect, bool IsBlank);
public sealed record ScoreDistributionResponse(string Label, int Count);
public sealed record QuestionAnalysisResponse(int QuestionNumber, decimal SuccessRate, int CorrectCount, int ParticipatingCount);
public sealed record ChoiceDistributionResponse(int QuestionNumber, string BookletCode, IReadOnlyCollection<ChoiceResponse> Choices, int BlankCount, int ParticipatingCount);
public sealed record ChoiceResponse(string Option, int Count, decimal Percentage, bool IsCorrect);
public sealed record LearningOutcomeAnalysisResponse(Guid Id, string Description, IReadOnlyCollection<int> QuestionNumbers, decimal? SuccessRate);

public interface IAnalysisService
{
    Task<AnalysisResponse?> GetAsync(Guid examId, Guid? studentId, CancellationToken cancellationToken);
    Task<byte[]?> ExportAsync(Guid examId, CancellationToken cancellationToken);
}
