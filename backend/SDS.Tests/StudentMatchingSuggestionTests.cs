// Öğrenci eşleştirmedeki öneri, toplu "girmedi", yok say ve geçersiz satır davranışlarını doğrular.
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SDS.Application.Imports;
using SDS.Application.Matching;
using SDS.Application.Security;
using SDS.Domain.Entities;
using SDS.Infrastructure.Importing;
using SDS.Infrastructure.Persistence;
using SDS.Infrastructure.Seeding;
using SDS.Infrastructure.Services;
using Xunit;

namespace SDS.Tests;

public sealed class StudentMatchingSuggestionTests
{
    private const string SampleLayoutName = "Örnek dosya düzeni (ornek_optik_cikti.txt)";

    [Fact]
    public async Task KK4_K15_AnswerKeyRowsAreNotConflicts()
    {
        await using var setup = await CreateSetupAsync();
        await setup.ImportFixtureAsync();
        var result = await setup.Matching.RunAsync(setup.ExamId, default);
        Assert.Equal(15, result!.Summary.TotalOpticalRecordCount);
        Assert.DoesNotContain(result.Records, item => item.OpticalStudentNumber == "000000000");
        Assert.Equal(0, result.Summary.InvalidOpticalCount);
    }

    [Fact]
    public async Task KK4_H2_InvalidLineDoesNotBlockConfirmation()
    {
        await using var setup = await CreateSetupAsync();
        await setup.SeedImportAsync([("001", "Ali", "Yılmaz")], [("001", "ALI YILMAZ", true), ("", "", false)]);
        var result = await setup.Matching.RunAsync(setup.ExamId, default);
        Assert.Contains(result!.Records, item => item.IsInvalidOptical);
        Assert.Equal(1, result.Summary.InvalidOpticalCount);
        Assert.Equal(0, result.Summary.ConflictCount);
        Assert.True(result.Summary.CanConfirm);
        var confirmed = await setup.Matching.ConfirmAsync(setup.ExamId, default);
        Assert.True(confirmed!.Summary.IsConfirmed);
    }

    [Fact]
    public async Task KK4_H3_IgnoreOpticalRecordSucceedsTwice()
    {
        await using var setup = await CreateSetupAsync();
        await setup.SeedImportAsync([("001", "Ali", "Yılmaz")], [("001", "ALI YILMAZ", true), ("901", "FAZLA BİR", true), ("902", "FAZLA İKİ", true)]);
        await setup.Matching.RunAsync(setup.ExamId, default);
        var extras = await setup.Context.OpticalExamRecords.Where(item => item.StudentNumber.StartsWith("90")).OrderBy(item => item.LineNumber).ToListAsync();
        await setup.Matching.IgnoreOpticalAsync(setup.ExamId, new IgnoreOpticalRequest { OpticalExamRecordId = extras[0].Id }, default);
        var result = await setup.Matching.IgnoreOpticalAsync(setup.ExamId, new IgnoreOpticalRequest { OpticalExamRecordId = extras[1].Id }, default);
        Assert.Equal(2, await setup.Context.StudentMatches.CountAsync(item => item.ImportedStudentId == null && item.IsAbsent));
        Assert.True(result!.Summary.CanConfirm);
        Assert.True((await setup.Matching.ConfirmAsync(setup.ExamId, default))!.Summary.IsConfirmed);
    }

    [Fact]
    public async Task KK4_K14_InnerSpaceNumberIsSuggestionNotMatch()
    {
        await using var setup = await CreateSetupAsync();
        await setup.SeedImportAsync([("213311042", "Ayşe", "Kaya")], [("2133110 2", "AYŞE KAYA", true)]);
        var result = await setup.Matching.RunAsync(setup.ExamId, default);
        Assert.Empty(await setup.Context.StudentMatches.ToListAsync());
        var suggestion = Assert.Single(result!.Suggestions);
        Assert.Equal(MatchSuggestionKind.SimilarNumber, suggestion.Kind);
        Assert.Equal("213311042", suggestion.StudentNumber);
        Assert.Equal("2133110 2", suggestion.OpticalStudentNumber);
        Assert.False(result.Summary.CanConfirm);
    }

    [Fact]
    public async Task KK4_K14_OuterSpaceNumberIsAutoMatched()
    {
        await using var setup = await CreateSetupAsync();
        await setup.SeedImportAsync([("213311042", "Ayşe", "Kaya")], [(" 213311042 ", "AYŞE KAYA", true)]);
        var result = await setup.Matching.RunAsync(setup.ExamId, default);
        Assert.Equal(1, result!.Summary.NormalizedMatchCount);
        Assert.Empty(result.Suggestions);
    }

    [Fact]
    public async Task KK4_K4_OneDigitDiffWithSameNameIsSuggested()
    {
        await using var setup = await CreateSetupAsync();
        await setup.SeedImportAsync([("203301027", "Betül", "Çelik")], [("223301027", "BETÜL ÇELİK", true)]);
        var result = await setup.Matching.RunAsync(setup.ExamId, default);
        var suggestion = Assert.Single(result!.Suggestions);
        Assert.Equal(MatchSuggestionKind.SimilarNumber, suggestion.Kind);
        Assert.Equal(0, result.Summary.ExactMatchCount + result.Summary.NormalizedMatchCount);
    }

    [Fact]
    public async Task KK4_K4_OneDigitDiffWithDifferentNameIsNotSuggested()
    {
        await using var setup = await CreateSetupAsync();
        await setup.SeedImportAsync([("203301027", "Ayşe", "Kaya")], [("223301027", "BETÜL ÇELİK", true)]);
        var result = await setup.Matching.RunAsync(setup.ExamId, default);
        Assert.DoesNotContain(result!.Suggestions, item => item.Kind == MatchSuggestionKind.SimilarNumber);
        Assert.Contains(result.Suggestions, item => item.Kind == MatchSuggestionKind.Absent);
    }

    [Fact]
    public async Task KK4_K14_NonDigitNumberIsNotAutoMatched()
    {
        await using var setup = await CreateSetupAsync();
        await setup.SeedImportAsync([("213311042", "Ayşe", "Kaya"), ("21331A043", "Veli", "Demir")], [("21331A042", "AYŞE KAYA", true), ("21331A043", "VELİ DEMİR", true)]);
        var result = await setup.Matching.RunAsync(setup.ExamId, default);
        Assert.Empty(await setup.Context.StudentMatches.ToListAsync());
        Assert.Equal(0, result!.Summary.ExactMatchCount + result.Summary.NormalizedMatchCount);
        Assert.Contains(result.Suggestions, item => item.Kind == MatchSuggestionKind.SimilarNumber && item.OpticalStudentNumber == "21331A042");
    }

    [Fact]
    public async Task KK4_K13_AbsentSuggestionsCanBeBulkConfirmed()
    {
        await using var setup = await CreateSetupAsync();
        await setup.SeedImportAsync([("001", "Ali", "Yılmaz"), ("002", "Ayşe", "Kaya"), ("003", "Veli", "Demir")], [("001", "ALI YILMAZ", true)]);
        var run = await setup.Matching.RunAsync(setup.ExamId, default);
        Assert.Equal(2, run!.Summary.AbsentSuggestionCount);
        Assert.Equal(1, await setup.Context.StudentMatches.CountAsync());
        var result = await setup.Matching.AcceptAbsentSuggestionsAsync(setup.ExamId, default);
        Assert.Equal(3, await setup.Context.StudentMatches.CountAsync());
        Assert.True(result!.Summary.CanConfirm);
        Assert.Equal(0, result.Summary.AbsentSuggestionCount);
        var again = await setup.Matching.AcceptAbsentSuggestionsAsync(setup.ExamId, default);
        Assert.True(again!.Summary.CanConfirm);
        Assert.Equal(3, await setup.Context.StudentMatches.CountAsync());
    }

    [Fact]
    public async Task KK4_Y3_14_SuggestionNotPersistedWithoutApproval()
    {
        await using var setup = await CreateSetupAsync();
        await setup.SeedImportAsync([("203301027", "Betül", "Çelik")], [("223301027", "BETÜL ÇELİK", true)]);
        var run = await setup.Matching.RunAsync(setup.ExamId, default);
        var suggestion = Assert.Single(run!.Suggestions);
        await setup.Matching.RunAsync(setup.ExamId, default);
        await setup.Matching.GetAsync(setup.ExamId, default);
        Assert.Empty(await setup.Context.StudentMatches.ToListAsync());
        var wrong = new AcceptSuggestionRequest { ImportedStudentId = suggestion.ImportedStudentId, OpticalExamRecordId = Guid.NewGuid() };
        await Assert.ThrowsAsync<InvalidOperationException>(() => setup.Matching.AcceptSuggestionAsync(setup.ExamId, wrong, default));
        Assert.Empty(await setup.Context.StudentMatches.ToListAsync());
        var accepted = await setup.Matching.AcceptSuggestionAsync(setup.ExamId, new AcceptSuggestionRequest { ImportedStudentId = suggestion.ImportedStudentId, OpticalExamRecordId = suggestion.OpticalExamRecordId!.Value }, default);
        Assert.Single(await setup.Context.StudentMatches.ToListAsync());
        Assert.True(accepted!.Summary.CanConfirm);
    }

    [Fact]
    public async Task KK0_ReferenceScenarioCanBeConfirmed()
    {
        await using var setup = await CreateSetupAsync();
        await setup.ImportFixtureAsync();
        var run = await setup.Matching.RunAsync(setup.ExamId, default);
        Assert.Equal(17, run!.Summary.TotalClassListCount);
        Assert.Equal(15, run.Summary.TotalOpticalRecordCount);
        Assert.Equal(13, run.Summary.ExactMatchCount + run.Summary.NormalizedMatchCount);
        Assert.Equal(2, run.Suggestions.Count(item => item.Kind == MatchSuggestionKind.SimilarNumber));
        Assert.Equal(2, run.Suggestions.Count(item => item.Kind == MatchSuggestionKind.Absent));
        Assert.DoesNotContain(run.Suggestions, item => item.Kind == MatchSuggestionKind.SimilarNumber && item.StudentNumber is "326407875" or "386457817");
        Assert.False(run.Summary.CanConfirm);
        foreach (var suggestion in run.Suggestions.Where(item => item.Kind == MatchSuggestionKind.SimilarNumber))
        {
            await setup.Matching.AcceptSuggestionAsync(setup.ExamId, new AcceptSuggestionRequest { ImportedStudentId = suggestion.ImportedStudentId, OpticalExamRecordId = suggestion.OpticalExamRecordId!.Value }, default);
        }
        var absent = await setup.Matching.AcceptAbsentSuggestionsAsync(setup.ExamId, default);
        Assert.True(absent!.Summary.CanConfirm);
        var confirmed = await setup.Matching.ConfirmAsync(setup.ExamId, default);
        Assert.True(confirmed!.Summary.IsConfirmed);
    }

    [Fact]
    public async Task KK4_Y3_14_AcceptSuggestion_UnassignedInstructorGetsNotFound()
    {
        await using var setup = await CreateSetupAsync();
        await setup.SeedImportAsync([("203301027", "Betül", "Çelik")], [("223301027", "BETÜL ÇELİK", true)]);
        var run = await setup.Matching.RunAsync(setup.ExamId, default);
        var suggestion = Assert.Single(run!.Suggestions);
        var outsider = new StudentMatchingService(setup.Context, new TestUser(setup.OutsiderId));
        var request = new AcceptSuggestionRequest { ImportedStudentId = suggestion.ImportedStudentId, OpticalExamRecordId = suggestion.OpticalExamRecordId!.Value };
        Assert.Null(await outsider.AcceptSuggestionAsync(setup.ExamId, request, default));
        Assert.Null(await outsider.AcceptAbsentSuggestionsAsync(setup.ExamId, default));
        Assert.Empty(await setup.Context.StudentMatches.ToListAsync());
    }

    private static async Task<Setup> CreateSetupAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var context = new SdsDbContext(new DbContextOptionsBuilder<SdsDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var instructor = new User { Id = Guid.NewGuid(), Username = "ogretmen-" + Guid.NewGuid(), DisplayName = "Öğretim Elemanı", Role = UserRole.Instructor, CreatedAt = DateTime.UtcNow };
        var outsider = new User { Id = Guid.NewGuid(), Username = "dis-" + Guid.NewGuid(), DisplayName = "Atanmamış", Role = UserRole.Instructor, CreatedAt = DateTime.UtcNow };
        var year = new AcademicYear { Id = Guid.NewGuid(), Name = Guid.NewGuid().ToString(), IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "EŞL" + Guid.NewGuid().ToString("N")[..5], Name = "Ders", AcademicYearId = year.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        context.AddRange(instructor, outsider, year, course);
        context.CourseInstructors.Add(new CourseInstructor { CourseId = course.Id, UserId = instructor.Id });
        await context.SaveChangesAsync();
        await new OpticalTemplateSeeder(context).SeedAsync(default);
        var versionId = await context.OpticalTemplates.AsNoTracking().Where(item => item.Name == SampleLayoutName).SelectMany(item => item.Versions).Select(item => item.Id).SingleAsync();
        var exam = new Exam { Id = Guid.NewGuid(), CourseId = course.Id, ExamType = ExamType.Midterm, EvaluationType = EvaluationType.MultipleChoice, OpticalTemplateVersionId = versionId, CreatedAt = DateTime.UtcNow };
        context.Exams.Add(exam);
        await context.SaveChangesAsync();
        return new Setup(context, connection, instructor.Id, outsider.Id, exam.Id);
    }

    private sealed class Setup(SdsDbContext context, SqliteConnection connection, Guid instructorId, Guid outsiderId, Guid examId) : IAsyncDisposable
    {
        public SdsDbContext Context { get; } = context;
        public Guid OutsiderId { get; } = outsiderId;
        public Guid ExamId { get; } = examId;
        public StudentMatchingService Matching { get; } = new(context, new TestUser(instructorId));

        public async Task ImportFixtureAsync()
        {
            var service = new StudentDataImportService(Context, new TestUser(instructorId), new OpticalFileParser(), new ClassListParser());
            await using var optical = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "optik-ornek-anonim.txt"));
            await using var classList = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "sinif-listesi-ornek-anonim.xlsx"));
            await service.ImportAsync(ExamId, optical, "optik-ornek-anonim.txt", [new ClassListFileInput(classList, "sinif-listesi-ornek-anonim.xlsx")], default);
        }

        public async Task SeedImportAsync((string Number, string FirstName, string LastName)[] students, (string Number, string Name, bool IsValid)[] records)
        {
            var import = new ExamImport { Id = Guid.NewGuid(), ExamId = ExamId, OpticalFileName = "optik.txt", ClassListFileName = "liste.xlsx", CreatedAt = DateTime.UtcNow };
            import.ImportedStudents = students.Select((item, index) => new ImportedStudent { Id = Guid.NewGuid(), ExamImportId = import.Id, StudentNumber = item.Number, FirstName = item.FirstName, LastName = item.LastName, RowNumber = index + 2 }).ToList();
            import.OpticalExamRecords = records.Select((item, index) => new OpticalExamRecord { Id = Guid.NewGuid(), ExamImportId = import.Id, LineNumber = index + 1, StudentNumber = item.Number, StudentName = item.Name, BookletType = item.IsValid ? "A" : string.Empty, Answers = "ABCD", IsValid = item.IsValid, ErrorMessage = item.IsValid ? null : "Öğrenci numarası boş." }).ToList();
            Context.ExamImports.Add(import);
            await Context.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class TestUser(Guid id) : ICurrentUser
    {
        public CurrentUser Get() => new(id, UserRole.Instructor.ToString());
    }
}
