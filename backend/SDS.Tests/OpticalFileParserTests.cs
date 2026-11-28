// Optik parser'ın kodlama algılama, kısa satır, sınav türü, cevap anahtarı satırı ve boş/çift işaret davranışlarını doğrular.
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SDS.Application.Imports;
using SDS.Infrastructure.Importing;
using Xunit;

namespace SDS.Tests;

public sealed class OpticalFileParserTests
{
    private static OpticalFormConfigurationInput SampleLayout(string encoding = "auto", char blank = ' ', char doubleMark = '*')
    {
        return new OpticalFormConfigurationInput(0, 9, 21, 20, 20, 1, 18, 1, 42, 25, encoding, blank, doubleMark);
    }

    private static byte[] FixtureBytes() => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "optik-ornek-anonim.txt"));

    private static byte[] Windows1254Bytes()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1254).GetBytes(Encoding.UTF8.GetString(FixtureBytes()));
    }

    private static async Task<OpticalParseResult> ParseAsync(byte[] bytes, OpticalFormConfigurationInput configuration)
    {
        await using var stream = new MemoryStream(bytes);
        return await new OpticalFileParser().ParseAsync(stream, configuration, default);
    }

    private static Task<OpticalParseResult> ParseTextAsync(string text, OpticalFormConfigurationInput? configuration = null)
    {
        return ParseAsync(new UTF8Encoding(false).GetBytes(text), configuration ?? SampleLayout());
    }

    private static string Line(string number, string examType, string booklet, string name, string answers)
    {
        return number.PadRight(9) + new string(' ', 9) + examType.PadRight(1) + " " + booklet + name.PadRight(20) + " " + answers.PadRight(25);
    }

    [Fact]
    public async Task KK4_K18_DetectsUtf8ThenFallsBackTo1254()
    {
        var utf8 = await ParseAsync(FixtureBytes(), SampleLayout());
        var legacyBytes = Windows1254Bytes();
        Assert.Throws<DecoderFallbackException>(() => new UTF8Encoding(false, true).GetString(legacyBytes));
        var legacy = await ParseAsync(legacyBytes, SampleLayout());
        Assert.Equal(17, utf8.Records.Count);
        Assert.All(utf8.Records, item => Assert.True(item.IsValid));
        Assert.Equal("DECYUM AMİ HÖKOT", utf8.Records.Single(item => item.LineNumber == 6).StudentName);
        Assert.Equal(utf8.Records.Select(item => item.StudentName), legacy.Records.Select(item => item.StudentName));
        Assert.Equal(utf8.Records.Select(item => item.Answers), legacy.Records.Select(item => item.Answers));
        Assert.All(legacy.Records.Where(item => !item.IsAnswerKey), item => Assert.Equal(25, item.Answers.Length));
    }

    [Fact]
    public async Task KK4_K18_ForcedEncodingIsRespected()
    {
        var utf8 = await ParseAsync(FixtureBytes(), SampleLayout());
        var legacyBytes = Windows1254Bytes();
        var forcedWrongOnUtf8 = await ParseAsync(FixtureBytes(), SampleLayout("windows-1254"));
        var forcedWrongOnLegacy = await ParseAsync(legacyBytes, SampleLayout("utf-8"));
        var forcedRightOnLegacy = await ParseAsync(legacyBytes, SampleLayout("windows-1254"));
        Assert.NotEqual("DECYUM AMİ HÖKOT", forcedWrongOnUtf8.Records.Single(item => item.LineNumber == 6).StudentName);
        Assert.NotEqual("DECYUM AMİ HÖKOT", forcedWrongOnLegacy.Records.Single(item => item.LineNumber == 6).StudentName);
        Assert.Equal(utf8.Records.Select(item => item.StudentName), forcedRightOnLegacy.Records.Select(item => item.StudentName));
        await Assert.ThrowsAsync<ArgumentException>(() => ParseAsync(FixtureBytes(), SampleLayout("yok-boyle-kodlama")));
    }

    [Fact]
    public async Task KK4_K18_ShortLineTreatedAsTrailingSpaces_H11()
    {
        var result = await ParseAsync(FixtureBytes(), SampleLayout());
        Assert.Equal(17, result.Records.Count(item => item.IsValid));
        Assert.Empty(result.Issues);
        var shortLine = Line("123456789", "1", "A", "KISA SATIR", "ABCDE").TrimEnd();
        Assert.True(shortLine.Length < 67);
        var shortResult = await ParseTextAsync(shortLine + "\r\n" + shortLine.TrimEnd() + "\r\n");
        var record = shortResult.Records.First();
        Assert.True(record.IsValid);
        Assert.Equal("ABCDE", record.Answers.TrimEnd());
        Assert.Equal(25, record.Answers.Length);
        Assert.Equal(20, record.BlankMarkCount);
        Assert.Equal(shortResult.Records.First().Answers, shortResult.Records.Last().Answers);
    }

    [Fact]
    public async Task KK4_K15_AllZeroStudentNumberIsAnswerKey()
    {
        var result = await ParseAsync(FixtureBytes(), SampleLayout());
        var keys = result.Records.Where(item => item.IsAnswerKey).ToList();
        Assert.Equal([1, 2], keys.Select(item => item.LineNumber));
        Assert.Equal(["A", "B"], keys.Select(item => item.BookletType));
        Assert.All(keys, item => Assert.True(item.IsValid));
        Assert.Equal(15, result.Records.Count(item => !item.IsAnswerKey));
    }

    [Fact]
    public async Task KK4_K15_LegacyAnswerKeyPrefixStillWorks()
    {
        var text = "ANSWER_KEY_A|CBAAEEAEADAADDEACADCAEDBE\nANSWER_KEY_B|EBDEACDACAEDDAADAEAEEAABC\n";
        var result = await ParseTextAsync(text);
        Assert.Equal(2, result.Records.Count(item => item.IsAnswerKey && item.IsValid));
        Assert.Empty(result.Issues);
    }

    [Fact]
    public async Task KK4_H6_ExamTypeFieldIsRead()
    {
        var result = await ParseAsync(FixtureBytes(), SampleLayout());
        Assert.All(result.Records.Where(item => !item.IsAnswerKey), item => Assert.Equal("1", item.ExamTypeValue));
        Assert.All(result.Records.Where(item => item.IsAnswerKey), item => Assert.Equal(string.Empty, item.ExamTypeValue));
    }

    [Fact]
    public async Task KK4_Y3_12_BlankMarkIsBlank()
    {
        var result = await ParseTextAsync(Line("123456789", "1", "A", "BOŞ İŞARET", "AB CD") + "\r\n");
        var record = Assert.Single(result.Records);
        Assert.Equal(21, record.BlankMarkCount);
        Assert.Equal(0, record.DoubleMarkCount);
        Assert.Equal(' ', record.Answers[2]);
        var custom = await ParseTextAsync(Line("123456789", "1", "A", "BOŞ İŞARET", "AB-CD"), SampleLayout(blank: '-'));
        Assert.Equal(1, Assert.Single(custom.Records).BlankMarkCount);
    }

    [Fact]
    public async Task KK4_Y3_13_DoubleMarkIsReported()
    {
        var result = await ParseTextAsync(Line("123456789", "1", "A", "ÇİFT İŞARET", "AB*C*") + "\r\n");
        var record = Assert.Single(result.Records);
        Assert.Equal(2, record.DoubleMarkCount);
        Assert.Equal(20, record.BlankMarkCount);
        Assert.Equal('*', record.Answers[2]);
        var custom = await ParseTextAsync(Line("123456789", "1", "A", "ÇİFT İŞARET", "AB*C*"), SampleLayout(doubleMark: '#'));
        Assert.Equal(0, Assert.Single(custom.Records).DoubleMarkCount);
    }

    [Fact]
    public async Task KK4_Y3_4_NameFieldIsTrimmed()
    {
        var result = await ParseTextAsync(Line("123456789", "1", "A", "  AD SOYAD", "ABCDE") + "\r\n");
        Assert.Equal("AD SOYAD", Assert.Single(result.Records).StudentName);
    }
}
