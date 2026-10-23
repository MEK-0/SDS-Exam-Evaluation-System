// Dersin vize, final ve bütünleme sonuçlarını dönem raporuna dönüştürür.
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SDS.Application.Grades;
using SDS.Application.Security;
using SDS.Application.TermAnalysis;
using SDS.Domain.Entities;
using SDS.Infrastructure.Persistence;

namespace SDS.Infrastructure.Services;

public sealed class TermAnalysisService(SdsDbContext dbContext, ICurrentUser currentUser) : ITermAnalysisService
{
    public async Task<TermAnalysisResponse?> GetAsync(Guid courseId, CancellationToken cancellationToken)
    {
        var data = await LoadAsync(courseId, cancellationToken);
        return data is null ? null : Build(data);
    }

    public async Task<byte[]?> ExportAsync(Guid courseId, CancellationToken cancellationToken)
    {
        var response = await GetAsync(courseId, cancellationToken);
        if (response is null) return null;
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Dönem Değerlendirme");
        sheet.Cell(1, 1).Value = "Dönem Değerlendirme";
        var headers = new[] { "Öğrenci No", "Ad Soyad", "Vize", "Final", "Büt.", "Eff. Sınav", "Ağ. Ort.", "Harf" };
        for (var column = 0; column < headers.Length; column++) sheet.Cell(2, column + 1).Value = headers[column];
        var row = 3;
        foreach (var student in response.Students)
        {
            sheet.Cell(row, 1).SetValue(student.StudentNumber).Style.NumberFormat.Format = "@";
            sheet.Cell(row, 2).Value = student.StudentName; sheet.Cell(row, 3).Value = student.MidtermScore; sheet.Cell(row, 4).Value = student.FinalScore; sheet.Cell(row, 5).Value = student.MakeUpScore; sheet.Cell(row, 6).Value = student.EffectiveExamScore; sheet.Cell(row, 7).Value = student.WeightedScore; sheet.Cell(row, 8).Value = student.LetterGrade ?? "-"; row++;
        }
        sheet.Columns().AdjustToContents(); sheet.SheetView.FreezeRows(2); sheet.Range(1, 1, 2, headers.Length).Style.Font.Bold = true;
        using var stream = new MemoryStream(); workbook.SaveAs(stream); return stream.ToArray();
    }

    private async Task<TermData?> LoadAsync(Guid courseId, CancellationToken cancellationToken)
    {
        var user = currentUser.Get(); var canAccess = user.IsAdministrator || user.Id.HasValue && await dbContext.CourseInstructors.AnyAsync(item => item.CourseId == courseId && item.UserId == user.Id.Value, cancellationToken); if (!canAccess) return null;
        var course = await dbContext.Courses.AsNoTracking().SingleOrDefaultAsync(item => item.Id == courseId, cancellationToken); if (course is null) return null;
        var exams = await dbContext.Exams.AsNoTracking().Where(item => item.CourseId == courseId).OrderByDescending(item => item.CreatedAt).ToListAsync(cancellationToken); var selected = new Dictionary<ExamType, Exam>(); foreach (var exam in exams.Where(item => !selected.ContainsKey(item.ExamType))) selected[exam.ExamType] = exam;
        var records = new Dictionary<ExamType, IReadOnlyCollection<TermRecord>>(); foreach (var pair in selected) { var students = await dbContext.ExamImports.AsNoTracking().Where(item => item.ExamId == pair.Value.Id).SelectMany(item => item.ImportedStudents).ToListAsync(cancellationToken); var results = await dbContext.ExamResults.AsNoTracking().Where(item => item.ExamId == pair.Value.Id).ToListAsync(cancellationToken); records[pair.Key] = students.Select(student => { var result = results.SingleOrDefault(item => item.ImportedStudentId == student.Id); var score = result?.Status == GradeStatus.Graded ? result.AdjustedScore ?? result.RawScore : null; return new TermRecord(student.StudentNumber, $"{student.FirstName} {student.LastName}".Trim(), score); }).ToList(); }
        var policy = await dbContext.GradingPolicies.AsNoTracking().OrderByDescending(item => item.UpdatedAt).FirstOrDefaultAsync(cancellationToken); return new TermData(records, policy?.MidtermWeight ?? 40, policy?.FinalWeight ?? 60);
    }

    private static TermAnalysisResponse Build(TermData data)
    {
        var students = data.Records.Values.SelectMany(item => item).GroupBy(item => item.Number, StringComparer.Ordinal).Select(group => { var first = group.First(); var midterm = GetScore(data.Records, ExamType.Midterm, group.Key); var final = GetScore(data.Records, ExamType.Final, group.Key); var makeUp = GetScore(data.Records, ExamType.MakeUp, group.Key); var calculated = TermAnalysisCalculator.Calculate(midterm, final, makeUp, data.MidtermWeight, data.FinalWeight); return new TermStudentResponse(group.Key, first.Name, midterm, final, makeUp, calculated.Effective, calculated.Weighted, GradeLetterCalculator.Calculate(calculated.Weighted), calculated.Weighted.HasValue ? "Değerlendirildi" : "Eksik veri"); }).OrderBy(item => item.StudentNumber, StringComparer.Ordinal).ToList();
        var valid = students.Where(item => item.WeightedScore.HasValue).ToList(); var summary = new TermAnalysisSummary(students.Count, valid.Count, valid.Count == 0 ? null : valid.Average(item => item.WeightedScore!.Value), valid.Count == 0 ? null : valid.Count(item => item.WeightedScore >= 50) * 100m / valid.Count, Average(students.Select(item => item.MidtermScore)), Average(students.Select(item => item.FinalScore)), Average(students.Select(item => item.MakeUpScore)), valid.Any(item => item.MakeUpScore.HasValue) ? "Bütünleme" : "Final", data.MidtermWeight, data.FinalWeight); var letters = students.Where(item => item.LetterGrade is not null).GroupBy(item => item.LetterGrade!).Select(item => new TermLetterResponse(item.Key, item.Count())).OrderBy(item => item.Letter).ToList(); return new TermAnalysisResponse(summary, students, letters, BuildDistribution(valid.Select(item => item.WeightedScore!.Value)));
    }

    private static decimal? GetScore(Dictionary<ExamType, IReadOnlyCollection<TermRecord>> records, ExamType type, string number) => records.TryGetValue(type, out var rows) ? rows.FirstOrDefault(item => item.Number == number)?.Score : null;
    private static decimal? Average(IEnumerable<decimal?> values) { var valid = values.Where(item => item.HasValue).Select(item => item!.Value).ToList(); return valid.Count == 0 ? null : decimal.Round(valid.Average(), 2); }
    private static IReadOnlyCollection<TermScoreDistributionResponse> BuildDistribution(IEnumerable<decimal> scores) { var values = scores.ToList(); var labels = new[] { "0–10", "10–20", "20–30", "30–40", "40–50", "50–60", "60–70", "70–80", "80–90", "90–100" }; return labels.Select((label, index) => new TermScoreDistributionResponse(label, values.Count(score => index == 9 ? score >= index * 10 && score <= 100 : score >= index * 10 && score < (index + 1) * 10))).ToList(); }
    private sealed record TermData(Dictionary<ExamType, IReadOnlyCollection<TermRecord>> Records, decimal MidtermWeight, decimal FinalWeight);
    private sealed record TermRecord(string Number, string Name, decimal? Score);
}
