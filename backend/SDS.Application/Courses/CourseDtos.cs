// Ders ve akademik yıl istek/yanıt modellerini tanımlar.
using System.ComponentModel.DataAnnotations;

namespace SDS.Application.Courses;

public sealed record AcademicYearResponse(Guid Id, string Name, bool IsActive);

public class CreateCourseRequest
{
    [Required, StringLength(32)]
    public string Code { get; set; } = string.Empty;

    [Required, StringLength(160)]
    public string Name { get; set; } = string.Empty;

    [Required]
    public Guid AcademicYearId { get; set; }
}

public sealed class UpdateCourseRequest : CreateCourseRequest;
public sealed record CourseResponse(Guid Id, string Code, string Name, Guid AcademicYearId, string AcademicYear, int InstructorCount, int ExamCount);
public sealed record AssignInstructorRequest(Guid UserId);
