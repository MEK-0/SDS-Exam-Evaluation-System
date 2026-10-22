// Öğrenme çıktısı kaydı ve sınav tamamlanma kurallarını doğrular.
using System;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SDS.Application.LearningOutcomes;
using SDS.Application.Security;
using SDS.Domain.Entities;
using SDS.Infrastructure.Persistence;
using SDS.Infrastructure.Services;
using Xunit;

namespace SDS.Tests;

public sealed class LearningOutcomeServiceTests
{
    [Fact]
    public async Task CreateOutcome_RejectsDuplicateDescriptionWithinCourse()
    {
        await using var setup = await CreateSetupAsync();
        var service = new LearningOutcomeService(setup.Context, new TestCurrentUser(setup.UserId));
        await service.CreateOutcomeAsync(setup.CourseId, new CreateLearningOutcomeRequest { Description = "  Algoritmik düşünür  " }, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.CreateOutcomeAsync(setup.CourseId, new CreateLearningOutcomeRequest { Description = "Algoritmik düşünür" }, default));
    }

    [Fact]
    public async Task CompleteExam_SavesOutcomeRelationsAndMarksExamCompleted()
    {
        await using var setup = await CreateSetupAsync();
        var service = new LearningOutcomeService(setup.Context, new TestCurrentUser(setup.UserId));
        var outcome = await service.CreateOutcomeAsync(setup.CourseId, new CreateLearningOutcomeRequest { Description = "Önerme mantığını uygular" }, default);
        var response = await service.CompleteExamAsync(setup.ExamId, new CompleteExamRequest { LearningOutcomeMappings = new SaveLearningOutcomeMappingsRequest { Mappings = [new LearningOutcomeQuestionMapInput { LearningOutcomeId = outcome!.Id, QuestionNumbers = [1, 2] }] } }, default);
        Assert.True(response!.IsComplete);
        Assert.Equal(2, await setup.Context.QuestionLearningOutcomes.CountAsync());
        Assert.Equal(ExamStatus.Completed, await setup.Context.Exams.Where(item => item.Id == setup.ExamId).Select(item => item.Status).SingleAsync());
    }

    private static async Task<TestSetup> CreateSetupAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var context = new SdsDbContext(new DbContextOptionsBuilder<SdsDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var user = new User { Id = Guid.NewGuid(), Username = Guid.NewGuid().ToString(), DisplayName = "Eğitmen", Role = UserRole.Instructor, CreatedAt = DateTime.UtcNow };
        var year = new AcademicYear { Id = Guid.NewGuid(), Name = Guid.NewGuid().ToString(), IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = Guid.NewGuid().ToString(), Name = "Ders", AcademicYearId = year.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var exam = new Exam { Id = Guid.NewGuid(), CourseId = course.Id, ExamType = ExamType.Midterm, EvaluationType = EvaluationType.MultipleChoice, MatchingConfirmed = true, CreatedAt = DateTime.UtcNow };
        var import = new ExamImport { Id = Guid.NewGuid(), ExamId = exam.Id, OpticalFileName = "optik.txt", ClassListFileName = "sinif.xlsx", CreatedAt = DateTime.UtcNow };
        var student = new ImportedStudent { Id = Guid.NewGuid(), ExamImportId = import.Id, StudentNumber = "001", FirstName = "Ali", LastName = "Yılmaz", SourceFileName = "sinif.xlsx", RowNumber = 2 };
        var key = new ExamAnswerKey { Id = Guid.NewGuid(), ExamId = exam.Id, MaximumScore = 100, QuestionCount = 2, Status = AnswerKeyStatus.Validated, UpdatedAt = DateTime.UtcNow };
        key.Questions = [new ExamQuestion { Id = Guid.NewGuid(), ExamAnswerKeyId = key.Id, CanonicalQuestionNumber = 1, Points = 50 }, new ExamQuestion { Id = Guid.NewGuid(), ExamAnswerKeyId = key.Id, CanonicalQuestionNumber = 2, Points = 50 }];
        var booklet = new ExamBooklet { Id = Guid.NewGuid(), ExamAnswerKeyId = key.Id, BookletCode = "A", QuestionCount = 2 };
        booklet.Questions = key.Questions.Select(question => new BookletQuestion { Id = Guid.NewGuid(), ExamBookletId = booklet.Id, ExamQuestionId = question.Id, DisplayQuestionNumber = question.CanonicalQuestionNumber, AcceptedAnswers = "A" }).ToList();
        key.Booklets = [booklet];
        var result = new ExamResult { Id = Guid.NewGuid(), ExamId = exam.Id, ImportedStudentId = student.Id, RawScore = 100, AdjustedScore = 100, Status = GradeStatus.Graded, CalculatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        import.ImportedStudents = [student];
        context.AddRange(user, year, course, exam, import, key, result);
        context.CourseInstructors.Add(new CourseInstructor { CourseId = course.Id, UserId = user.Id });
        await context.SaveChangesAsync();
        return new TestSetup(context, connection, course.Id, exam.Id, user.Id);
    }

    private sealed record TestSetup(SdsDbContext Context, SqliteConnection Connection, Guid CourseId, Guid ExamId, Guid UserId) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() { await Context.DisposeAsync(); await Connection.DisposeAsync(); }
    }

    private sealed class TestCurrentUser(Guid userId) : ICurrentUser
    {
        public CurrentUser Get() => new(userId, UserRole.Instructor.ToString());
    }
}
