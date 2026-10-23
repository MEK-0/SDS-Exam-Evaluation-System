// SDS kullanıcı varlığını tanımlar.
namespace SDS.Domain.Entities;

public sealed class User
{
    public Guid Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PersonnelNumber { get; set; }
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public bool MustChangePassword { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public ICollection<CourseInstructor> CourseInstructors { get; set; } = new List<CourseInstructor>();
}

public enum UserRole
{
    Admin,
    AdminInstructor,
    Instructor
}
