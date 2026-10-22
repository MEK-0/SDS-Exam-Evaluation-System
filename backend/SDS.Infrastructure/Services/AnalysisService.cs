// Sınav sonuçlarını gerçek verilerden analiz eder ve Excel raporu üretir.
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SDS.Application.Analysis;
using SDS.Application.Security;
using SDS.Domain.Entities;
using SDS.Infrastructure.Persistence;

namespace SDS.Infrastructure.Services;

public sealed class AnalysisService(SdsDbContext dbContext, ICurrentUser currentUser) : IAnalysisService
{
    public async Task<AnalysisResponse?> GetAsync(Guid examId, Guid? studentId, CancellationToken cancellationToken)
    {
        var data = await LoadAsync(examId, cancellationToken);
        if (data is null) return null;
        return Build(data, studentId);
    }

    public async Task<byte[]?> ExportAsync(Guid examId, CancellationToken cancellationToken)
    {
        var data = await LoadAsync(examId, cancellationToken);
        if (data is null) return null;
        var analysis = Build(data, null);
        using var workbook = new XLWorkbook();
        var general = workbook.Worksheets.Add("Genel Bilgiler");
        general.Cell(1, 1).Value = "Sınav Başarı Analiz Raporu";
        general.Cell(3, 1).Value = "Ders"; general.Cell(3, 2).Value = $"{analysis.General.CourseCode} — {analysis.General.CourseName}";
        general.Cell(4, 1).Value = "Sınav Türü"; general.Cell(4, 2).Value = analysis.General.ExamType;
        general.Cell(5, 1).Value = "Sınav Formatı"; general.Cell(5, 2).Value = analysis.General.ExamFormat;
        general.Cell(6, 1).Value = "Soru Sayısı"; general.Cell(6, 2).Value = analysis.General.QuestionCount;
        general.Cell(7, 1).Value = "Maks. Puan"; general.Cell(7, 2).Value = analysis.General.MaximumScore;
        general.Cell(9, 1).Value = "İstatistik"; general.Cell(9, 2).Value = "Değer";
        var summaryRows = new[] { ("Toplam Öğrenci", analysis.Summary.TotalStudents.ToString()), ("Sınava Katılan", analysis.Summary.ParticipatingStudents.ToString()), ("Ortalama Puan", Format(analysis.Summary.Average)), ("Medyan", Format(analysis.Summary.Median)), ("Standart Sapma", Format(analysis.Summary.StandardDeviation)), ("Min / Maks", $"{Format(analysis.Summary.Minimum)} / {Format(analysis.Summary.Maximum)}") };
        for (var index = 0; index < summaryRows.Length; index++) { general.Cell(10 + index, 1).Value = summaryRows[index].Item1; general.Cell(10 + index, 2).Value = summaryRows[index].Item2; }
        var students = workbook.Worksheets.Add("Öğrenci Sonuçları");
        WriteHeader(students, new[] { "Öğrenci Numarası", "Ad Soyad", "Durum", "Puan" });
        for (var index = 0; index < analysis.Students.Count; index++) { var row = analysis.Students.ElementAt(index); students.Cell(index + 2, 1).SetValue(row.StudentNumber).Style.NumberFormat.Format = "@"; students.Cell(index + 2, 2).Value = row.StudentName; students.Cell(index + 2, 3).Value = row.Status; if (row.Score.HasValue) students.Cell(index + 2, 4).Value = row.Score.Value; }
        var questions = workbook.Worksheets.Add("Soru Analizi");
        WriteHeader(questions, new[] { "Soru", "Başarı Oranı", "Doğru", "Katılan" });
        for (var index = 0; index < analysis.Questions.Count; index++) { var row = analysis.Questions.ElementAt(index); questions.Cell(index + 2, 1).Value = row.QuestionNumber; questions.Cell(index + 2, 2).Value = row.SuccessRate / 100m; questions.Cell(index + 2, 2).Style.NumberFormat.Format = "0.00%"; questions.Cell(index + 2, 3).Value = row.CorrectCount; questions.Cell(index + 2, 4).Value = row.ParticipatingCount; }
        var outcomes = workbook.Worksheets.Add("Öğrenme Çıktıları");
        WriteHeader(outcomes, new[] { "#", "Öğrenme Çıktısı", "Eşleşen Sorular", "Başarı Oranı" });
        for (var index = 0; index < analysis.LearningOutcomes.Count; index++) { var row = analysis.LearningOutcomes.ElementAt(index); outcomes.Cell(index + 2, 1).Value = index + 1; outcomes.Cell(index + 2, 2).Value = row.Description; outcomes.Cell(index + 2, 3).Value = string.Join(", ", row.QuestionNumbers.Select(number => $"S{number}")); if (row.SuccessRate.HasValue) { outcomes.Cell(index + 2, 4).Value = row.SuccessRate.Value / 100m; outcomes.Cell(index + 2, 4).Style.NumberFormat.Format = "0.00%"; } }
        foreach (var sheet in workbook.Worksheets) { sheet.Columns().AdjustToContents(); sheet.SheetView.FreezeRows(1); sheet.PageSetup.PageOrientation = XLPageOrientation.Landscape; sheet.PageSetup.FitToPages(1, 0); }
        using var stream = new MemoryStream(); workbook.SaveAs(stream); return stream.ToArray();
    }

    private async Task<AnalysisData?> LoadAsync(Guid examId, CancellationToken cancellationToken)
    {
        var exam = await dbContext.Exams.AsNoTracking().Include(item => item.Course).SingleOrDefaultAsync(item => item.Id == examId, cancellationToken);
        if (exam is null || !await HasAccessAsync(exam.CourseId, cancellationToken)) return null;
        if (exam.Status != ExamStatus.Completed) throw new InvalidOperationException("Sınav tamamlanmadan analiz raporu görüntülenemez.");
        var answerKey = await dbContext.ExamAnswerKeys.AsNoTracking().Include(item => item.Questions).Include(item => item.Booklets).ThenInclude(item => item.Questions).ThenInclude(item => item.ExamQuestion).SingleOrDefaultAsync(item => item.ExamId == examId, cancellationToken);
        var imports = await dbContext.ExamImports.AsNoTracking().Include(item => item.ImportedStudents).Include(item => item.OpticalExamRecords).SingleOrDefaultAsync(item => item.ExamId == examId, cancellationToken);
        var matches = await dbContext.StudentMatches.AsNoTracking().Include(item => item.OpticalExamRecord).Where(item => item.ExamId == examId).ToListAsync(cancellationToken);
        var results = await dbContext.ExamResults.AsNoTracking().Where(item => item.ExamId == examId).ToListAsync(cancellationToken);
        var outcomes = await dbContext.QuestionLearningOutcomes.AsNoTracking().Include(item => item.LearningOutcome).Include(item => item.ExamQuestion).Where(item => item.ExamId == examId).ToListAsync(cancellationToken);
        if (answerKey is null || imports is null) throw new InvalidOperationException("Sınav analiz verileri henüz tamamlanmamış.");
        return new AnalysisData(exam, answerKey, imports.ImportedStudents.ToList(), imports.OpticalExamRecords.Where(item => item.IsValid && !item.IsAnswerKey).ToList(), matches, results, outcomes);
    }

    private async Task<bool> HasAccessAsync(Guid courseId, CancellationToken cancellationToken)
    {
        var user = currentUser.Get();
        return user.IsAdministrator || user.Id.HasValue && await dbContext.CourseInstructors.AnyAsync(item => item.CourseId == courseId && item.UserId == user.Id.Value, cancellationToken);
    }

    private static AnalysisResponse Build(AnalysisData data, Guid? selectedStudentId)
    {
        var students = data.Students.ToDictionary(item => item.Id);
        var results = data.Results.ToDictionary(item => item.ImportedStudentId);
        var matches = data.Matches.ToDictionary(item => item.ImportedStudentId);
        var graded = data.Results.Where(item => item.Status == GradeStatus.Graded && item.AdjustedScore.HasValue).ToList();
        var scores = graded.Select(item => item.AdjustedScore!.Value).OrderBy(item => item).ToList();
        decimal? average = scores.Count == 0 ? null : scores.Average();
        decimal? median = scores.Count == 0 ? null : scores.Count % 2 == 1 ? scores[scores.Count / 2] : (scores[scores.Count / 2 - 1] + scores[scores.Count / 2]) / 2m;
        var standardDeviation = scores.Count == 0 ? null : (decimal?)Math.Sqrt(scores.Sum(score => Math.Pow((double)(score - average!.Value), 2)) / scores.Count);
        var studentRows = data.Students.Select(student => { results.TryGetValue(student.Id, out var result); var status = result?.Status == GradeStatus.Graded ? "Değerlendirildi" : result?.Status == GradeStatus.Absent ? "Sınava Girmedi" : "Not Girilmedi"; return new AnalysisStudentResponse(student.Id, student.StudentNumber, $"{student.FirstName} {student.LastName}".Trim(), status, result?.AdjustedScore ?? result?.RawScore); }).OrderBy(item => item.StudentNumber, StringComparer.Ordinal).ToList();
        var summary = new AnalysisSummaryResponse(data.Students.Count, graded.Count, average, median, standardDeviation, scores.Count == 0 ? null : scores.Min(), scores.Count == 0 ? null : scores.Max(), null, null, "Başarı eşiği tanımlı değil.");
        var questionRows = BuildQuestions(data, graded, matches);
        var choices = BuildChoices(data, graded, matches);
        var outcomeRows = data.Outcomes.GroupBy(item => item.LearningOutcomeId).Select(group => { var numbers = group.Select(item => item.ExamQuestion.CanonicalQuestionNumber).Distinct().OrderBy(item => item).ToList(); var rates = questionRows.Where(item => numbers.Contains(item.QuestionNumber) && item.ParticipatingCount > 0).Select(item => item.SuccessRate).ToList(); return new LearningOutcomeAnalysisResponse(group.Key, group.First().LearningOutcome.Description, numbers, rates.Count == 0 ? null : rates.Average()); }).OrderBy(item => item.Description, StringComparer.CurrentCulture).ToList();
        var selected = selectedStudentId.HasValue && students.ContainsKey(selectedStudentId.Value) ? BuildStudentDetail(data, students[selectedStudentId.Value], results.GetValueOrDefault(selectedStudentId.Value), matches.GetValueOrDefault(selectedStudentId.Value)) : null;
        return new AnalysisResponse(summary, new AnalysisGeneralResponse(data.Exam.Course.Code, data.Exam.Course.Name, ExamTypeText(data.Exam.ExamType), data.Exam.EvaluationType == EvaluationType.MultipleChoice ? "Çoktan Seçmeli" : "Açık Uçlu", data.AnswerKey.QuestionCount, data.AnswerKey.MaximumScore), studentRows, selected, BuildDistribution(scores), questionRows, choices, outcomeRows);
    }

    private static IReadOnlyCollection<QuestionAnalysisResponse> BuildQuestions(AnalysisData data, IReadOnlyCollection<ExamResult> graded, IReadOnlyDictionary<Guid, StudentMatch> matches)
    {
        var rows = new List<QuestionAnalysisResponse>();
        foreach (var logical in data.AnswerKey.Questions.Where(item => item.Status == ExamQuestionStatus.Active).OrderBy(item => item.CanonicalQuestionNumber))
        {
            var booklets = data.AnswerKey.Booklets.SelectMany(item => item.Questions.Where(question => question.ExamQuestionId == logical.Id).Select(question => (item.BookletCode, question))).ToList();
            var correct = 0; var participating = 0;
            foreach (var result in graded) if (matches.TryGetValue(result.ImportedStudentId, out var match) && match.OpticalExamRecord is not null && booklets.FirstOrDefault(item => item.BookletCode.Equals(match.OpticalExamRecord.BookletType, StringComparison.OrdinalIgnoreCase)).question is { } question) { participating++; var answer = AnswerAt(match.OpticalExamRecord.Answers, question.DisplayQuestionNumber); if (IsCorrect(answer, question.AcceptedAnswers)) correct++; }
            rows.Add(new QuestionAnalysisResponse(logical.CanonicalQuestionNumber, participating == 0 ? 0 : decimal.Round(correct * 100m / participating, 2), correct, participating));
        }
        return rows;
    }

    private static IReadOnlyCollection<ChoiceDistributionResponse> BuildChoices(AnalysisData data, IReadOnlyCollection<ExamResult> graded, IReadOnlyDictionary<Guid, StudentMatch> matches)
    {
        var output = new List<ChoiceDistributionResponse>();
        foreach (var booklet in data.AnswerKey.Booklets)
            foreach (var question in booklet.Questions.OrderBy(item => item.DisplayQuestionNumber))
            {
                var selected = graded.Where(result => matches.TryGetValue(result.ImportedStudentId, out var match) && match.OpticalExamRecord is not null && match.OpticalExamRecord.BookletType.Equals(booklet.BookletCode, StringComparison.OrdinalIgnoreCase)).Select(result => AnswerAt(matches[result.ImportedStudentId].OpticalExamRecord!.Answers, question.DisplayQuestionNumber)).ToList();
                var choices = new[] { "A", "B", "C", "D", "E" }.Select(option => new ChoiceResponse(option, selected.Count(item => item == option), selected.Count == 0 ? 0 : decimal.Round(selected.Count(item => item == option) * 100m / selected.Count, 2), question.AcceptedAnswers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains(option, StringComparer.OrdinalIgnoreCase))).ToList();
                output.Add(new ChoiceDistributionResponse(question.ExamQuestion.CanonicalQuestionNumber, booklet.BookletCode, choices, selected.Count(string.IsNullOrWhiteSpace), selected.Count));
            }
        return output;
    }

    private static AnalysisStudentDetailResponse BuildStudentDetail(AnalysisData data, ImportedStudent student, ExamResult? result, StudentMatch? match)
    {
        if (data.Exam.EvaluationType == EvaluationType.OpenEnded)
        {
            var openEndedStatus = result?.Status == GradeStatus.Graded ? "Değerlendirildi" : result?.Status == GradeStatus.Absent ? "Sınava Girmedi" : "Not Girilmedi";
            return new AnalysisStudentDetailResponse(student.Id, student.StudentNumber, $"{student.FirstName} {student.LastName}".Trim(), openEndedStatus, null, result?.RawScore, result?.AdjustedScore, 0, 0, 0, []);
        }
        if (result?.Status != GradeStatus.Graded || match?.OpticalExamRecord is null) return new AnalysisStudentDetailResponse(student.Id, student.StudentNumber, $"{student.FirstName} {student.LastName}".Trim(), "Sınava Girmedi", null, null, null, 0, 0, 0, []);
        var booklet = data.AnswerKey.Booklets.FirstOrDefault(item => item.BookletCode.Equals(match.OpticalExamRecord.BookletType, StringComparison.OrdinalIgnoreCase));
        var questions = booklet?.Questions.OrderBy(item => item.DisplayQuestionNumber).Select(item => { var answer = AnswerAt(match.OpticalExamRecord!.Answers, item.DisplayQuestionNumber); return new StudentQuestionResponse(item.ExamQuestion.CanonicalQuestionNumber, string.IsNullOrWhiteSpace(answer) ? "Boş" : answer, item.AcceptedAnswers, IsCorrect(answer, item.AcceptedAnswers), string.IsNullOrWhiteSpace(answer)); }).ToList() ?? [];
        return new AnalysisStudentDetailResponse(student.Id, student.StudentNumber, $"{student.FirstName} {student.LastName}".Trim(), "Değerlendirildi", booklet?.BookletCode, result.RawScore, result.AdjustedScore, questions.Count(item => item.IsCorrect), questions.Count(item => !item.IsCorrect && !item.IsBlank), questions.Count(item => item.IsBlank), questions);
    }

    private static IReadOnlyCollection<ScoreDistributionResponse> BuildDistribution(IReadOnlyCollection<decimal> scores) => Enumerable.Range(0, 10).Select(index => { var lower = index * 10; var upper = lower + 10; return new ScoreDistributionResponse($"{lower}–{upper}", scores.Count(score => index == 9 ? score >= lower && score <= upper : score >= lower && score < upper)); }).ToList();
    private static string AnswerAt(string answers, int number) => number > 0 && number <= answers.Length ? answers[number - 1].ToString().Trim().ToUpperInvariant() : string.Empty;
    private static bool IsCorrect(string answer, string accepted) => !string.IsNullOrWhiteSpace(answer) && accepted.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains(answer, StringComparer.OrdinalIgnoreCase);
    private static string ExamTypeText(ExamType type) => type switch { ExamType.Midterm => "Vize", ExamType.Final => "Final", ExamType.MakeUp => "Bütünleme", _ => type.ToString() };
    private static string Format(decimal? value) => value?.ToString("0.##") ?? "—";
    private static void WriteHeader(IXLWorksheet sheet, IReadOnlyCollection<string> headers) { for (var index = 0; index < headers.Count; index++) sheet.Cell(1, index + 1).Value = headers.ElementAt(index); sheet.Row(1).Style.Font.Bold = true; }

    private sealed record AnalysisData(Exam Exam, ExamAnswerKey AnswerKey, IReadOnlyCollection<ImportedStudent> Students, IReadOnlyCollection<OpticalExamRecord> OpticalRecords, IReadOnlyCollection<StudentMatch> Matches, IReadOnlyCollection<ExamResult> Results, IReadOnlyCollection<QuestionLearningOutcome> Outcomes);
}
