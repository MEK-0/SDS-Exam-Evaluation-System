// Geliştirme ortamı için yerel demo verilerini oluşturur.
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SDS.Domain.Entities;
using SDS.Infrastructure.Persistence;

namespace SDS.Infrastructure.Seeding;

public sealed class DevelopmentSeeder(SdsDbContext dbContext, IConfiguration configuration)
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);
        if (await dbContext.Users.AnyAsync(cancellationToken)) return;
        var hasher = new PasswordHasher<User>();
        var admin = CreateUser(hasher, "admin", "SDS Yönetici", UserRole.Admin, GetRequiredPassword("Seed:AdminPassword"));
        var adminInstructor = CreateUser(hasher, "admin.instructor", "SDS Yönetici Eğitmen", UserRole.AdminInstructor, GetRequiredPassword("Seed:AdminInstructorPassword"));
        var instructor = CreateUser(hasher, "instructor", "Dr. Öğretim Üyesi Sude Deniz", UserRole.Instructor, GetRequiredPassword("Seed:InstructorPassword"));
        var year = new AcademicYear { Id = Guid.NewGuid(), Name = "2026-2027", IsActive = true };
        var firstCourse = CreateCourse("3301258", "Ayrık Matematik", year.Id);
        var secondCourse = CreateCourse("BM201", "Veri Yapıları", year.Id);
        dbContext.AddRange(admin, adminInstructor, instructor, year, firstCourse, secondCourse);
        dbContext.CourseInstructors.AddRange(new CourseInstructor { CourseId = firstCourse.Id, UserId = instructor.Id }, new CourseInstructor { CourseId = secondCourse.Id, UserId = adminInstructor.Id });
        dbContext.Exams.Add(new Exam { Id = Guid.NewGuid(), CourseId = firstCourse.Id, ExamType = ExamType.Midterm, EvaluationType = EvaluationType.MultipleChoice, Status = ExamStatus.Draft, CreatedAt = DateTime.UtcNow });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static User CreateUser(IPasswordHasher<User> hasher, string username, string displayName, UserRole role, string password)
    {
        var user = new User { Id = Guid.NewGuid(), Username = username, DisplayName = displayName, Role = role, CreatedAt = DateTime.UtcNow, MustChangePassword = false };
        user.PasswordHash = hasher.HashPassword(user, password);
        return user;
    }

    private string GetRequiredPassword(string key) => configuration[key] ?? throw new InvalidOperationException($"{key} yapılandırması bulunamadı.");

    private static Course CreateCourse(string code, string name, Guid academicYearId) => new() { Id = Guid.NewGuid(), Code = code, Name = name, AcademicYearId = academicYearId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
}
