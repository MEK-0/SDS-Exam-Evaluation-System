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
        var hasher = new PasswordHasher<User>();
        var resetPasswords = bool.TryParse(configuration["Seed:ResetPasswords"], out var shouldResetPasswords) && shouldResetPasswords;
        var admin = await GetOrCreateUserAsync(hasher, "admin@sds.local", "admin", "SDS", "Yönetici", "10000001", UserRole.Admin, GetRequiredPassword("Seed:AdminPassword"), resetPasswords, cancellationToken);
        var adminInstructor = await GetOrCreateUserAsync(hasher, "admin.instructor@sds.local", "admin.instructor", "SDS", "Yönetici Eğitmen", "10000002", UserRole.AdminInstructor, GetRequiredPassword("Seed:AdminInstructorPassword"), resetPasswords, cancellationToken);
        var instructor = await GetOrCreateUserAsync(hasher, "instructor@sds.local", "instructor", "Sude", "Deniz", "10000003", UserRole.Instructor, GetRequiredPassword("Seed:InstructorPassword"), resetPasswords, cancellationToken);
        var year = await dbContext.AcademicYears.SingleOrDefaultAsync(item => item.Name == "2026-2027", cancellationToken) ?? new AcademicYear { Id = Guid.NewGuid(), Name = "2026-2027", IsActive = true };
        var firstCourse = await GetOrCreateCourseAsync("3301258", "Ayrık Matematik", year.Id, cancellationToken);
        var secondCourse = await GetOrCreateCourseAsync("BM201", "Veri Yapıları", year.Id, cancellationToken);
        if (dbContext.Entry(year).State == EntityState.Detached) dbContext.AcademicYears.Add(year);
        if (!await dbContext.CourseInstructors.AnyAsync(item => item.CourseId == firstCourse.Id && item.UserId == instructor.Id, cancellationToken)) dbContext.CourseInstructors.Add(new CourseInstructor { CourseId = firstCourse.Id, UserId = instructor.Id });
        if (!await dbContext.CourseInstructors.AnyAsync(item => item.CourseId == secondCourse.Id && item.UserId == adminInstructor.Id, cancellationToken)) dbContext.CourseInstructors.Add(new CourseInstructor { CourseId = secondCourse.Id, UserId = adminInstructor.Id });
        if (!await dbContext.Exams.AnyAsync(item => item.CourseId == firstCourse.Id && item.ExamType == ExamType.Midterm, cancellationToken)) dbContext.Exams.Add(new Exam { Id = Guid.NewGuid(), CourseId = firstCourse.Id, ExamType = ExamType.Midterm, EvaluationType = EvaluationType.MultipleChoice, Status = ExamStatus.Draft, CreatedAt = DateTime.UtcNow });
        if (!await dbContext.GradingPolicies.AnyAsync(cancellationToken)) dbContext.GradingPolicies.Add(new GradingPolicy { Id = Guid.NewGuid(), AaMinimum = 90, BaMinimum = 85, BbMinimum = 80, CbMinimum = 70, CcMinimum = 60, DcMinimum = 50, MidtermWeight = 40, FinalWeight = 60, MakeUpWeight = 60, UpdatedAt = DateTime.UtcNow });
        if (!await dbContext.OpticalSystemSettings.AnyAsync(cancellationToken)) dbContext.OpticalSystemSettings.Add(new OpticalSystemSettings { Id = Guid.NewGuid(), StudentNumberStart = 0, StudentNumberLength = 9, BookletTypeStart = 9, BookletTypeLength = 1, AnswersStart = 10, AnswersLength = 25, EncodingName = "utf-8", UpdatedAt = DateTime.UtcNow });
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<User> GetOrCreateUserAsync(IPasswordHasher<User> hasher, string username, string legacyUsername, string firstName, string lastName, string personnelNumber, UserRole role, string password, bool resetPassword, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users.SingleOrDefaultAsync(item => item.Username == username || item.Username == legacyUsername, cancellationToken);
        if (user is null)
        {
            user = new User { Id = Guid.NewGuid(), Username = username, CreatedAt = DateTime.UtcNow, MustChangePassword = false };
            user.PasswordHash = hasher.HashPassword(user, password);
            dbContext.Users.Add(user);
        }
        if (resetPassword) user.PasswordHash = hasher.HashPassword(user, password);
        user.FirstName = firstName;
        user.LastName = lastName;
        user.DisplayName = $"{firstName} {lastName}";
        user.Username = username;
        user.PersonnelNumber = personnelNumber;
        user.Role = role;
        user.IsActive = true;
        return user;
    }

    private string GetRequiredPassword(string key) => configuration[key] ?? throw new InvalidOperationException($"{key} yapılandırması bulunamadı.");

    private static Course CreateCourse(string code, string name, Guid academicYearId) => new() { Id = Guid.NewGuid(), Code = code, Name = name, AcademicYearId = academicYearId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };

    private async Task<Course> GetOrCreateCourseAsync(string code, string name, Guid academicYearId, CancellationToken cancellationToken)
    {
        var course = await dbContext.Courses.SingleOrDefaultAsync(item => item.AcademicYearId == academicYearId && item.Code == code, cancellationToken);
        if (course is not null) return course;
        course = CreateCourse(code, name, academicYearId);
        dbContext.Courses.Add(course);
        return course;
    }
}
