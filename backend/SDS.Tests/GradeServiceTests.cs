// Otomatik puanlama ve not sonuçlarının temel kurallarını doğrular.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Text;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SDS.Application.Grades;
using SDS.Application.Security;
using SDS.Domain.Entities;
using SDS.Infrastructure.Persistence;
using SDS.Infrastructure.Services;
using Xunit;

namespace SDS.Tests;

public sealed class GradeServiceTests
{
    [Fact]
    public async Task Calculate_UsesBookletKeyAndEqualQuestionPoints()
    {
        await using var setup = await CreateSetupAsync();
        var service = new GradeService(setup.Context, new TestCurrentUser(setup.UserId));
        var result = await service.CalculateAsync(setup.ExamId, default);
        Assert.Equal(100m, result!.Rows.Single(item => item.StudentNumber == "001").RawScore);
        Assert.Equal(80m, result.Rows.Single(item => item.StudentNumber == "002").RawScore);
    }

    [Fact]
    public async Task Calculate_SelectsBookletSpecificAnswerKey()
    {
        await using var setup = await CreateSetupAsync();
        var bookletB = setup.Context.ExamAnswerKeys.SelectMany(item => item.Booklets).Single(item => item.BookletCode == "B");
        var record = setup.Context.OpticalExamRecords.Single(item => item.StudentNumber == "002");
        record.BookletType = "B";
        record.Answers = new string('B', 25);
        await setup.Context.SaveChangesAsync();
        var result = await new GradeService(setup.Context, new TestCurrentUser(setup.UserId)).CalculateAsync(setup.ExamId, default);
        Assert.Equal(100m, result!.Rows.Single(item => item.StudentNumber == "002").RawScore);
        Assert.Equal(25, bookletB.Questions.Count);
    }

    private static async Task<TestSetup> CreateSetupAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var context = new SdsDbContext(new DbContextOptionsBuilder<SdsDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var user = new User { Id = Guid.NewGuid(), Username = Guid.NewGuid().ToString(), DisplayName = "Instructor", Role = UserRole.Instructor, CreatedAt = DateTime.UtcNow };
        var year = new AcademicYear { Id = Guid.NewGuid(), Name = Guid.NewGuid().ToString(), IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = Guid.NewGuid().ToString(), Name = "Ders", AcademicYearId = year.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var exam = new Exam { Id = Guid.NewGuid(), CourseId = course.Id, ExamType = ExamType.Midterm, EvaluationType = EvaluationType.MultipleChoice, MatchingConfirmed = true, CreatedAt = DateTime.UtcNow };
        var import = new ExamImport { Id = Guid.NewGuid(), ExamId = exam.Id, OpticalFileName = "optik.txt", ClassListFileName = "sinif.xlsx", CreatedAt = DateTime.UtcNow };
        var firstStudent = new ImportedStudent { Id = Guid.NewGuid(), ExamImportId = import.Id, StudentNumber = "001", FirstName = "Ali", LastName = "Yılmaz", SourceFileName = "sinif.xlsx", RowNumber = 2 };
        var secondStudent = new ImportedStudent { Id = Guid.NewGuid(), ExamImportId = import.Id, StudentNumber = "002", FirstName = "Ayşe", LastName = "Kaya", SourceFileName = "sinif.xlsx", RowNumber = 3 };
        var firstRecord = new OpticalExamRecord { Id = Guid.NewGuid(), ExamImportId = import.Id, StudentNumber = "001", BookletType = "A", Answers = new string('A', 25), IsValid = true, LineNumber = 1 };
        var secondRecord = new OpticalExamRecord { Id = Guid.NewGuid(), ExamImportId = import.Id, StudentNumber = "002", BookletType = "A", Answers = new string('A', 20) + new string('B', 5), IsValid = true, LineNumber = 2 };
        import.ImportedStudents = [firstStudent, secondStudent];
        import.OpticalExamRecords = [firstRecord, secondRecord];
        var key = new ExamAnswerKey { Id = Guid.NewGuid(), ExamId = exam.Id, MaximumScore = 100, QuestionCount = 25, Status = AnswerKeyStatus.Validated, UpdatedAt = DateTime.UtcNow };
        key.Questions = Enumerable.Range(1, 25).Select(number => new ExamQuestion { Id = Guid.NewGuid(), ExamAnswerKeyId = key.Id, CanonicalQuestionNumber = number, Points = 4, Status = ExamQuestionStatus.Active }).ToList();
        key.Booklets = [CreateBooklet(key, "A", "A"), CreateBooklet(key, "B", "B")];
        context.AddRange(user, year, course, exam, import, key);
        context.CourseInstructors.Add(new CourseInstructor { CourseId = course.Id, UserId = user.Id });
        context.StudentMatches.AddRange(new StudentMatch { Id = Guid.NewGuid(), ExamId = exam.Id, ImportedStudentId = firstStudent.Id, OpticalExamRecordId = firstRecord.Id, MatchType = StudentMatchType.Exact, Status = StudentMatchStatus.Matched, MatchedAt = DateTime.UtcNow, MatchedByUserId = user.Id }, new StudentMatch { Id = Guid.NewGuid(), ExamId = exam.Id, ImportedStudentId = secondStudent.Id, OpticalExamRecordId = secondRecord.Id, MatchType = StudentMatchType.Exact, Status = StudentMatchStatus.Matched, MatchedAt = DateTime.UtcNow, MatchedByUserId = user.Id });
        await context.SaveChangesAsync();
        return new TestSetup(context, connection, exam.Id, user.Id);
    }

    private static ExamBooklet CreateBooklet(ExamAnswerKey key, string code, string answer)
    {
        var booklet = new ExamBooklet { Id = Guid.NewGuid(), ExamAnswerKeyId = key.Id, BookletCode = code, QuestionCount = 25 };
        booklet.Questions = key.Questions.Select(question => new BookletQuestion { Id = Guid.NewGuid(), ExamBookletId = booklet.Id, ExamQuestionId = question.Id, DisplayQuestionNumber = question.CanonicalQuestionNumber, AcceptedAnswers = answer }).ToList();
        return booklet;
    }

    private sealed record TestSetup(SdsDbContext Context, SqliteConnection Connection, Guid ExamId, Guid UserId) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }

    private sealed class TestCurrentUser(Guid userId) : ICurrentUser
    {
        public CurrentUser Get() => new(userId, UserRole.Instructor.ToString());
    }
}
