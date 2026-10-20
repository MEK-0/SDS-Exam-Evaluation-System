// SDS ders varlığını tanımlar.
namespace SDS.Domain.Entities;

public sealed class Course
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid AcademicYearId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;
    public ICollection<CourseInstructor> Instructors { get; set; } = new List<CourseInstructor>();
    public ICollection<Exam> Exams { get; set; } = new List<Exam>();
}
