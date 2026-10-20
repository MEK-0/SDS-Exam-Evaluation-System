// Cevap anahtarı doğrulama, kaydetme ve yetki senaryolarını doğrular.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SDS.Application.AnswerKeys;
using SDS.Application.Security;
using SDS.Domain.Entities;
using SDS.Infrastructure.Persistence;
using SDS.Infrastructure.Services;
using Xunit;

namespace SDS.Tests;

public sealed class AnswerKeyServiceTests
{
    [Fact]
    public async Task Update_WithSingleBooklet_SavesValidatedKey()
    {
        await using var setup = await CreateContextAsync(UserRole.Instructor);
        var service = new AnswerKeyService(setup.Context, new TestCurrentUser(setup.UserId, UserRole.Instructor));
        var request = CreateRequest(1, [new AnswerKeyQuestionRequest { CanonicalQuestionNumber = 1, Points = 10 }], [CreateBooklet("A", (1, 1, "A"))]);
        var result = await service.UpdateAsync(setup.ExamId, request, default);
        Assert.NotNull(result);
        Assert.True(result.IsComplete);
        Assert.Equal(AnswerKeyStatus.Validated, result.Status);
    }

    [Fact]
    public async Task Update_WithDifferentBookletOrder_PreservesLogicalMapping()
    {
        await using var setup = await CreateContextAsync(UserRole.Instructor);
        var service = new AnswerKeyService(setup.Context, new TestCurrentUser(setup.UserId, UserRole.Instructor));
        var request = CreateRequest(2, [new AnswerKeyQuestionRequest { CanonicalQuestionNumber = 1, Points = 5 }, new AnswerKeyQuestionRequest { CanonicalQuestionNumber = 2, Points = 5 }], [CreateBooklet("A", (1, 1, "A"), (2, 2, "B")), CreateBooklet("B", (2, 1, "C"), (1, 2, "D"))]);
        var result = await service.UpdateAsync(setup.ExamId, request, default);
        var bookletB = Assert.Single(result!.Booklets, item => item.BookletCode == "B");
        Assert.Equal(2, bookletB.Questions.Single(item => item.DisplayQuestionNumber == 1).ExamQuestionNumber);
        Assert.Equal(new[] { "C" }, bookletB.Questions.Single(item => item.DisplayQuestionNumber == 1).AcceptedAnswers);
    }

    [Fact]
    public async Task Update_WithMultipleAcceptedAnswers_SavesWhenEnabled()
    {
        await using var setup = await CreateContextAsync(UserRole.Instructor);
        var service = new AnswerKeyService(setup.Context, new TestCurrentUser(setup.UserId, UserRole.Instructor));
        var request = CreateRequest(1, [new AnswerKeyQuestionRequest { CanonicalQuestionNumber = 1, Points = 10 }], [CreateBooklet("A", (1, 1, "A", "B"))]);
        request.AllowMultipleAnswers = true;
        var result = await service.UpdateAsync(setup.ExamId, request, default);
        Assert.Equal(new[] { "A", "B" }, result!.Booklets.Single().Questions.Single().AcceptedAnswers);
    }

    [Fact]
    public async Task Update_WithMissingAnswers_SavesDraft()
    {
        await using var setup = await CreateContextAsync(UserRole.Instructor);
        var service = new AnswerKeyService(setup.Context, new TestCurrentUser(setup.UserId, UserRole.Instructor));
        var result = await service.UpdateAsync(setup.ExamId, CreateRequest(2, [new AnswerKeyQuestionRequest { CanonicalQuestionNumber = 1, Points = 5 }, new AnswerKeyQuestionRequest { CanonicalQuestionNumber = 2, Points = 5 }], [CreateBooklet("A", (1, 1, "A"), (2, 2, ""))]), default);
        Assert.NotNull(result);
        Assert.False(result.IsComplete);
        Assert.Equal(AnswerKeyStatus.Draft, result.Status);
    }

    [Fact]
    public async Task Update_WithInvalidAnswer_IsRejectedWithoutChangingExistingKey()
    {
        await using var setup = await CreateContextAsync(UserRole.Instructor);
        var service = new AnswerKeyService(setup.Context, new TestCurrentUser(setup.UserId, UserRole.Instructor));
        await service.UpdateAsync(setup.ExamId, CreateRequest(1, [new AnswerKeyQuestionRequest { CanonicalQuestionNumber = 1, Points = 10 }], [CreateBooklet("A", (1, 1, "A"))]), default);
        var invalid = CreateRequest(1, [new AnswerKeyQuestionRequest { CanonicalQuestionNumber = 1, Points = 10 }], [CreateBooklet("A", (1, 1, "F"))]);
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync(setup.ExamId, invalid, default));
        var saved = await service.GetAsync(setup.ExamId, default);
        Assert.Equal(new[] { "A" }, saved!.Booklets.Single().Questions.Single().AcceptedAnswers);
    }

    [Fact]
    public async Task Update_WithExistingKey_ReplacesItTransactionally()
    {
        await using var setup = await CreateContextAsync(UserRole.Instructor);
        var service = new AnswerKeyService(setup.Context, new TestCurrentUser(setup.UserId, UserRole.Instructor));
        await service.UpdateAsync(setup.ExamId, CreateRequest(1, [new AnswerKeyQuestionRequest { CanonicalQuestionNumber = 1, Points = 10 }], [CreateBooklet("A", (1, 1, "A"))]), default);
        var result = await service.UpdateAsync(setup.ExamId, CreateRequest(1, [new AnswerKeyQuestionRequest { CanonicalQuestionNumber = 1, Points = 10 }], [CreateBooklet("B", (1, 1, "B"))]), default);
        Assert.Equal("B", result!.Booklets.Single().BookletCode);
        Assert.Equal(new[] { "B" }, result.Booklets.Single().Questions.Single().AcceptedAnswers);
    }

    [Fact]
    public async Task Update_WithDuplicateDisplayNumber_IsRejected()
    {
        await using var setup = await CreateContextAsync(UserRole.Instructor);
        var service = new AnswerKeyService(setup.Context, new TestCurrentUser(setup.UserId, UserRole.Instructor));
        var request = CreateRequest(2, [new AnswerKeyQuestionRequest { CanonicalQuestionNumber = 1, Points = 5 }, new AnswerKeyQuestionRequest { CanonicalQuestionNumber = 2, Points = 5 }], [CreateBooklet("A", (1, 1, "A"), (2, 1, "B"))]);
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAsync(setup.ExamId, request, default));
    }

    [Fact]
    public async Task Update_WithCancelledQuestion_UsesConfiguredPolicy()
    {
        await using var setup = await CreateContextAsync(UserRole.Instructor);
        var service = new AnswerKeyService(setup.Context, new TestCurrentUser(setup.UserId, UserRole.Instructor));
        var request = CreateRequest(2, [new AnswerKeyQuestionRequest { CanonicalQuestionNumber = 1, Points = 0, Status = ExamQuestionStatus.Cancelled }, new AnswerKeyQuestionRequest { CanonicalQuestionNumber = 2, Points = 10 }], [CreateBooklet("A", (1, 1, ""), (2, 2, "A"))]);
        var result = await service.UpdateAsync(setup.ExamId, request, default);
        Assert.True(result!.IsComplete);
    }

    [Fact]
    public async Task Get_WithUnauthorizedUser_ReturnsNull()
    {
        await using var setup = await CreateContextAsync(UserRole.Instructor);
        var service = new AnswerKeyService(setup.Context, new TestCurrentUser(Guid.NewGuid(), UserRole.Instructor));
        Assert.Null(await service.GetAsync(setup.ExamId, default));
    }

    private static UpdateAnswerKeyRequest CreateRequest(int questionCount, List<AnswerKeyQuestionRequest> questions, List<AnswerKeyBookletRequest> booklets) => new() { MaximumScore = 10, QuestionCount = questionCount, DynamicScoring = false, AllowMultipleAnswers = false, Questions = questions, Booklets = booklets };
    private static AnswerKeyBookletRequest CreateBooklet(string code, params (int Logical, int Display)[] values) => new() { BookletCode = code, Questions = values.Select(item => CreateQuestion(item.Logical, item.Display)).ToList() };
    private static AnswerKeyBookletRequest CreateBooklet(string code, params (int Logical, int Display, string Answer)[] values) => new() { BookletCode = code, Questions = values.Select(item => CreateQuestion(item.Logical, item.Display, item.Answer)).ToList() };
    private static AnswerKeyBookletRequest CreateBooklet(string code, params (int Logical, int Display, string Answer, string Additional)[] values) => new() { BookletCode = code, Questions = values.Select(item => CreateQuestion(item.Logical, item.Display, item.Answer, item.Additional)).ToList() };
    private static BookletQuestionRequest CreateQuestion(int logical, int display, params string[] answers) => new() { ExamQuestionNumber = logical, DisplayQuestionNumber = display, AcceptedAnswers = answers.Where(item => !string.IsNullOrWhiteSpace(item)).ToList() };
    private static async Task<TestSetup> CreateContextAsync(UserRole role)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var context = new SdsDbContext(new DbContextOptionsBuilder<SdsDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var user = new User { Id = Guid.NewGuid(), Username = "instructor", DisplayName = "Instructor", Role = role, CreatedAt = DateTime.UtcNow };
        var course = new Course { Id = Guid.NewGuid(), Code = "BM201", Name = "Veri Yapıları", AcademicYearId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, AcademicYear = new AcademicYear { Id = Guid.NewGuid(), Name = Guid.NewGuid().ToString(), IsActive = true } };
        var exam = new Exam { Id = Guid.NewGuid(), CourseId = course.Id, ExamType = ExamType.Midterm, EvaluationType = EvaluationType.MultipleChoice, CreatedAt = DateTime.UtcNow, Course = course };
        context.Users.Add(user);
        context.Courses.Add(course);
        context.Exams.Add(exam);
        context.CourseInstructors.Add(new CourseInstructor { CourseId = course.Id, UserId = user.Id });
        await context.SaveChangesAsync();
        return new TestSetup(context, connection, exam.Id, user.Id);
    }

    private sealed record TestSetup(SdsDbContext Context, SqliteConnection Connection, Guid ExamId, Guid UserId) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() { await Context.DisposeAsync(); await Connection.DisposeAsync(); }
    }

    private sealed class TestCurrentUser(Guid id, UserRole role) : ICurrentUser
    {
        public CurrentUser Get() => new(id, role.ToString());
    }
}
