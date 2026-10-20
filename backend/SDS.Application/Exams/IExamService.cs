// Sınav uygulama servisi sözleşmesini tanımlar.
namespace SDS.Application.Exams;

public interface IExamService
{
    Task<IReadOnlyCollection<ExamResponse>?> GetCourseExamsAsync(Guid courseId, CancellationToken cancellationToken);
    Task<ExamResponse?> GetExamAsync(Guid examId, CancellationToken cancellationToken);
    Task<ExamResponse?> CreateExamAsync(Guid courseId, CreateExamRequest request, CancellationToken cancellationToken);
}
