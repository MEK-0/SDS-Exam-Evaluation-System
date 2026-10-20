// Sabit genişlikli optik TXT dosyalarını yapılandırılmış kayıtlara dönüştürür.
using System.Text;
using SDS.Application.Imports;

namespace SDS.Infrastructure.Importing;

public sealed class OpticalFileParser : IOpticalFileParser
{
    public async Task<OpticalParseResult> ParseAsync(Stream content, OpticalFormConfigurationInput configuration, CancellationToken cancellationToken)
    {
        ValidateConfiguration(configuration);
        var encoding = ResolveEncoding(configuration.EncodingName);
        using var reader = new StreamReader(content, encoding, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var records = new List<ParsedOpticalRecord>();
        var issues = new List<ImportIssue>();
        var lineNumber = 0;
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            lineNumber++;
            var result = ParseLine(line, lineNumber, configuration);
            records.Add(result);
            if (!result.IsValid) issues.Add(new ImportIssue("optical", lineNumber, result.ErrorMessage ?? "Optik satır doğrulanamadı."));
        }
        if (lineNumber == 0) issues.Add(new ImportIssue("optical", null, "Optik dosyası boş."));
        return new OpticalParseResult(records, issues);
    }

    private static ParsedOpticalRecord ParseLine(string line, int lineNumber, OpticalFormConfigurationInput configuration)
    {
        if (string.IsNullOrWhiteSpace(line)) return new ParsedOpticalRecord(lineNumber, string.Empty, string.Empty, string.Empty, string.Empty, false, "Satır boş.");
        try
        {
            var studentNumber = Slice(line, configuration.StudentNumberStart, configuration.StudentNumberLength, "öğrenci numarası").Trim();
            var studentName = configuration.StudentNameStart.HasValue && configuration.StudentNameLength.HasValue ? Slice(line, configuration.StudentNameStart.Value, configuration.StudentNameLength.Value, "öğrenci adı").Trim() : string.Empty;
            var bookletType = Slice(line, configuration.BookletTypeStart, configuration.BookletTypeLength, "kitapçık türü").Trim();
            var answers = Slice(line, configuration.AnswersStart, configuration.AnswersLength).TrimEnd();
            if (string.IsNullOrWhiteSpace(studentNumber)) return new ParsedOpticalRecord(lineNumber, studentNumber, studentName, bookletType, answers, false, "Öğrenci numarası boş.");
            if (string.IsNullOrWhiteSpace(bookletType)) return new ParsedOpticalRecord(lineNumber, studentNumber, studentName, bookletType, answers, false, "Kitapçık türü boş.");
            return new ParsedOpticalRecord(lineNumber, studentNumber, studentName, bookletType, answers, true, null);
        }
        catch (FormatException exception)
        {
            return new ParsedOpticalRecord(lineNumber, string.Empty, string.Empty, string.Empty, string.Empty, false, exception.Message);
        }
    }

    private static string Slice(string line, int start, int length, string fieldName = "alan")
    {
        if (start < 0 || length <= 0 || start > line.Length || line.Length - start < length) throw new FormatException($"{fieldName} beklenen uzunlukta değil.");
        return line.Substring(start, length);
    }

    private static void ValidateConfiguration(OpticalFormConfigurationInput configuration)
    {
        if (configuration.StudentNumberStart < 0 || configuration.StudentNumberLength <= 0 || configuration.BookletTypeStart < 0 || configuration.BookletTypeLength <= 0 || configuration.AnswersStart < 0 || configuration.AnswersLength <= 0) throw new ArgumentException("Optik format yapılandırmasındaki alan konumları geçersiz.");
        if (configuration.StudentNameStart.HasValue != configuration.StudentNameLength.HasValue || configuration.ExamTypeStart.HasValue != configuration.ExamTypeLength.HasValue) throw new ArgumentException("Opsiyonel optik alanların başlangıç ve uzunluk değerleri birlikte sağlanmalıdır.");
    }

    private static Encoding ResolveEncoding(string encodingName)
    {
        try { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); return Encoding.GetEncoding(string.IsNullOrWhiteSpace(encodingName) ? "utf-8" : encodingName); }
        catch (ArgumentException exception) { throw new ArgumentException("Optik dosyası için desteklenmeyen karakter kodlaması.", exception); }
    }
}
