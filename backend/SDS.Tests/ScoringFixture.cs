// Puanlama ve kitapçık testleri için gerçek servislerle çalışan SQLite in-memory sınav düzeneği sağlar.
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

namespace SDS.Tests;

internal sealed class FixtureUser(Guid id, UserRole role) : ICurrentUser
{
    public CurrentUser Get() => new(id, role.ToString());
}

internal sealed class ScoringFixture : IAsyncDisposable
{
    private readonly SqliteConnection connection;
    private readonly Guid importId;
    private int lineNumber;

    private ScoringFixture(SqliteConnection connection, SdsDbContext context, Guid examId, Guid courseId, Guid userId, Guid outsiderId, Guid importId)
    {
        this.connection = connection;
        this.importId = importId;
        Context = context;
        ExamId = examId;
        CourseId = courseId;
        UserId = userId;
        OutsiderId = outsiderId;
    }

    public SdsDbContext Context { get; }
    public Guid ExamId { get; }
    public Guid CourseId { get; }
    public Guid UserId { get; }
    public Guid OutsiderId { get; }
    public ICurrentUser Instructor => new FixtureUser(UserId, UserRole.Instructor);
    public ICurrentUser Outsider => new FixtureUser(OutsiderId, UserRole.Instructor);
    public GradeService Grades => new(Context, Instructor);
    public AnswerKeyService AnswerKeys => new(Context, Instructor);
    public LearningOutcomeService Outcomes => new(Context, Instructor);
    public AnalysisService Analysis => new(Context, Instructor);
    public CancellationSuggestionService Suggestions => new(Context, Instructor, Grades);

    public static async Task<ScoringFixture> CreateAsync()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var context = new SdsDbContext(new DbContextOptionsBuilder<SdsDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var user = new User { Id = Guid.NewGuid(), Username = Guid.NewGuid().ToString(), DisplayName = "Eğitmen", Role = UserRole.Instructor, CreatedAt = DateTime.UtcNow };
        var outsider = new User { Id = Guid.NewGuid(), Username = Guid.NewGuid().ToString(), DisplayName = "Atanmamış", Role = UserRole.Instructor, CreatedAt = DateTime.UtcNow };
        var year = new AcademicYear { Id = Guid.NewGuid(), Name = Guid.NewGuid().ToString(), IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = Guid.NewGuid().ToString(), Name = "Ders", AcademicYearId = year.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var templateVersionId = TestOpticalTemplates.AddVersion(context);
        var exam = new Exam { Id = Guid.NewGuid(), OpticalTemplateVersionId = templateVersionId, CourseId = course.Id, ExamType = ExamType.Midterm, EvaluationType = EvaluationType.MultipleChoice, MatchingConfirmed = true, CreatedAt = DateTime.UtcNow };
        var import = new ExamImport { Id = Guid.NewGuid(), ExamId = exam.Id, OpticalFileName = "optik.txt", ClassListFileName = "sinif.xlsx", CreatedAt = DateTime.UtcNow };
        context.AddRange(user, outsider, year, course, exam, import);
        context.CourseInstructors.Add(new CourseInstructor { CourseId = course.Id, UserId = user.Id });
        await context.SaveChangesAsync();
        return new ScoringFixture(connection, context, exam.Id, course.Id, user.Id, outsider.Id, import.Id);
    }

    public static UpdateAnswerKeyRequest KeyRequest(decimal maximumScore, int questionCount, params (string Code, string Answers)[] booklets)
    {
        return KeyRequest(maximumScore, questionCount, booklets.Select(item => (item.Code, item.Answers, Enumerable.Range(1, questionCount).ToArray())).ToArray());
    }

    public static UpdateAnswerKeyRequest KeyRequest(decimal maximumScore, int questionCount, params (string Code, string Answers, int[] Canonical)[] booklets)
    {
        return new UpdateAnswerKeyRequest
        {
            MaximumScore = maximumScore,
            QuestionCount = questionCount,
            Questions = Enumerable.Range(1, questionCount).Select(number => new AnswerKeyQuestionRequest { CanonicalQuestionNumber = number }).ToList(),
            Booklets = booklets.Select(item => new AnswerKeyBookletRequest
            {
                BookletCode = item.Code,
                Questions = item.Answers.Select((answer, index) => new BookletQuestionRequest { ExamQuestionNumber = item.Canonical[index], DisplayQuestionNumber = index + 1, AcceptedAnswers = [answer.ToString()] }).ToList()
            }).ToList()
        };
    }

    public async Task<AnswerKeyResponse> SaveKeyAsync(UpdateAnswerKeyRequest request)
    {
        var response = await AnswerKeys.UpdateAsync(ExamId, request, default);
        return response!;
    }

    public async Task<Guid> AddStudentAsync(string number, string booklet, string answers)
    {
        lineNumber++;
        var student = new ImportedStudent { Id = Guid.NewGuid(), ExamImportId = importId, StudentNumber = number, FirstName = "Öğrenci", LastName = number, SourceFileName = "sinif.xlsx", RowNumber = lineNumber + 1 };
        var record = new OpticalExamRecord { Id = Guid.NewGuid(), ExamImportId = importId, StudentNumber = number, BookletType = booklet, Answers = answers, IsValid = true, LineNumber = lineNumber };
        Context.AddRange(student, record);
        Context.StudentMatches.Add(new StudentMatch { Id = Guid.NewGuid(), ExamId = ExamId, ImportedStudentId = student.Id, OpticalExamRecordId = record.Id, MatchType = StudentMatchType.Exact, Status = StudentMatchStatus.Matched, MatchedAt = DateTime.UtcNow, MatchedByUserId = UserId });
        await Context.SaveChangesAsync();
        return student.Id;
    }

    public async Task<IReadOnlyDictionary<string, SDS.Application.Grades.GradeRowResponse>> CalculateAsync()
    {
        var response = await Grades.CalculateAsync(ExamId, default);
        return response!.Rows.ToDictionary(item => item.StudentNumber);
    }

    public async ValueTask DisposeAsync()
    {
        await Context.DisposeAsync();
        await connection.DisposeAsync();
    }
}
