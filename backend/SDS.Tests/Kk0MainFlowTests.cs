// Anonim örnek optik ve sınıf listesiyle ana akışı (içe aktarma, anahtar, eşleştirme, not, ÖÇ, analiz, dönem) servis zinciriyle doğrular.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SDS.Application.AnswerKeys;
using SDS.Application.Exams;
using SDS.Application.Grades;
using SDS.Application.Imports;
using SDS.Application.LearningOutcomes;
using SDS.Application.Matching;
using SDS.Application.Security;
using SDS.Domain.Entities;
using SDS.Infrastructure.Importing;
using SDS.Infrastructure.Persistence;
using SDS.Infrastructure.Seeding;
using SDS.Infrastructure.Services;
using Xunit;

namespace SDS.Tests;

public sealed class Kk0MainFlowTests
{
    private const string SampleLayoutName = "Örnek dosya düzeni (ornek_optik_cikti.txt)";
    private const int QuestionCount = 25;

    [Fact]
    public async Task KK0_FullMultipleChoiceFlow_ProducesExpectedAnalysis()
    {
        await using var flow = await Flow.CreateAsync();
        var result = await flow.RunAsync(ExamType.Midterm, File.ReadAllBytes(FixturePath("optik-ornek-anonim.txt")));
        AssertMidtermResults(result);
    }

    [Fact]
    public async Task KK0_FullFlowWithWindows1254File_SameResults()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var utf8Text = File.ReadAllText(FixturePath("optik-ornek-anonim.txt"), new UTF8Encoding(false));
        var bytes = Encoding.GetEncoding(1254).GetBytes(utf8Text);
        Assert.NotEqual(File.ReadAllBytes(FixturePath("optik-ornek-anonim.txt")), bytes);
        await using var flow = await Flow.CreateAsync();
        var result = await flow.RunAsync(ExamType.Midterm, bytes);
        AssertMidtermResults(result);
    }

    [Fact]
    public async Task KK0_TermEvaluation_ProducesExpectedWeightedAverage()
    {
        await using var flow = await Flow.CreateAsync();
        await flow.RunAsync(ExamType.Midterm, File.ReadAllBytes(FixturePath("optik-ornek-anonim.txt")));
        var final = await flow.RunAsync(ExamType.Final, BuildFinalOptical());
        Assert.Equal([100m, 96m, 88m, 80m, 76m, 68m, 60m, 56m, 48m, 40m, 36m, 28m, 20m, 16m, 8m], final.Scores);
        var report = await flow.Terms.GetAsync(flow.CourseId, default);
        Assert.Equal(17, report!.Summary.TotalStudents);
        Assert.Equal(15, report.Summary.ParticipatingStudents);
        Assert.Equal(44.85m, decimal.Round(report.Summary.WeightedAverage!.Value, 2));
        Assert.Equal(40m, report.Summary.SuccessRate);
        Assert.Equal(30.13m, report.Summary.MidtermAverage);
        Assert.Equal(54.67m, report.Summary.FinalAverage);
        var letters = report.LetterDistribution.ToDictionary(item => item.Letter, item => item.Count);
        Assert.Equal(new Dictionary<string, int> { ["BA"] = 1, ["CB"] = 1, ["CC"] = 1, ["DC"] = 3, ["FF"] = 9 }, letters);
        var best = report.Students.Single(item => item.WeightedScore == 86.4m);
        Assert.Equal(86.4m, best.WeightedScore);
        Assert.Equal("BA", best.LetterGrade);
    }

    [Fact]
    public async Task KK0_FlowEndpoints_UnassignedInstructorGetsNull()
    {
        await using var flow = await Flow.CreateAsync();
        await flow.RunAsync(ExamType.Midterm, File.ReadAllBytes(FixturePath("optik-ornek-anonim.txt")));
        var outsider = new TestOutsider(flow.OutsiderId);
        Assert.Null(await new StudentMatchingService(flow.Context, outsider).GetAsync(flow.LastExamId, default));
        Assert.Null(await new StudentMatchingService(flow.Context, outsider).ConfirmAsync(flow.LastExamId, default));
        Assert.Null(await new GradeService(flow.Context, outsider).GetAsync(flow.LastExamId, default));
        Assert.Null(await new AnalysisService(flow.Context, outsider).GetAsync(flow.LastExamId, null, default));
        Assert.Null(await new LearningOutcomeService(flow.Context, outsider).GetExamPageAsync(flow.LastExamId, default));
        Assert.Null(await new AnswerKeyService(flow.Context, outsider).GetAsync(flow.LastExamId, default));
    }

    private static void AssertMidtermResults(FlowResult result)
    {
        Assert.Equal(2, result.PreviewKeyRows);
        Assert.Equal(17, result.ValidRecords);
        Assert.Equal(2, result.SimilarSuggestions);
        Assert.Equal(2, result.AbsentSuggestions);
        Assert.Equal(15, result.GradedCount);
        Assert.Equal(2, result.AbsentCount);
        // Her öğrenci kendi kitapçığının anahtarıyla puanlanır; 25 soru x 4 puan, sırayla satır 3..17 (satır 11 öneriyle eşleşir).
        Assert.Equal([24m, 44m, 84m, 24m, 20m, 24m, 12m, 8m, 36m, 40m, 0m, 24m, 4m, 28m, 80m], result.Scores);
        Assert.Equal(30.13m, decimal.Round(result.Average, 2));
        Assert.Equal(24m, result.Median);
        // Varsayılan politika: 90/85/80/70/60/50; 84 ve 80 BB, kalan 13 öğrenci FF.
        Assert.Equal(new Dictionary<string, int> { ["BB"] = 2, ["FF"] = 13 }, result.Letters);
        Assert.Equal(15, result.AnalysisParticipating);
        Assert.Equal(30.13m, decimal.Round(result.AnalysisAverage, 2));
        Assert.Equal(24m, result.AnalysisMedian);
        Assert.Equal(ExamStatus.Completed, result.FinalStatus);
    }

    private static byte[] BuildFinalOptical()
    {
        var text = File.ReadAllText(FixturePath("optik-ornek-anonim.txt"), new UTF8Encoding(false));
        var lines = text.Split("\r\n");
        var keys = lines.Where(line => line[..9] == "000000000").ToDictionary(line => line[20], line => line.Substring(42, QuestionCount));
        var output = new List<string>();
        var index = 0;
        foreach (var line in lines)
        {
            if (line[..9] == "000000000")
            {
                output.Add(line);
                continue;
            }
            var correct = QuestionCount - 5 * index / 3;
            var key = keys[line[20]];
            var answers = new string(key.Select((answer, position) => position < correct ? answer : answer == 'E' ? 'A' : 'E').ToArray());
            var number = line[..9] switch
            {
                "3064578 9" => "306457819",
                "326407866" => "396407866",
                var other => other
            };
            var rebuilt = number + line[9..42] + answers + line[(42 + QuestionCount)..];
            output.Add(rebuilt);
            index++;
        }
        return new UTF8Encoding(false).GetBytes(string.Join("\r\n", output));
    }

    private static string FixturePath(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private sealed record FlowResult(int PreviewKeyRows, int ValidRecords, int SimilarSuggestions, int AbsentSuggestions, int GradedCount, int AbsentCount, IReadOnlyList<decimal> Scores, decimal Average, decimal Median, IReadOnlyDictionary<string, int> Letters, int AnalysisParticipating, decimal AnalysisAverage, decimal AnalysisMedian, ExamStatus FinalStatus);

    private sealed class TestOutsider(Guid id) : ICurrentUser
    {
        public CurrentUser Get() => new(id, UserRole.Instructor.ToString());
    }

    private sealed class Flow : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly ICurrentUser instructor;
        private readonly Guid templateId;

        private Flow(SqliteConnection connection, SdsDbContext context, Guid courseId, Guid instructorId, Guid outsiderId, Guid templateId)
        {
            this.connection = connection;
            this.templateId = templateId;
            Context = context;
            CourseId = courseId;
            OutsiderId = outsiderId;
            instructor = new TestOutsider(instructorId);
        }

        public SdsDbContext Context { get; }
        public Guid CourseId { get; }
        public Guid OutsiderId { get; }
        public Guid LastExamId { get; private set; }
        public TermAnalysisService Terms => new(Context, instructor);

        public static async Task<Flow> CreateAsync()
        {
            var connection = new SqliteConnection("DataSource=:memory:");
            await connection.OpenAsync();
            var context = new SdsDbContext(new DbContextOptionsBuilder<SdsDbContext>().UseSqlite(connection).Options);
            await context.Database.EnsureCreatedAsync();
            var teacher = new User { Id = Guid.NewGuid(), Username = "ogretmen-" + Guid.NewGuid(), DisplayName = "Öğretim Elemanı", Role = UserRole.Instructor, CreatedAt = DateTime.UtcNow };
            var outsider = new User { Id = Guid.NewGuid(), Username = "dis-" + Guid.NewGuid(), DisplayName = "Atanmamış", Role = UserRole.Instructor, CreatedAt = DateTime.UtcNow };
            var year = new AcademicYear { Id = Guid.NewGuid(), Name = Guid.NewGuid().ToString(), IsActive = true };
            var course = new Course { Id = Guid.NewGuid(), Code = "KK0101", Name = "Ders", AcademicYearId = year.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            context.AddRange(teacher, outsider, year, course);
            context.CourseInstructors.Add(new CourseInstructor { CourseId = course.Id, UserId = teacher.Id });
            context.GradingPolicies.Add(new GradingPolicy { Id = Guid.NewGuid(), AaMinimum = 90, BaMinimum = 85, BbMinimum = 80, CbMinimum = 70, CcMinimum = 60, DcMinimum = 50, MidtermWeight = 40, FinalWeight = 60, MakeUpWeight = 60, UpdatedAt = DateTime.UtcNow });
            await context.SaveChangesAsync();
            await new OpticalTemplateSeeder(context).SeedAsync(default);
            var template = await context.OpticalTemplates.AsNoTracking().SingleAsync(item => item.Name == SampleLayoutName);
            return new Flow(connection, context, course.Id, teacher.Id, outsider.Id, template.Id);
        }

        public async Task<FlowResult> RunAsync(ExamType type, byte[] opticalBytes)
        {
            var exam = await new ExamService(Context, instructor).CreateExamAsync(CourseId, new CreateExamRequest { ExamType = type, EvaluationType = EvaluationType.MultipleChoice, OpticalTemplateId = templateId }, default);
            var examId = exam!.Id;
            LastExamId = examId;
            var importService = new StudentDataImportService(Context, instructor, new OpticalFileParser(), new ClassListParser());
            await using var optical = new MemoryStream(opticalBytes);
            await using var classList = File.OpenRead(FixturePath("sinif-listesi-ornek-anonim.xlsx"));
            var preview = await importService.ImportAsync(examId, optical, "optik.txt", [new ClassListFileInput(classList, "sinif-listesi-ornek-anonim.xlsx")], default);
            var keyRows = (await importService.GetOpticalRecordsAsync(examId, default))!.Where(item => item.IsAnswerKey).ToList();
            var request = new UpdateAnswerKeyRequest
            {
                MaximumScore = 100,
                QuestionCount = QuestionCount,
                Questions = Enumerable.Range(1, QuestionCount).Select(number => new AnswerKeyQuestionRequest { CanonicalQuestionNumber = number }).ToList(),
                Booklets = keyRows.Select(row => new AnswerKeyBookletRequest
                {
                    BookletCode = row.BookletType,
                    Questions = row.Answers.Select((answer, index) => new BookletQuestionRequest { ExamQuestionNumber = index + 1, DisplayQuestionNumber = index + 1, AcceptedAnswers = [answer.ToString()] }).ToList()
                }).ToList()
            };
            var key = await new AnswerKeyService(Context, instructor).UpdateAsync(examId, request, default);
            Assert.Equal(AnswerKeyStatus.Validated, key!.Status);
            var matching = new StudentMatchingService(Context, instructor);
            var run = await matching.RunAsync(examId, default);
            var similar = run!.Suggestions.Where(item => item.Kind == MatchSuggestionKind.SimilarNumber).ToList();
            var absent = run.Suggestions.Count(item => item.Kind == MatchSuggestionKind.Absent);
            foreach (var suggestion in similar) await matching.AcceptSuggestionAsync(examId, new AcceptSuggestionRequest { ImportedStudentId = suggestion.ImportedStudentId, OpticalExamRecordId = suggestion.OpticalExamRecordId!.Value }, default);
            await matching.AcceptAbsentSuggestionsAsync(examId, default);
            var confirmed = await matching.ConfirmAsync(examId, default);
            Assert.True(confirmed!.Summary.IsConfirmed);
            var grades = new GradeService(Context, instructor);
            var calculated = await grades.CalculateAsync(examId, default);
            var graded = calculated!.Rows.Where(item => item.Status == GradeStatus.Graded).OrderBy(item => item.StudentNumber, StringComparer.Ordinal).ToList();
            var outcomes = new LearningOutcomeService(Context, instructor);
            var existing = await outcomes.GetCourseOutcomesAsync(CourseId, default);
            var outcome = existing!.FirstOrDefault() ?? await outcomes.CreateOutcomeAsync(CourseId, new CreateLearningOutcomeRequest { Description = "Temel kavramları uygular" }, default);
            var reverse = await outcomes.ApplyReverseMappingAsync(examId, new ReverseBookletMappingRequest { BookletCode = "B" }, default);
            var complete = new CompleteExamRequest
            {
                BookletMappings = new SaveBookletQuestionMappingsRequest { Mappings = reverse!.BookletMappings.Select(item => new BookletMappingInput { ReferenceBookletCode = item.ReferenceBookletCode, BookletCode = item.BookletCode, Items = item.Items.ToList() }).ToList() },
                LearningOutcomeMappings = new SaveLearningOutcomeMappingsRequest { Mappings = [new LearningOutcomeQuestionMapInput { LearningOutcomeId = outcome!.Id, QuestionNumbers = Enumerable.Range(1, QuestionCount).ToList() }] }
            };
            var completed = await outcomes.CompleteExamAsync(examId, complete, default);
            Assert.True(completed!.IsComplete);
            var analysis = await new AnalysisService(Context, instructor).GetAsync(examId, null, default);
            var status = await Context.Exams.AsNoTracking().Where(item => item.Id == examId).Select(item => item.Status).SingleAsync();
            var scores = graded.Select(item => item.AdjustedScore!.Value).ToList();
            var ordered = scores.OrderBy(item => item).ToList();
            var letters = graded.GroupBy(item => item.LetterGrade!).ToDictionary(item => item.Key, item => item.Count());
            var rowsByLine = await ScoresInOpticalOrderAsync(examId, calculated);
            return new FlowResult(keyRows.Count, preview.ValidOpticalRecordCount, similar.Count, absent, graded.Count, calculated.Rows.Count(item => item.Status == GradeStatus.Absent), rowsByLine, calculated.Summary.AverageScore!.Value, calculated.Summary.MedianScore!.Value, letters, analysis!.Summary.ParticipatingStudents, analysis.Summary.Average!.Value, analysis.Summary.Median!.Value, status);
        }

        private async Task<IReadOnlyList<decimal>> ScoresInOpticalOrderAsync(Guid examId, GradesResponse grades)
        {
            var matches = await Context.StudentMatches.AsNoTracking().Include(item => item.OpticalExamRecord).Where(item => item.ExamId == examId && item.OpticalExamRecordId != null).ToListAsync();
            var byStudent = grades.Rows.ToDictionary(item => item.ImportedStudentId);
            return matches.OrderBy(item => item.OpticalExamRecord!.LineNumber).Select(item => byStudent[item.ImportedStudentId!.Value].AdjustedScore!.Value).ToList();
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
