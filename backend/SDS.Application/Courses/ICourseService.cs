// Ders ve akademik yıl uygulama servisi sözleşmelerini tanımlar.
namespace SDS.Application.Courses;

public interface ICourseService
{
    Task<IReadOnlyCollection<AcademicYearResponse>> GetAcademicYearsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyCollection<CourseResponse>> GetCoursesAsync(CancellationToken cancellationToken);
    Task<CourseResponse?> GetCourseAsync(Guid courseId, CancellationToken cancellationToken);
    Task<CourseResponse> CreateCourseAsync(CreateCourseRequest request, CancellationToken cancellationToken);
    Task<CourseResponse?> UpdateCourseAsync(Guid courseId, UpdateCourseRequest request, CancellationToken cancellationToken);
    Task<bool> AssignInstructorAsync(Guid courseId, AssignInstructorRequest request, CancellationToken cancellationToken);
}
