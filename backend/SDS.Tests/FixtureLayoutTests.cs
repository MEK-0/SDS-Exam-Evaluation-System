// Anonim optik TXT ve sınıf listesi XLSX fixture'larının gerçek dosya düzenini koruduğunu doğrular.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ClosedXML.Excel;
using Xunit;

namespace SDS.Tests;

public sealed class FixtureLayoutTests
{
    private const string TurkishLetters = "İŞĞÜÖÇışğüöç";

    private static readonly int[] ExpectedCharLengths = [142, 141, 142, 142, 142, 142, 142, 142, 142, 142, 142, 142, 142, 142, 142, 142, 67];
    private static readonly int[] ExpectedByteLengths = [142, 141, 142, 142, 142, 144, 143, 143, 144, 142, 142, 142, 143, 144, 144, 143, 70];

    private static readonly Dictionary<int, int[]> ExpectedNonAsciiPositions = new()
    {
        [6] = [30, 33],
        [7] = [22],
        [8] = [24],
        [9] = [27, 28],
        [13] = [33],
        [14] = [21, 25],
        [15] = [23, 30],
        [16] = [23],
        [17] = [24, 33, 36]
    };

    private static string FixturePath(string name)
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
    }

    private static byte[] ReadOpticalBytes()
    {
        return File.ReadAllBytes(FixturePath("optik-ornek-anonim.txt"));
    }

    private static List<byte[]> SplitBytesByCrlf(byte[] bytes)
    {
        var lines = new List<byte[]>();
        var start = 0;
        for (var index = 0; index < bytes.Length - 1; index++)
        {
            if (bytes[index] == 0x0D && bytes[index + 1] == 0x0A)
            {
                lines.Add(bytes[start..index]);
                start = index + 2;
                index++;
            }
        }

        lines.Add(bytes[start..]);
        return lines;
    }

    private static List<string> ReadOpticalLines()
    {
        var strictUtf8 = new UTF8Encoding(false, true);
        return SplitBytesByCrlf(ReadOpticalBytes())
            .Select(line => strictUtf8.GetString(line))
            .ToList();
    }

    private sealed record OpticalStudent(int LineNumber, string Number, string Name);

    private sealed record ListStudent(string Number, string Name);

    private static List<OpticalStudent> ReadOpticalStudents()
    {
        var lines = ReadOpticalLines();
        return lines
            .Select((line, index) => new OpticalStudent(index + 1, line[..9], line[21..41].Trim()))
            .Where(student => student.LineNumber >= 3)
            .ToList();
    }

    private static List<ListStudent> ReadListStudents()
    {
        using var workbook = new XLWorkbook(FixturePath("sinif-listesi-ornek-anonim.xlsx"));
        var sheet = workbook.Worksheet("Sayfa1");
        var students = new List<ListStudent>();
        for (var row = 2; row <= 18; row++)
        {
            var number = ((long)sheet.Cell(row, 1).GetDouble()).ToString("D9");
            var name = sheet.Cell(row, 2).GetString().Trim();
            students.Add(new ListStudent(number, name));
        }

        return students;
    }

    private static int DigitDifference(string left, string right)
    {
        return left.Zip(right, (a, b) => a == b ? 0 : 1).Sum();
    }

    [Fact]
    public void T0a_00_OpticalFixtureLineAndByteLengthsMatchOriginalLayout()
    {
        var byteLines = SplitBytesByCrlf(ReadOpticalBytes());
        var lines = ReadOpticalLines();

        Assert.Equal(17, lines.Count);
        Assert.Equal(ExpectedCharLengths, lines.Select(line => line.Length).ToArray());
        Assert.Equal(ExpectedByteLengths, byteLines.Select(line => line.Length).ToArray());
        Assert.All(lines, line => Assert.Equal(67, line.TrimEnd(' ').Length));
    }

    [Fact]
    public void T0a_00_OpticalFixtureHasCrlfAndNoTrailingNewline()
    {
        var bytes = ReadOpticalBytes();
        var crlfCount = 0;
        var loneLfCount = 0;
        var loneCrCount = 0;
        for (var index = 0; index < bytes.Length; index++)
        {
            var previousIsCr = index > 0 && bytes[index - 1] == 0x0D;
            var nextIsLf = index + 1 < bytes.Length && bytes[index + 1] == 0x0A;
            if (bytes[index] == 0x0A && previousIsCr)
            {
                crlfCount++;
            }
            else if (bytes[index] == 0x0A)
            {
                loneLfCount++;
            }
            else if (bytes[index] == 0x0D && !nextIsLf)
            {
                loneCrCount++;
            }
        }

        Assert.Equal(16, crlfCount);
        Assert.Equal(0, loneLfCount);
        Assert.Equal(0, loneCrCount);
        Assert.NotEqual(0x0A, bytes[^1]);
        Assert.NotEqual(0x0D, bytes[^1]);
    }

    [Fact]
    public void T0a_00_OpticalFixtureHasNoBom()
    {
        var bytes = ReadOpticalBytes();

        Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        var text = new UTF8Encoding(false, true).GetString(bytes);
        Assert.NotEqual('﻿', text[0]);
    }

    [Fact]
    public void T0a_00_FirstTwoRowsAreAllZeroAnswerKeys_K15()
    {
        var lines = ReadOpticalLines();

        Assert.Equal("000000000", lines[0][..9]);
        Assert.Equal("000000000", lines[1][..9]);
        Assert.Equal(' ', lines[0][18]);
        Assert.Equal(' ', lines[1][18]);
        Assert.Equal('A', lines[0][20]);
        Assert.Equal('B', lines[1][20]);
        Assert.All(lines.Skip(2), line => Assert.Equal('1', line[18]));
        var booklets = string.Concat(lines.Skip(2).Select(line => line[20]));
        Assert.Equal("AAABBABBBABABAB", booklets);
    }

    [Fact]
    public void T0a_00_InnerSpaceNumberPatternPreserved_K14()
    {
        var lines = ReadOpticalLines();
        var innerSpaceNumber = lines[10][..9];

        Assert.Equal(' ', innerSpaceNumber[7]);
        Assert.Equal(8, innerSpaceNumber.Count(char.IsAsciiDigit));
        foreach (var index in Enumerable.Range(2, 15).Where(index => index != 10))
        {
            var number = lines[index][..9];
            Assert.Equal(9, number.Length);
            Assert.All(number, character => Assert.True(char.IsAsciiDigit(character)));
        }
    }

    [Fact]
    public void T0a_00_TurkishCharacterPositionsPreserved()
    {
        var lines = ReadOpticalLines();

        for (var lineNumber = 1; lineNumber <= lines.Count; lineNumber++)
        {
            var line = lines[lineNumber - 1];
            var actual = Enumerable.Range(0, line.Length)
                .Where(index => line[index] > 127)
                .ToArray();
            var expected = ExpectedNonAsciiPositions.TryGetValue(lineNumber, out var positions) ? positions : [];
            Assert.Equal(expected, actual);
            Assert.All(actual, index => Assert.Contains(line[index], TurkishLetters));
        }
    }

    [Fact]
    public void T0a_00_ClassListFixtureHasNumericStudentNumbers()
    {
        using var workbook = new XLWorkbook(FixturePath("sinif-listesi-ornek-anonim.xlsx"));
        var sheet = workbook.Worksheet("Sayfa1");

        for (var row = 2; row <= 18; row++)
        {
            Assert.Equal(XLDataType.Number, sheet.Cell(row, 1).DataType);
            Assert.Equal(XLDataType.Text, sheet.Cell(row, 2).DataType);
        }
    }

    [Fact]
    public void T0a_00_ClassListHeadersAndRowCount()
    {
        using var workbook = new XLWorkbook(FixturePath("sinif-listesi-ornek-anonim.xlsx"));

        Assert.Single(workbook.Worksheets);
        var sheet = workbook.Worksheet("Sayfa1");
        Assert.Equal("OGRENCINO", sheet.Cell(1, 1).GetString());
        Assert.Equal("ADSOYAD", sheet.Cell(1, 2).GetString());
        Assert.Equal("SINAVNOTU", sheet.Cell(1, 3).GetString());
        Assert.Equal(18, sheet.LastRowUsed()!.RowNumber());
        for (var row = 2; row <= 18; row++)
        {
            Assert.False(sheet.Cell(row, 1).IsEmpty());
            Assert.False(sheet.Cell(row, 2).IsEmpty());
            Assert.True(sheet.Cell(row, 3).IsEmpty());
        }
    }

    [Fact]
    public void T0a_00_ThirteenExactMatchesHaveIdenticalNames()
    {
        var optical = ReadOpticalStudents();
        var list = ReadListStudents();

        var pairs = optical
            .Where(student => list.Any(item => item.Number == student.Number))
            .Select(student => (student, item: list.Single(entry => entry.Number == student.Number)))
            .ToList();

        Assert.Equal(13, pairs.Count);
        Assert.All(pairs, pair => Assert.Equal(pair.item.Name, pair.student.Name));
    }

    [Fact]
    public void T0a_00_ListHasFourStudentsNotExactlyInOptical_AndKeysAreNotInList()
    {
        var optical = ReadOpticalStudents();
        var list = ReadListStudents();
        var opticalNumbers = optical.Select(student => student.Number).ToHashSet();

        Assert.Equal(15, optical.Count);
        Assert.Equal(4, list.Count(item => !opticalNumbers.Contains(item.Number)));
        Assert.DoesNotContain(list, item => item.Number == "000000000");
    }

    [Fact]
    public void T0a_00_InnerSpaceStudentHasFullNumberInListWithSameName()
    {
        var optical = ReadOpticalStudents();
        var list = ReadListStudents();
        var innerSpace = Assert.Single(optical, student => student.Number.Contains(' '));
        var opticalNumbers = optical.Select(student => student.Number).ToHashSet();
        var matchingPositions = Enumerable.Range(0, 9).Where(index => index != 7).ToArray();

        Assert.Equal(11, innerSpace.LineNumber);
        var candidates = list
            .Where(item => !opticalNumbers.Contains(item.Number))
            .Where(item => item.Name == innerSpace.Name)
            .Where(item => matchingPositions.All(index => item.Number[index] == innerSpace.Number[index]))
            .ToList();
        Assert.Single(candidates);
    }

    [Fact]
    public void T0a_00_OneDigitDifferenceWithSameNameRelationPreserved()
    {
        var optical = ReadOpticalStudents();
        var list = ReadListStudents();
        var listNumbers = list.Select(item => item.Number).ToHashSet();
        var opticalNumbers = optical.Select(student => student.Number).ToHashSet();

        var pairs = optical
            .Where(student => !student.Number.Contains(' ') && !listNumbers.Contains(student.Number))
            .SelectMany(student => list
                .Where(item => !opticalNumbers.Contains(item.Number))
                .Where(item => DigitDifference(item.Number, student.Number) == 1)
                .Where(item => item.Name == student.Name)
                .Select(item => (student, item)))
            .ToList();

        Assert.Single(pairs);
    }

    [Fact]
    public void T0a_00_AyseBetulTrapPreserved_OneDigitDifferenceWithDifferentName()
    {
        var optical = ReadOpticalStudents();
        var list = ReadListStudents();
        var listNumbers = list.Select(item => item.Number).ToHashSet();
        var opticalNumbers = optical.Select(student => student.Number).ToHashSet();
        var exactStudents = optical.Where(student => listNumbers.Contains(student.Number)).ToList();

        var traps = list
            .Where(item => !opticalNumbers.Contains(item.Number))
            .Where(item => exactStudents.Any(student =>
                DigitDifference(item.Number, student.Number) == 1 && item.Name != student.Name))
            .ToList();

        Assert.NotEmpty(traps);
    }

    [Fact]
    public void T0a_00_TwoAbsentStudentsAreOneDigitTrapsWithDifferentNames()
    {
        var optical = ReadOpticalStudents();
        var list = ReadListStudents();
        var listNumbers = list.Select(item => item.Number).ToHashSet();
        var opticalNumbers = optical.Select(student => student.Number).ToHashSet();
        var exactStudents = optical.Where(student => listNumbers.Contains(student.Number)).ToList();

        var traps = list
            .Where(item => !opticalNumbers.Contains(item.Number))
            .Where(item => exactStudents.Any(student =>
                DigitDifference(item.Number, student.Number) == 1 && item.Name != student.Name))
            .Where(item => optical.All(student => student.Name != item.Name))
            .ToList();

        Assert.Equal(2, traps.Count);
    }
}
