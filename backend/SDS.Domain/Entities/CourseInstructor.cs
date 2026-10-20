// Ders ve eğitmen arasındaki ilişki varlığını tanımlar.
namespace SDS.Domain.Entities;

public sealed class CourseInstructor
{
    public Guid CourseId { get; set; }
    public Guid UserId { get; set; }
    public Course Course { get; set; } = null!;
    public User User { get; set; } = null!;
}
