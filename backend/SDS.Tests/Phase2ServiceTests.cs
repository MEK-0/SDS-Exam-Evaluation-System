// Phase 2 kimlik, yetki, ders ve sınav servislerini doğrular.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using SDS.Application.Auth;
using SDS.Application.Courses;
using SDS.Application.Exams;
using SDS.Application.Security;
using SDS.Domain.Entities;
using SDS.Infrastructure.Persistence;
using SDS.Infrastructure.Services;
using Xunit;

namespace SDS.Tests;

public sealed class Phase2ServiceTests
{
    [Fact]
    public async Task Login_WithValidCredentials_ReturnsToken()
    {
        await using var context = CreateContext();
        AddUser(context, "instructor", "Instructor123!", UserRole.Instructor);
        var service = new AuthService(context, CreateConfiguration());
        var result = await service.LoginAsync(new LoginRequest { Username = "instructor", Password = "Instructor123!" }, default);
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
    }

    [Fact]
    public async Task Login_WithInvalidCredentials_ReturnsNull()
    {
        await using var context = CreateContext();
        AddUser(context, "instructor", "Instructor123!", UserRole.Instructor);
        var service = new AuthService(context, CreateConfiguration());
        var result = await service.LoginAsync(new LoginRequest { Username = "instructor", Password = "yanlis" }, default);
        Assert.Null(result);
    }

    [Fact]
    public async Task ChangePassword_ClearsFirstLoginRequirement()
    {
        await using var context = CreateContext();
        var user = AddUser(context, "instructor", "Instructor123!", UserRole.Instructor);
        user.MustChangePassword = true;
        await context.SaveChangesAsync();
        var service = new AuthService(context, CreateConfiguration());
        var result = await service.ChangePasswordAsync(user.Id, new ChangePasswordRequest { CurrentPassword = "Instructor123!", NewPassword = "NewPassword123!" }, default);
        Assert.True(result);
        Assert.False((await context.Users.SingleAsync()).MustChangePassword);
    }

    [Fact]
    public async Task Instructor_CannotReadAnotherCourse()
    {
        await using var context = CreateContext();
        var year = AddYear(context);
        var instructor = AddUser(context, "instructor", "Instructor123!", UserRole.Instructor);
        var course = new Course { Id = Guid.NewGuid(), Code = "BM201", Name = "Veri Yapıları", AcademicYearId = year.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        context.Courses.Add(course);
        await context.SaveChangesAsync();
        var service = new CourseService(context, new TestCurrentUser(instructor.Id, UserRole.Instructor));
        var result = await service.GetCourseAsync(course.Id, default);
        Assert.Null(result);
    }

    [Fact]
    public async Task CourseCreation_WithDuplicateCodeInAcademicYear_Throws()
    {
        await using var context = CreateContext();
        var year = AddYear(context);
        context.Courses.Add(new Course { Id = Guid.NewGuid(), Code = "BM201", Name = "Veri Yapıları", AcademicYearId = year.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();
        var service = new CourseService(context, new TestCurrentUser(Guid.NewGuid(), UserRole.Admin));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateCourseAsync(new CreateCourseRequest { Code = "bm201", Name = "Yeni Ders", AcademicYearId = year.Id }, default));
    }

    [Fact]
    public async Task ExamCreation_WithInvalidType_Throws()
    {
        await using var context = CreateContext();
        var year = AddYear(context);
        var instructor = AddUser(context, "instructor", "Instructor123!", UserRole.Instructor);
        var course = new Course { Id = Guid.NewGuid(), Code = "BM201", Name = "Veri Yapıları", AcademicYearId = year.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        context.Courses.Add(course);
        context.CourseInstructors.Add(new CourseInstructor { CourseId = course.Id, UserId = instructor.Id });
        await context.SaveChangesAsync();
        var service = new ExamService(context, new TestCurrentUser(instructor.Id, UserRole.Instructor));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateExamAsync(course.Id, new CreateExamRequest { ExamType = (ExamType)99, EvaluationType = EvaluationType.MultipleChoice }, default));
    }

    private static SdsDbContext CreateContext() => new(new DbContextOptionsBuilder<SdsDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static AcademicYear AddYear(SdsDbContext context) { var year = new AcademicYear { Id = Guid.NewGuid(), Name = "2026-2027", IsActive = true }; context.AcademicYears.Add(year); context.SaveChanges(); return year; }
    private static User AddUser(SdsDbContext context, string username, string password, UserRole role) { var user = new User { Id = Guid.NewGuid(), Username = username, DisplayName = username, Role = role, CreatedAt = DateTime.UtcNow }; user.PasswordHash = new PasswordHasher<User>().HashPassword(user, password); context.Users.Add(user); context.SaveChanges(); return user; }
    private static IConfiguration CreateConfiguration() => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:Key"] = "local-development-key-with-more-than-32-chars", ["Jwt:Issuer"] = "SDS.Api", ["Jwt:Audience"] = "SDS.Frontend", ["Jwt:ExpirationMinutes"] = "30" }).Build();

    private sealed class TestCurrentUser(Guid id, UserRole role) : ICurrentUser
    {
        public CurrentUser Get() => new(id, role.ToString());
    }
}
