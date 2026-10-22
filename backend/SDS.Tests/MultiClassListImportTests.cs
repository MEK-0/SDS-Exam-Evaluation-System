// İki sınıf listesinin tek import içinde birleştirilmesini doğrular.
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SDS.Application.Imports;
using SDS.Application.Security;
using SDS.Domain.Entities;
using SDS.Infrastructure.Importing;
using SDS.Infrastructure.Persistence;
using SDS.Infrastructure.Services;
using Xunit;

namespace SDS.Tests;

public sealed class MultiClassListImportTests
{
    [Fact]
    public async Task Import_MergesTwoClassListsAndReportsDuplicateStudentNumber()
    {
        await using var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        await using var context = CreateContext(connection);
        await context.Database.EnsureCreatedAsync();
        var user = new User { Id = Guid.NewGuid(), Username = "multi-import", DisplayName = "Instructor", Role = UserRole.Instructor, CreatedAt = DateTime.UtcNow };
        var year = new AcademicYear { Id = Guid.NewGuid(), Name = Guid.NewGuid().ToString(), IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "MULTI", Name = "Ders", AcademicYearId = year.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var exam = new Exam { Id = Guid.NewGuid(), CourseId = course.Id, ExamType = ExamType.Midterm, EvaluationType = EvaluationType.MultipleChoice, CreatedAt = DateTime.UtcNow };
        context.AddRange(user, year, course, exam);
        context.CourseInstructors.Add(new CourseInstructor { CourseId = course.Id, UserId = user.Id });
        await context.SaveChangesAsync();
        var service = new StudentDataImportService(context, new TestCurrentUser(user.Id), new OpticalFileParser(), new ClassListParser());
        var result = await ImportAsync(service, exam.Id);
        Assert.Equal(2, result.StudentCount);
        Assert.Contains(result.ClassListFileNames, name => name == "Sinif_Listesi_A.xlsx");
        Assert.Contains(result.Issues, issue => issue.Message.Contains("Mükerrer öğrenci numarası", StringComparison.Ordinal));
        var replacement = await ImportAsync(service, exam.Id);
        Assert.Equal(2, replacement.StudentCount);
        Assert.Equal(1, await context.ExamImports.CountAsync(item => item.ExamId == exam.Id));
        Assert.Equal(2, await context.ImportedStudents.CountAsync(item => item.ExamImport.ExamId == exam.Id));
    }

    private static async Task<ImportPreviewResponse> ImportAsync(StudentDataImportService service, Guid examId)
    {
        await using var optical = new MemoryStream(Encoding.UTF8.GetBytes("00123456AABCD\n"));
        await using var first = CreateWorkbook(("00123456", "Çağrı", "Öztürk"));
        await using var second = CreateWorkbook(("00123456", "Çağrı", "Öztürk"), ("00123457", "Ayşe", "Yılmaz"));
        return await service.ImportAsync(examId, optical, "optik.txt", [new ClassListFileInput(first, "Sinif_Listesi_A.xlsx"), new ClassListFileInput(second, "Sinif_Listesi_B.xlsx")], new OpticalFormConfigurationInput(0, 8, null, null, 8, 1, null, null, 9, 4, "utf-8"), default);
    }

    private static SdsDbContext CreateContext(SqliteConnection connection) => new(new DbContextOptionsBuilder<SdsDbContext>().UseSqlite(connection).Options);

    private static MemoryStream CreateWorkbook(params (string Number, string FirstName, string LastName)[] rows)
    {
        var stream = new MemoryStream();
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Sınıf Listesi");
        sheet.Cell(1, 1).Value = "Öğrenci No";
        sheet.Cell(1, 2).Value = "Ad";
        sheet.Cell(1, 3).Value = "Soyad";
        for (var index = 0; index < rows.Length; index++)
        {
            sheet.Cell(index + 2, 1).Value = rows[index].Number;
            sheet.Cell(index + 2, 2).Value = rows[index].FirstName;
            sheet.Cell(index + 2, 3).Value = rows[index].LastName;
        }
        workbook.SaveAs(stream);
        stream.Position = 0;
        return stream;
    }

    private sealed class TestCurrentUser(Guid userId) : ICurrentUser
    {
        public CurrentUser Get() => new(userId, UserRole.Instructor.ToString());
    }
}
