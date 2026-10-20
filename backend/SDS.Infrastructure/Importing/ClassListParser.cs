// OBİS XLSX sınıf listelerini başlık eşleme ve öğrenci kayıtlarına dönüştürür.
using ClosedXML.Excel;
using SDS.Application.Imports;

namespace SDS.Infrastructure.Importing;

public sealed class ClassListParser : IClassListParser
{
    public Task<ClassListParseResult> ParseAsync(Stream content, CancellationToken cancellationToken)
    {
        try
        {
            using var workbook = new XLWorkbook(content);
            var worksheet = workbook.Worksheets.FirstOrDefault() ?? throw new ArgumentException("XLSX dosyasında çalışma sayfası bulunamadı.");
            var usedRange = worksheet.RangeUsed() ?? throw new ArgumentException("XLSX dosyası boş.");
            var headers = FindHeaders(usedRange);
            var students = new List<ParsedStudent>();
            var issues = new List<ImportIssue>();
            for (var rowNumber = headers.RowNumber + 1; rowNumber <= usedRange.LastRow().RowNumber(); rowNumber++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var row = worksheet.Row(rowNumber);
                var values = new[] { row.Cell(headers.StudentNumberColumn).GetFormattedString().Trim(), row.Cell(headers.FirstNameColumn).GetFormattedString().Trim(), row.Cell(headers.LastNameColumn).GetFormattedString().Trim() };
                if (values.All(string.IsNullOrWhiteSpace)) continue;
                if (string.IsNullOrWhiteSpace(values[0])) { issues.Add(new ImportIssue("classList", rowNumber, "Öğrenci numarası boş.")); continue; }
                if (string.IsNullOrWhiteSpace(values[1]) || string.IsNullOrWhiteSpace(values[2])) { issues.Add(new ImportIssue("classList", rowNumber, "Ad veya soyad boş.")); continue; }
                if (students.Any(item => item.StudentNumber == values[0])) { issues.Add(new ImportIssue("classList", rowNumber, "Öğrenci numarası tekrar ediyor.")); continue; }
                students.Add(new ParsedStudent(rowNumber, values[0], values[1], values[2]));
            }
            return Task.FromResult(new ClassListParseResult(students, issues));
        }
        catch (ArgumentException) { throw; }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new ArgumentException("XLSX dosyası geçerli bir sınıf listesi olarak okunamadı.", exception);
        }
    }

    private static HeaderMap FindHeaders(IXLRange range)
    {
        for (var rowNumber = range.FirstRow().RowNumber(); rowNumber <= Math.Min(range.LastRow().RowNumber(), range.FirstRow().RowNumber() + 10); rowNumber++)
        {
            var row = range.Worksheet.Row(rowNumber);
            var columns = row.CellsUsed().ToDictionary(item => Normalize(item.GetFormattedString()), item => item.Address.ColumnNumber);
            var studentNumber = FindColumn(columns, "öğrenci numarası", "ogrenci numarasi", "öğrenci no", "ogrenci no", "student number", "studentnumber");
            var firstName = FindColumn(columns, "ad", "first name", "firstname");
            var lastName = FindColumn(columns, "soyad", "soyadı", "soyadi", "last name", "lastname");
            if (studentNumber.HasValue && firstName.HasValue && lastName.HasValue) return new HeaderMap(rowNumber, studentNumber.Value, firstName.Value, lastName.Value);
        }
        throw new ArgumentException("XLSX başlıklarında öğrenci numarası, ad ve soyad sütunları bulunamadı.");
    }

    private static int? FindColumn(IReadOnlyDictionary<string, int> columns, params string[] names) => names.Select(Normalize).Where(columns.ContainsKey).Select(item => columns[item]).FirstOrDefault() is var column && column > 0 ? column : null;
    private static string Normalize(string value) => value.Trim().ToLowerInvariant().Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);
    private sealed record HeaderMap(int RowNumber, int StudentNumberColumn, int FirstNameColumn, int LastNameColumn);
}
