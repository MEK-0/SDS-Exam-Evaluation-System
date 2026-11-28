// Sabit genişlikli optik TXT dosyalarını karakter bazlı okuyup yapılandırılmış kayıtlara dönüştürür.
using System.Text;
using SDS.Application.Imports;
using SDS.Application.OpticalTemplates;

namespace SDS.Infrastructure.Importing;

public sealed class OpticalFileParser : IOpticalFileParser
{
    public async Task<OpticalParseResult> ParseAsync(Stream content, OpticalFormConfigurationInput configuration, CancellationToken cancellationToken)
    {
        ValidateConfiguration(configuration);
        var text = await DecodeAsync(content, configuration.EncodingName, cancellationToken);
        using var reader = new StringReader(text);
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
        if (line.StartsWith("ANSWER_KEY_", StringComparison.OrdinalIgnoreCase)) return ParseAnswerKeyLine(line, lineNumber);
        var padded = line.PadRight(GetRequiredLength(configuration), ' ');
        var studentNumber = Slice(padded, configuration.StudentNumberStart, configuration.StudentNumberLength).Trim();
        var studentName = configuration.StudentNameStart.HasValue && configuration.StudentNameLength.HasValue
            ? Slice(padded, configuration.StudentNameStart.Value, configuration.StudentNameLength.Value).Trim()
            : string.Empty;
        var bookletType = Slice(padded, configuration.BookletTypeStart, configuration.BookletTypeLength).Trim();
        var examType = configuration.ExamTypeStart.HasValue && configuration.ExamTypeLength.HasValue
            ? Slice(padded, configuration.ExamTypeStart.Value, configuration.ExamTypeLength.Value).Trim()
            : string.Empty;
        var answers = Slice(padded, configuration.AnswersStart, configuration.AnswersLength);
        var blankCount = CountMarks(answers, configuration.BlankMarkChar);
        var doubleCount = CountMarks(answers, configuration.DoubleMarkChar);
        if (string.IsNullOrWhiteSpace(studentNumber)) return new ParsedOpticalRecord(lineNumber, studentNumber, studentName, bookletType, answers, false, "Öğrenci numarası boş.", false, examType, blankCount, doubleCount);
        if (string.IsNullOrWhiteSpace(bookletType)) return new ParsedOpticalRecord(lineNumber, studentNumber, studentName, bookletType, answers, false, "Kitapçık türü boş.", false, examType, blankCount, doubleCount);
        var isAnswerKey = studentNumber.All(character => character == '0');
        return new ParsedOpticalRecord(lineNumber, studentNumber, studentName, bookletType, answers, true, null, isAnswerKey, examType, blankCount, doubleCount);
    }

    private static ParsedOpticalRecord ParseAnswerKeyLine(string line, int lineNumber)
    {
        var separatorIndex = line.IndexOf('|');
        var header = separatorIndex > 0 ? line[..separatorIndex].Trim().ToUpperInvariant() : line.Trim().ToUpperInvariant();
        var bookletType = header switch
        {
            "ANSWER_KEY_A" => "A",
            "ANSWER_KEY_B" => "B",
            _ => string.Empty
        };
        if (string.IsNullOrWhiteSpace(bookletType)) return new ParsedOpticalRecord(lineNumber, string.Empty, string.Empty, string.Empty, string.Empty, false, "Cevap anahtarı satırı A veya B kitapçığı olarak tanınamadı.", true);
        var answers = separatorIndex > 0 ? line[(separatorIndex + 1)..].Trim() : string.Empty;
        if (answers.Length != 25 || answers.Any(answer => answer is not ('A' or 'B' or 'C' or 'D' or 'E' or 'a' or 'b' or 'c' or 'd' or 'e'))) return new ParsedOpticalRecord(lineNumber, string.Empty, string.Empty, bookletType, answers, false, "Cevap anahtarı satırı 25 adet A-E seçeneği içermelidir.", true);
        return new ParsedOpticalRecord(lineNumber, string.Empty, string.Empty, bookletType, answers.ToUpperInvariant(), true, null, true);
    }

    private static int CountMarks(string answers, char mark) => answers.Count(character => character == mark);

    private static int GetRequiredLength(OpticalFormConfigurationInput configuration)
    {
        var ends = new List<int>
        {
            configuration.StudentNumberStart + configuration.StudentNumberLength,
            configuration.BookletTypeStart + configuration.BookletTypeLength,
            configuration.AnswersStart + configuration.AnswersLength
        };
        if (configuration.StudentNameStart.HasValue && configuration.StudentNameLength.HasValue) ends.Add(configuration.StudentNameStart.Value + configuration.StudentNameLength.Value);
        if (configuration.ExamTypeStart.HasValue && configuration.ExamTypeLength.HasValue) ends.Add(configuration.ExamTypeStart.Value + configuration.ExamTypeLength.Value);
        return ends.Max();
    }

    private static string Slice(string line, int start, int length) => line.Substring(start, length);

    private static void ValidateConfiguration(OpticalFormConfigurationInput configuration)
    {
        if (configuration.StudentNumberStart < 0 || configuration.StudentNumberLength <= 0 || configuration.BookletTypeStart < 0 || configuration.BookletTypeLength <= 0 || configuration.AnswersStart < 0 || configuration.AnswersLength <= 0) throw new ArgumentException("Optik format yapılandırmasındaki alan konumları geçersiz.");
        if (configuration.StudentNameStart.HasValue != configuration.StudentNameLength.HasValue || configuration.ExamTypeStart.HasValue != configuration.ExamTypeLength.HasValue) throw new ArgumentException("Opsiyonel optik alanların başlangıç ve uzunluk değerleri birlikte sağlanmalıdır.");
    }

    private static async Task<string> DecodeAsync(Stream content, string encodingName, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var text = IsAutomatic(encodingName) ? DecodeAutomatically(bytes) : DecodeForced(bytes, encodingName);
        return text.TrimStart('﻿');
    }

    private static bool IsAutomatic(string encodingName) => string.IsNullOrWhiteSpace(encodingName) || string.Equals(encodingName.Trim(), OpticalEncodingNames.Auto, StringComparison.OrdinalIgnoreCase);

    private static string DecodeAutomatically(byte[] bytes)
    {
        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            return Encoding.GetEncoding(OpticalEncodingNames.Windows1254).GetString(bytes);
        }
    }

    private static string DecodeForced(byte[] bytes, string encodingName)
    {
        try
        {
            return Encoding.GetEncoding(encodingName.Trim()).GetString(bytes);
        }
        catch (ArgumentException exception)
        {
            throw new ArgumentException("Optik dosyası için desteklenmeyen karakter kodlaması.", exception);
        }
    }
}
