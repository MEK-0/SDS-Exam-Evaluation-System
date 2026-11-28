// Dönem raporunun tamamlanmış sınav seçimini, numara normalizasyonunu, harf eşiklerini ve erişim kurallarını doğrular.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SDS.Application.Security;
using SDS.Domain.Entities;
using SDS.Infrastructure.Persistence;
using SDS.Infrastructure.Services;
using Xunit;

namespace SDS.Tests;

public sealed class TermAnalysisServiceTests
{
    [Fact]
    public async Task KK7_H13_UsesCompletedExamsOnly()
    {
        await using var setup = await TermSetup.CreateAsync();
        await setup.AddExamAsync(ExamType.Midterm, ExamStatus.Completed, 0, ("111111111", 70m));
        await setup.AddExamAsync(ExamType.Final, ExamStatus.Completed, 1, ("111111111", 90m));
        await setup.AddExamAsync(ExamType.Midterm, ExamStatus.Draft, 2);
        var report = await setup.Service(setup.InstructorId).GetAsync(setup.CourseId, default);
        var student = Assert.Single(report!.Students);
        Assert.Equal(70m, student.MidtermScore);
        Assert.Equal(90m, student.FinalScore);
        Assert.Equal(82m, student.WeightedScore);
    }

    [Fact]
    public async Task KK0_TermEvaluation_IgnoresDraftExams_H13()
    {
        await using var setup = await TermSetup.CreateAsync();
        await setup.AddExamAsync(ExamType.Midterm, ExamStatus.Completed, 0, ("111111111", 50m));
        await setup.AddExamAsync(ExamType.Final, ExamStatus.Completed, 1, ("111111111", 50m));
        await setup.AddExamAsync(ExamType.Final, ExamStatus.Draft, 2, ("111111111", 100m));
        var report = await setup.Service(setup.InstructorId).GetAsync(setup.CourseId, default);
        Assert.Equal(50m, report!.Students.Single().FinalScore);
        Assert.Equal(50m, report.Summary.WeightedAverage);
    }

    [Fact]
    public async Task KK0_TermEvaluation_LatestCompletedExamOfTypeIsUsed()
    {
        await using var setup = await TermSetup.CreateAsync();
        await setup.AddExamAsync(ExamType.Midterm, ExamStatus.Completed, 0, ("111111111", 10m));
        await setup.AddExamAsync(ExamType.Midterm, ExamStatus.Completed, 3, ("111111111", 60m));
        await setup.AddExamAsync(ExamType.Final, ExamStatus.Completed, 1, ("111111111", 60m));
        var report = await setup.Service(setup.InstructorId).GetAsync(setup.CourseId, default);
        Assert.Equal(60m, report!.Students.Single().MidtermScore);
    }

    [Fact]
    public async Task KK0_TermEvaluation_OpensWithMidtermAndMakeUp_R18_1()
    {
        await using var setup = await TermSetup.CreateAsync();
        await setup.AddExamAsync(ExamType.Midterm, ExamStatus.Completed, 0, ("111111111", 40m));
        await setup.AddExamAsync(ExamType.MakeUp, ExamStatus.Completed, 1, ("111111111", 80m));
        var course = await new CourseService(setup.Context, new TestUser(setup.InstructorId)).GetCourseAsync(setup.CourseId, default);
        Assert.True(course!.TermEvaluationAvailable);
        var report = await setup.Service(setup.InstructorId).GetAsync(setup.CourseId, default);
        var student = report!.Students.Single();
        Assert.Equal(80m, student.EffectiveExamScore);
        Assert.Equal(64m, student.WeightedScore);
    }

    [Fact]
    public async Task R7_3_TermEvaluation_NotAvailableWithDraftFinal()
    {
        await using var setup = await TermSetup.CreateAsync();
        await setup.AddExamAsync(ExamType.Midterm, ExamStatus.Completed, 0, ("111111111", 40m));
        await setup.AddExamAsync(ExamType.Final, ExamStatus.Draft, 1, ("111111111", 80m));
        var course = await new CourseService(setup.Context, new TestUser(setup.InstructorId)).GetCourseAsync(setup.CourseId, default);
        Assert.False(course!.TermEvaluationAvailable);
        await Assert.ThrowsAsync<InvalidOperationException>(() => setup.Service(setup.InstructorId).GetAsync(setup.CourseId, default));
    }

    [Fact]
    public async Task R7_3_TermEvaluation_NotAvailableWithoutMidterm()
    {
        await using var setup = await TermSetup.CreateAsync();
        await setup.AddExamAsync(ExamType.Final, ExamStatus.Completed, 0, ("111111111", 80m));
        var course = await new CourseService(setup.Context, new TestUser(setup.InstructorId)).GetCourseAsync(setup.CourseId, default);
        Assert.False(course!.TermEvaluationAvailable);
    }

    [Fact]
    public async Task R18_NormalizesStudentNumbersAcrossExams()
    {
        await using var setup = await TermSetup.CreateAsync();
        await setup.AddExamAsync(ExamType.Midterm, ExamStatus.Completed, 0, ("123456789", 60m));
        await setup.AddExamAsync(ExamType.Final, ExamStatus.Completed, 1, (" 123456789 ", 80m));
        var report = await setup.Service(setup.InstructorId).GetAsync(setup.CourseId, default);
        var student = Assert.Single(report!.Students);
        Assert.Equal("123456789", student.StudentNumber);
        Assert.Equal(60m, student.MidtermScore);
        Assert.Equal(80m, student.FinalScore);
    }

    [Fact]
    public async Task KK7_H12_TermReportUsesPolicyThresholds()
    {
        await using var setup = await TermSetup.CreateAsync();
        setup.Context.GradingPolicies.Add(new GradingPolicy { Id = Guid.NewGuid(), AaMinimum = 95, BaMinimum = 85, BbMinimum = 80, CbMinimum = 70, CcMinimum = 60, DcMinimum = 50, MidtermWeight = 40, FinalWeight = 60, MakeUpWeight = 60, UpdatedAt = DateTime.UtcNow });
        await setup.Context.SaveChangesAsync();
        await setup.AddExamAsync(ExamType.Midterm, ExamStatus.Completed, 0, ("111111111", 90m));
        await setup.AddExamAsync(ExamType.Final, ExamStatus.Completed, 1, ("111111111", 90m));
        var report = await setup.Service(setup.InstructorId).GetAsync(setup.CourseId, default);
        Assert.Equal("BA", report!.Students.Single().LetterGrade);
    }

    [Fact]
    public async Task KK7_TermAnalysis_UnassignedInstructorGetsNull()
    {
        await using var setup = await TermSetup.CreateAsync();
        await setup.AddExamAsync(ExamType.Midterm, ExamStatus.Completed, 0, ("111111111", 40m));
        await setup.AddExamAsync(ExamType.Final, ExamStatus.Completed, 1, ("111111111", 80m));
        Assert.Null(await setup.Service(setup.OutsiderId).GetAsync(setup.CourseId, default));
        Assert.Null(await setup.Service(setup.OutsiderId).ExportAsync(setup.CourseId, default));
        Assert.Null(await new CourseService(setup.Context, new TestUser(setup.OutsiderId)).GetCourseAsync(setup.CourseId, default));
    }

    private sealed class TestUser(Guid id) : ICurrentUser
    {
        public CurrentUser Get() => new(id, UserRole.Instructor.ToString());
    }

    private sealed class TermSetup : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly Guid templateVersionId;

        private TermSetup(SqliteConnection connection, SdsDbContext context, Guid courseId, Guid instructorId, Guid outsiderId, Guid templateVersionId)
        {
            this.connection = connection;
            this.templateVersionId = templateVersionId;
            Context = context;
            CourseId = courseId;
            InstructorId = instructorId;
            OutsiderId = outsiderId;
        }

        public SdsDbContext Context { get; }
        public Guid CourseId { get; }
        public Guid InstructorId { get; }
        public Guid OutsiderId { get; }

        public TermAnalysisService Service(Guid userId) => new(Context, new TestUser(userId));

        public static async Task<TermSetup> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            var context = new SdsDbContext(new DbContextOptionsBuilder<SdsDbContext>().UseSqlite(connection).Options);
            await context.Database.EnsureCreatedAsync();
            var instructor = new User { Id = Guid.NewGuid(), Username = Guid.NewGuid().ToString(), DisplayName = "Eğitmen", Role = UserRole.Instructor, CreatedAt = DateTime.UtcNow };
            var outsider = new User { Id = Guid.NewGuid(), Username = Guid.NewGuid().ToString(), DisplayName = "Atanmamış", Role = UserRole.Instructor, CreatedAt = DateTime.UtcNow };
            var year = new AcademicYear { Id = Guid.NewGuid(), Name = Guid.NewGuid().ToString(), IsActive = true };
            var course = new Course { Id = Guid.NewGuid(), Code = "DON101", Name = "Ders", AcademicYearId = year.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            var versionId = TestOpticalTemplates.AddVersion(context);
            context.AddRange(instructor, outsider, year, course);
            context.CourseInstructors.Add(new CourseInstructor { CourseId = course.Id, UserId = instructor.Id });
            await context.SaveChangesAsync();
            return new TermSetup(connection, context, course.Id, instructor.Id, outsider.Id, versionId);
        }

        public async Task AddExamAsync(ExamType type, ExamStatus status, int createdOffsetMinutes, params (string Number, decimal Score)[] students)
        {
            var exam = new Exam { Id = Guid.NewGuid(), CourseId = CourseId, OpticalTemplateVersionId = templateVersionId, ExamType = type, EvaluationType = EvaluationType.MultipleChoice, Status = status, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(createdOffsetMinutes) };
            var import = new ExamImport { Id = Guid.NewGuid(), ExamId = exam.Id, OpticalFileName = "optik.txt", ClassListFileName = "sinif.xlsx", CreatedAt = DateTime.UtcNow };
            var imported = new List<ImportedStudent>();
            var results = new List<ExamResult>();
            var row = 1;
            foreach (var (number, score) in students)
            {
                var student = new ImportedStudent { Id = Guid.NewGuid(), ExamImportId = import.Id, StudentNumber = number, FirstName = "Öğrenci", LastName = number.Trim(), SourceFileName = "sinif.xlsx", RowNumber = ++row };
                imported.Add(student);
                results.Add(new ExamResult { Id = Guid.NewGuid(), ExamId = exam.Id, ImportedStudentId = student.Id, RawScore = score, ScaledScore = score, AdjustedScore = score, Status = GradeStatus.Graded, CalculatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            }
            import.ImportedStudents = imported;
            Context.AddRange(exam, import);
            Context.ExamResults.AddRange(results);
            await Context.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
