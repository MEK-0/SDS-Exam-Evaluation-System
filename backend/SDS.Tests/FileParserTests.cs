// Optik TXT ve OBİS XLSX parser davranışlarını doğrular.
using System.IO;
using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ClosedXML.Excel;
using SDS.Application.Imports;
using SDS.Infrastructure.Importing;
using Xunit;

namespace SDS.Tests;

public sealed class FileParserTests
{
    private static readonly OpticalFormConfigurationInput Configuration = new(0, 8, null, null, 8, 1, null, null, 9, 4, "utf-8");

    [Fact]
    public async Task OpticalParser_ReadsValidRecordWithLeadingZeroAndTurkishData()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("00123456AABCD\n"));
        var result = await new OpticalFileParser().ParseAsync(stream, Configuration, default);
        var record = Assert.Single(result.Records);
        Assert.True(record.IsValid);
        Assert.Equal("00123456", record.StudentNumber);
        Assert.Equal("A", record.BookletType);
        Assert.Equal("ABCD", record.Answers);
    }

    [Fact]
    public async Task OpticalParser_ReportsShortLineWithoutStoppingOtherRecords()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("00123456AABCD\nKISA\n00123457BBCDA\n"));
        var result = await new OpticalFileParser().ParseAsync(stream, Configuration, default);
        Assert.Equal(3, result.Records.Count);
        Assert.Equal(1, result.Records.Count(item => item.IsValid == false));
        Assert.Single(result.Issues);
    }

    [Fact]
    public async Task OpticalParser_ReportsEmptyFile()
    {
        await using var stream = new MemoryStream();
        var result = await new OpticalFileParser().ParseAsync(stream, Configuration, default);
        Assert.Empty(result.Records);
        Assert.Contains(result.Issues, item => item.Message == "Optik dosyası boş.");
    }

    [Fact]
    public async Task ClassListParser_ReadsHeadersAndPreservesLeadingZero()
    {
        await using var stream = CreateWorkbook(new[] { ("Öğrenci No", "Ad", "Soyad"), ("00123456", "Çağrı", "Öztürk") });
        var result = await new ClassListParser().ParseAsync(stream, default);
        var student = Assert.Single(result.Students);
        Assert.Equal("00123456", student.StudentNumber);
        Assert.Equal("Çağrı", student.FirstName);
        Assert.Equal("Öztürk", student.LastName);
    }

    [Fact]
    public async Task ClassListParser_ReportsDuplicateStudentNumber()
    {
        await using var stream = CreateWorkbook(new[] { ("Öğrenci No", "Ad", "Soyad"), ("00123456", "Çağrı", "Öztürk"), ("00123456", "Ayşe", "Yılmaz") });
        var result = await new ClassListParser().ParseAsync(stream, default);
        Assert.Single(result.Students);
        Assert.Contains(result.Issues, item => item.Message == "Öğrenci numarası tekrar ediyor.");
    }

    [Fact]
    public async Task ClassListParser_RejectsMissingHeaders()
    {
        await using var stream = CreateWorkbook(new[] { ("Numara", "Ad", "Soyad"), ("00123456", "Çağrı", "Öztürk") });
        await Assert.ThrowsAsync<ArgumentException>(() => new ClassListParser().ParseAsync(stream, default));
    }

    private static MemoryStream CreateWorkbook((string Number, string FirstName, string LastName)[] rows)
    {
        var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.AddWorksheet("Sınıf Listesi");
            for (var rowIndex = 0; rowIndex < rows.Length; rowIndex++)
            {
                var row = rows[rowIndex];
                sheet.Cell(rowIndex + 1, 1).Value = row.Number;
                sheet.Cell(rowIndex + 1, 2).Value = row.FirstName;
                sheet.Cell(rowIndex + 1, 3).Value = row.LastName;
            }
            workbook.SaveAs(stream);
        }
        stream.Position = 0;
        return stream;
    }
}
