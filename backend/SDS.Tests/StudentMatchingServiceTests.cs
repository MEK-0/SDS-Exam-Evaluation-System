// Öğrenci eşleştirme servisinin otomatik, manuel ve onay akışını doğrular.
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SDS.Application.Matching;
using SDS.Application.Security;
using SDS.Domain.Entities;
using SDS.Infrastructure.Persistence;
using SDS.Infrastructure.Services;
using Xunit;

namespace SDS.Tests;

public sealed class StudentMatchingServiceTests
{
    [Fact]
    public async Task Run_MatchesExactAndNormalizedNumbersWithoutChangingLeadingZeros()
    {
        await using var setup = await CreateSetupAsync([("00123", "Ali", "Yılmaz"), (" 00456 ", "Ayşe", "Kaya"), ("00999", "Veli", "Demir")], [("00123", "A", true), ("00456", "B", true), ("9990", "C", true)]);
        var result = await setup.Service.RunAsync(setup.ExamId, default);
        Assert.Equal(1, result!.Summary.ExactMatchCount);
        Assert.Equal(1, result.Summary.NormalizedMatchCount);
        Assert.Contains(result.Records, item => item.StudentNumber == "00123" && item.Status == StudentMatchStatus.Matched);
        Assert.Contains(result.Records, item => item.StudentNumber == " 00456 " && item.Status == StudentMatchStatus.Matched);
    }

    [Fact]
    public async Task Run_DifferentStudentNumbersDoNotFuzzyMatch()
    {
        await using var setup = await CreateSetupAsync([("233301076", "Ali", "Yılmaz")], [("233301078", "A", true)]);
        var result = await setup.Service.RunAsync(setup.ExamId, default);
        Assert.Equal(1, result!.Summary.UnmatchedStudentCount);
        Assert.Contains(result.Records, item => item.Status == StudentMatchStatus.Conflict && item.OpticalStudentNumber == "233301078");
    }

    [Fact]
    public async Task Run_ExcludesAnswerKeyRecordsFromStudentMatching()
    {
        await using var setup = await CreateSetupAsync([("00123", "Ali", "Yılmaz")], [("00123", "A", true)]);
        setup.Context.OpticalExamRecords.Add(new OpticalExamRecord { Id = Guid.NewGuid(), ExamImportId = (await setup.Context.ExamImports.SingleAsync()).Id, LineNumber = 2, BookletType = "A", Answers = "ABCDE", IsValid = true, IsAnswerKey = true });
        await setup.Context.SaveChangesAsync();
        var result = await setup.Service.RunAsync(setup.ExamId, default);
        Assert.Equal(1, result!.Summary.TotalOpticalRecordCount);
        Assert.Single(result.Records);
    }

    [Fact]
    public async Task Run_DuplicateOpticalRecordsCreatesConflict()
    {
        await using var setup = await CreateSetupAsync([("00123", "Ali", "Yılmaz")], [("00123", "A", true), ("00123", "B", true)]);
        var result = await setup.Service.RunAsync(setup.ExamId, default);
        Assert.True(result!.Summary.ConflictCount > 0);
        Assert.False(result.Summary.CanConfirm);
    }

    [Fact]
    public async Task ManualMatch_ReassignsOpticalRecordSafely()
    {
        await using var setup = await CreateSetupAsync([("001", "Ali", "Yılmaz"), ("002", "Ayşe", "Kaya")], [("901", "A", true)]);
        var students = await setup.Context.ImportedStudents.ToListAsync();
        var optical = await setup.Context.OpticalExamRecords.SingleAsync();
        await setup.Service.CreateManualAsync(setup.ExamId, new ManualMatchRequest { ImportedStudentId = students[0].Id, OpticalExamRecordId = optical.Id }, default);
        var result = await setup.Service.CreateManualAsync(setup.ExamId, new ManualMatchRequest { ImportedStudentId = students[1].Id, OpticalExamRecordId = optical.Id }, default);
        Assert.Contains(result!.Records, item => item.ImportedStudentId == students[1].Id && item.OpticalExamRecordId == optical.Id && item.Status == StudentMatchStatus.Matched);
        Assert.Contains(result.Records, item => item.ImportedStudentId == students[0].Id && item.Status == StudentMatchStatus.Unmatched);
    }

    [Fact]
    public async Task ManualMatch_IsPreservedWhenAutomaticMatchingRunsAgain()
    {
        await using var setup = await CreateSetupAsync([("001", "Ali", "Yılmaz")], [("901", "A", true)]);
        var student = await setup.Context.ImportedStudents.SingleAsync();
        var optical = await setup.Context.OpticalExamRecords.SingleAsync();
        await setup.Service.CreateManualAsync(setup.ExamId, new ManualMatchRequest { ImportedStudentId = student.Id, OpticalExamRecordId = optical.Id }, default);
        var result = await setup.Service.RunAsync(setup.ExamId, default);
        Assert.Equal(1, result!.Summary.ManualMatchCount);
        Assert.True(result.Summary.CanConfirm);
    }

    [Fact]
    public async Task AbsentStudentCanBeConfirmedWithoutOpticalRecord()
    {
        await using var setup = await CreateSetupAsync([("001", "Ali", "Yılmaz")], []);
        var student = await setup.Context.ImportedStudents.SingleAsync();
        await setup.Service.RunAsync(setup.ExamId, default);
        var absent = await setup.Service.MarkAbsentAsync(setup.ExamId, new MarkAbsentRequest { ImportedStudentId = student.Id }, default);
        Assert.True(absent!.Summary.CanConfirm);
        var confirmed = await setup.Service.ConfirmAsync(setup.ExamId, default);
        Assert.True(confirmed!.Summary.IsConfirmed);
    }

    [Fact]
    public async Task Confirm_WithUnresolvedConflictIsRejected()
    {
        await using var setup = await CreateSetupAsync([("001", "Ali", "Yılmaz")], [("001", "A", true), ("001", "B", true)]);
        await setup.Service.RunAsync(setup.ExamId, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => setup.Service.ConfirmAsync(setup.ExamId, default));
    }

    [Fact]
    public async Task ManualMatchCanBeRemoved()
    {
        await using var setup = await CreateSetupAsync([("001", "Ali", "Yılmaz")], [("901", "A", true)]);
        var student = await setup.Context.ImportedStudents.SingleAsync();
        var optical = await setup.Context.OpticalExamRecords.SingleAsync();
        var matched = await setup.Service.CreateManualAsync(setup.ExamId, new ManualMatchRequest { ImportedStudentId = student.Id, OpticalExamRecordId = optical.Id }, default);
        var removed = await setup.Service.RemoveManualAsync(setup.ExamId, matched!.Records.Single(item => item.MatchId.HasValue).MatchId!.Value, default);
        Assert.Equal(1, removed!.Summary.UnmatchedStudentCount);
    }

    [Fact]
    public async Task UnauthorizedUserCannotReadMatching()
    {
        await using var setup = await CreateSetupAsync([("001", "Ali", "Yılmaz")], []);
        var unauthorizedService = new StudentMatchingService(setup.Context, new TestCurrentUser(Guid.NewGuid(), UserRole.Instructor));
        Assert.Null(await unauthorizedService.GetAsync(setup.ExamId, default));
    }

    [Fact]
    public async Task MatchingResultsRemainAvailableAfterReload()
    {
        await using var setup = await CreateSetupAsync([("001", "Ali", "Yılmaz")], [("001", "A", true)]);
        await setup.Service.RunAsync(setup.ExamId, default);
        var reloaded = await new StudentMatchingService(setup.Context, setup.CurrentUser).GetAsync(setup.ExamId, default);
        Assert.Equal(1, reloaded!.Summary.ExactMatchCount);
    }

    [Fact]
    public async Task RunningMatchingTwiceDoesNotCreateDuplicates()
    {
        await using var setup = await CreateSetupAsync([("001", "Ali", "Yılmaz")], [("001", "A", true)]);
        await setup.Service.RunAsync(setup.ExamId, default);
        await setup.Service.RunAsync(setup.ExamId, default);
        Assert.Single(await setup.Context.StudentMatches.ToListAsync());
    }

    [Fact]
    public async Task InvalidOpticalRecordIsReportedAsConflict()
    {
        await using var setup = await CreateSetupAsync([("001", "Ali", "Yılmaz")], [("001", "A", false)]);
        var result = await setup.Service.RunAsync(setup.ExamId, default);
        Assert.Contains(result!.Records, item => item.Status == StudentMatchStatus.Conflict);
    }

    [Fact]
    public async Task MatchedStudentCannotBeMarkedAbsent()
    {
        await using var setup = await CreateSetupAsync([("001", "Ali", "Yılmaz")], [("001", "A", true)]);
        var student = await setup.Context.ImportedStudents.SingleAsync();
        await setup.Service.RunAsync(setup.ExamId, default);
        await Assert.ThrowsAsync<InvalidOperationException>(() => setup.Service.MarkAbsentAsync(setup.ExamId, new MarkAbsentRequest { ImportedStudentId = student.Id }, default));
    }

    [Fact]
    public async Task ManualMatchCanReplaceAutomaticMatch()
    {
        await using var setup = await CreateSetupAsync([("001", "Ali", "Yılmaz")], [("001", "A", true), ("901", "B", true)]);
        var student = await setup.Context.ImportedStudents.SingleAsync();
        var optical = (await setup.Context.OpticalExamRecords.ToListAsync()).Single(item => item.StudentNumber == "901");
        await setup.Service.RunAsync(setup.ExamId, default);
        var result = await setup.Service.CreateManualAsync(setup.ExamId, new ManualMatchRequest { ImportedStudentId = student.Id, OpticalExamRecordId = optical.Id }, default);
        Assert.Equal(1, result!.Summary.ManualMatchCount);
    }

    private static async Task<TestSetup> CreateSetupAsync((string Number, string FirstName, string LastName)[] students, (string Number, string Booklet, bool IsValid)[] opticalRecords, Guid? userId = null)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var context = new SdsDbContext(new DbContextOptionsBuilder<SdsDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var user = new User { Id = userId ?? Guid.NewGuid(), Username = Guid.NewGuid().ToString(), DisplayName = "Instructor", Role = UserRole.Instructor, CreatedAt = DateTime.UtcNow };
        var year = new AcademicYear { Id = Guid.NewGuid(), Name = Guid.NewGuid().ToString(), IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = Guid.NewGuid().ToString()[..8], Name = "Ders", AcademicYearId = year.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var exam = new Exam { Id = Guid.NewGuid(), CourseId = course.Id, ExamType = ExamType.Midterm, EvaluationType = EvaluationType.MultipleChoice, CreatedAt = DateTime.UtcNow };
        var import = new ExamImport { Id = Guid.NewGuid(), ExamId = exam.Id, OpticalFileName = "optik.txt", ClassListFileName = "liste.xlsx", CreatedAt = DateTime.UtcNow };
        import.ImportedStudents = students.Select((item, index) => new ImportedStudent { Id = Guid.NewGuid(), ExamImportId = import.Id, StudentNumber = item.Number, FirstName = item.FirstName, LastName = item.LastName, RowNumber = index + 2 }).ToList();
        import.OpticalExamRecords = opticalRecords.Select((item, index) => new OpticalExamRecord { Id = Guid.NewGuid(), ExamImportId = import.Id, LineNumber = index + 1, StudentNumber = item.Number, BookletType = item.Booklet, Answers = "ABCD", IsValid = item.IsValid }).ToList();
        context.AddRange(user, year, course, exam, import);
        context.CourseInstructors.Add(new CourseInstructor { CourseId = course.Id, UserId = user.Id });
        await context.SaveChangesAsync();
        var currentUser = new TestCurrentUser(userId ?? user.Id, UserRole.Instructor);
        return new TestSetup(context, connection, exam.Id, new StudentMatchingService(context, currentUser), currentUser);
    }

    private sealed record TestSetup(SdsDbContext Context, SqliteConnection Connection, Guid ExamId, StudentMatchingService Service, TestCurrentUser CurrentUser) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync() { await Context.DisposeAsync(); await Connection.DisposeAsync(); }
    }

    private sealed class TestCurrentUser(Guid id, UserRole role) : ICurrentUser
    {
        public CurrentUser Get() => new(id, role.ToString());
    }
}
