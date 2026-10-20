// SDS akademik yıl varlığını tanımlar.
namespace SDS.Domain.Entities;

public sealed class AcademicYear
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public ICollection<Course> Courses { get; set; } = new List<Course>();
}
