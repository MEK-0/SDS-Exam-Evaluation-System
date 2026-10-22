// Onaylı öğrenci eşleşmelerinden puan üretir ve not düzenlemelerini yönetir.
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SDS.Application.Grades;
using SDS.Application.Security;
using SDS.Domain.Entities;
using SDS.Infrastructure.Persistence;

namespace SDS.Infrastructure.Services;

public sealed class GradeService(SdsDbContext dbContext, ICurrentUser currentUser) : IGradeService
{
    public async Task<GradesResponse?> GetAsync(Guid examId, CancellationToken cancellationToken)
    {
        var data = await LoadAsync(examId, cancellationToken);
        return data is null ? null : ToResponse(data.Results, data.Students, data.ProjectGrades, data.CalculatedAt);
    }

    public async Task<GradesResponse?> CalculateAsync(Guid examId, CancellationToken cancellationToken)
    {
        var data = await LoadCalculationDataAsync(examId, cancellationToken);
        if (data is null) return null;
        EnsureReady(data.Exam, data.AnswerKey);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var existing = await dbContext.ExamResults.Where(item => item.ExamId == examId).ToDictionaryAsync(item => item.ImportedStudentId, cancellationToken);
        var matches = data.Matches.ToDictionary(item => item.ImportedStudentId);
        var booklets = data.AnswerKey!.Booklets.ToDictionary(item => item.BookletCode, StringComparer.OrdinalIgnoreCase);
        foreach (var student in data.Students)
        {
            matches.TryGetValue(student.Id, out var match);
            existing.TryGetValue(student.Id, out var result);
            if (result is null)
            {
                result = new ExamResult { Id = Guid.NewGuid(), ExamId = examId, ImportedStudentId = student.Id, CalculatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
                dbContext.ExamResults.Add(result);
            }
            if (match is null || match.Status == StudentMatchStatus.Absent || match.OpticalExamRecord is null)
            {
                result.RawScore = null;
                result.AdjustedScore = null;
                result.Status = GradeStatus.Absent;
            }
            else
            {
                if (!booklets.TryGetValue(match.OpticalExamRecord.BookletType.Trim(), out var booklet)) throw new InvalidOperationException($@"{student.StudentNumber} öğrencisinin kitapçığı için cevap anahtarı bulunamadı.");
                var rawScore = CalculateScore(match.OpticalExamRecord.Answers, booklet);
                result.RawScore = rawScore;
                result.AdjustedScore ??= rawScore;
                result.Status = GradeStatus.Graded;
            }
            result.CalculatedAt = DateTime.UtcNow;
            result.UpdatedAt = DateTime.UtcNow;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(examId, cancellationToken);
    }

    public async Task<GradesResponse?> UpdateAsync(Guid examId, Guid importedStudentId, UpdateGradeRequest request, CancellationToken cancellationToken)
    {
        ValidateScore(request.AdjustedScore);
        var exam = await GetAccessibleExamAsync(examId, cancellationToken);
        if (exam is null) return null;
        var result = await dbContext.ExamResults.Include(item => item.Exam).SingleOrDefaultAsync(item => item.ExamId == examId && item.ImportedStudentId == importedStudentId, cancellationToken);
        if (result is null) throw new KeyNotFoundException("Öğrenci notu bulunamadı.");
        if (result.Status == GradeStatus.Absent) throw new InvalidOperationException("Sınava girmeyen öğrencinin notu düzenlenemez.");
        var previous = result.AdjustedScore ?? result.RawScore ?? 0;
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        result.AdjustedScore = decimal.Round(request.AdjustedScore, 2, MidpointRounding.AwayFromZero);
        result.UpdatedAt = DateTime.UtcNow;
        dbContext.GradeAdjustments.Add(new GradeAdjustment { Id = Guid.NewGuid(), ExamResultId = result.Id, UserId = GetUserId(), Type = GradeAdjustmentType.ManualOverride, Amount = result.AdjustedScore.Value - previous, PreviousScore = previous, NewScore = result.AdjustedScore.Value, CreatedAt = DateTime.UtcNow });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(examId, cancellationToken);
    }

    public async Task<GradesResponse?> AddBonusAsync(Guid examId, BonusRequest request, CancellationToken cancellationToken)
    {
        if (request.Amount <= 0) throw new ArgumentException("Bonus puanı sıfırdan büyük olmalıdır.");
        if (request.Amount > 100) throw new ArgumentException("Bonus puanı 100 değerini aşamaz.");
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey)) throw new ArgumentException("Bonus işlemi için işlem anahtarı gereklidir.");
        var data = await LoadAsync(examId, cancellationToken);
        if (data is null) return null;
        var resultIds = data.Results.Where(item => request.ImportedStudentIds.Contains(item.ImportedStudentId) && item.Status == GradeStatus.Graded).Select(item => item.Id).ToList();
        if (resultIds.Count == 0) throw new ArgumentException("Bonus uygulanabilecek notlandırılmış öğrenci bulunamadı.");
        var operationPrefix = $"{request.IdempotencyKey}:";
        if (await dbContext.GradeAdjustments.AnyAsync(item => item.IdempotencyKey == request.IdempotencyKey || item.IdempotencyKey != null && item.IdempotencyKey.StartsWith(operationPrefix), cancellationToken)) return await GetAsync(examId, cancellationToken);
        var results = await dbContext.ExamResults.Where(item => resultIds.Contains(item.Id)).ToListAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        foreach (var result in results)
        {
            var previous = result.AdjustedScore ?? result.RawScore ?? 0;
            var next = Math.Min(100, previous + request.Amount);
            result.AdjustedScore = decimal.Round(next, 2, MidpointRounding.AwayFromZero);
            result.UpdatedAt = DateTime.UtcNow;
            dbContext.GradeAdjustments.Add(new GradeAdjustment { Id = Guid.NewGuid(), ExamResultId = result.Id, UserId = GetUserId(), Type = GradeAdjustmentType.Bonus, Amount = result.AdjustedScore.Value - previous, PreviousScore = previous, NewScore = result.AdjustedScore.Value, IdempotencyKey = $"{operationPrefix}{result.Id}", CreatedAt = DateTime.UtcNow });
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(examId, cancellationToken);
    }

    public async Task<ProjectGradeImportResponse?> ImportProjectGradesAsync(Guid examId, Stream content, string fileName, CancellationToken cancellationToken)
    {
        var data = await LoadAsync(examId, cancellationToken);
        if (data is null) return null;
        var rows = ParseProjectGrades(content, fileName);
        var students = data.Students.ToDictionary(item => item.StudentNumber, StringComparer.Ordinal);
        var issues = new List<string>();
        var validRows = new Dictionary<string, (decimal Score, int Row)>();
        foreach (var row in rows)
        {
            if (validRows.ContainsKey(row.Number)) { issues.Add($@"{row.RowNumber}. satır: Öğrenci numarası tekrar ediyor."); continue; }
            if (!students.ContainsKey(row.Number)) { issues.Add($@"{row.RowNumber}. satır: Öğrenci sınıf listesinde bulunamadı."); continue; }
            if (row.Score is null || row.Score < 0 || row.Score > 100) { issues.Add($@"{row.RowNumber}. satır: Proje notu 0 ile 100 arasında olmalıdır."); continue; }
            validRows[row.Number] = (row.Score.Value, row.RowNumber);
        }
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        foreach (var row in validRows)
        {
            var student = students[row.Key];
            var existing = await dbContext.ProjectGradeImports.SingleOrDefaultAsync(item => item.ExamId == examId && item.ImportedStudentId == student.Id, cancellationToken);
            if (existing is null) dbContext.ProjectGradeImports.Add(new ProjectGradeImport { Id = Guid.NewGuid(), ExamId = examId, ImportedStudentId = student.Id, ProjectScore = row.Value.Score, SourceFileName = Path.GetFileName(fileName), RowNumber = row.Value.Row, ImportedAt = DateTime.UtcNow });
            else { existing.ProjectScore = row.Value.Score; existing.SourceFileName = Path.GetFileName(fileName); existing.RowNumber = row.Value.Row; existing.ImportedAt = DateTime.UtcNow; }
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var refreshed = await LoadAsync(examId, cancellationToken);
        return refreshed is null ? null : new ProjectGradeImportResponse(ToResponse(refreshed.Results, refreshed.Students, refreshed.ProjectGrades, refreshed.CalculatedAt), validRows.Count, issues);
    }

    public async Task<GradeSummaryResponse?> GetSummaryAsync(Guid examId, CancellationToken cancellationToken)
    {
        var data = await LoadAsync(examId, cancellationToken);
        return data is null ? null : ToResponse(data.Results, data.Students, data.ProjectGrades, data.CalculatedAt).Summary;
    }

    private async Task<GradeData?> LoadAsync(Guid examId, CancellationToken cancellationToken)
    {
        var exam = await GetAccessibleExamAsync(examId, cancellationToken);
        if (exam is null) return null;
        var results = await dbContext.ExamResults.AsNoTracking().Include(item => item.Adjustments).Where(item => item.ExamId == examId).ToListAsync(cancellationToken);
        var students = await dbContext.ExamImports.Where(item => item.ExamId == examId).SelectMany(item => item.ImportedStudents).AsNoTracking().ToListAsync(cancellationToken);
        var projectGrades = await dbContext.ProjectGradeImports.AsNoTracking().Where(item => item.ExamId == examId).ToListAsync(cancellationToken);
        var calculatedAt = results.Count == 0 ? null : results.Max(item => (DateTime?)item.CalculatedAt);
        return new GradeData(exam, students, results, projectGrades, calculatedAt);
    }

    private async Task<CalculationData?> LoadCalculationDataAsync(Guid examId, CancellationToken cancellationToken)
    {
        var exam = await GetAccessibleExamAsync(examId, cancellationToken);
        if (exam is null) return null;
        var answerKey = await dbContext.ExamAnswerKeys.AsNoTracking().Include(item => item.Booklets).ThenInclude(item => item.Questions).ThenInclude(item => item.ExamQuestion).SingleOrDefaultAsync(item => item.ExamId == examId, cancellationToken);
        var import = await dbContext.ExamImports.AsNoTracking().Include(item => item.ImportedStudents).Include(item => item.OpticalExamRecords).SingleOrDefaultAsync(item => item.ExamId == examId, cancellationToken);
        if (import is null) throw new InvalidOperationException("Puanlama için dosya import işlemi tamamlanmalıdır.");
        var matches = await dbContext.StudentMatches.AsNoTracking().Include(item => item.OpticalExamRecord).Where(item => item.ExamId == examId).ToListAsync(cancellationToken);
        return new CalculationData(exam, answerKey, import.ImportedStudents.ToList(), matches);
    }

    private async Task<Exam?> GetAccessibleExamAsync(Guid examId, CancellationToken cancellationToken)
    {
        var exam = await dbContext.Exams.AsNoTracking().SingleOrDefaultAsync(item => item.Id == examId, cancellationToken);
        if (exam is null) return null;
        var user = currentUser.Get();
        return user.IsAdministrator || user.Id.HasValue && await dbContext.CourseInstructors.AnyAsync(item => item.CourseId == exam.CourseId && item.UserId == user.Id.Value, cancellationToken) ? exam : null;
    }

    private static void EnsureReady(Exam exam, ExamAnswerKey? answerKey)
    {
        if (exam.EvaluationType != EvaluationType.MultipleChoice) throw new InvalidOperationException("Puanlama yalnızca çoktan seçmeli sınavlarda kullanılabilir.");
        if (answerKey is null || answerKey.Status != AnswerKeyStatus.Validated) throw new InvalidOperationException("Puanlama için cevap anahtarı doğrulanmalıdır.");
        if (!exam.MatchingConfirmed) throw new InvalidOperationException("Puanlama için öğrenci eşleştirmesi onaylanmalıdır.");
    }

    private static decimal CalculateScore(string answers, ExamBooklet booklet)
    {
        var score = 0m;
        foreach (var question in booklet.Questions.Where(item => item.ExamQuestion.Status == ExamQuestionStatus.Active))
        {
            var index = question.DisplayQuestionNumber - 1;
            var answer = index >= 0 && index < answers.Length ? answers[index].ToString().Trim().ToUpperInvariant() : string.Empty;
            if (question.AcceptedAnswers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains(answer, StringComparer.OrdinalIgnoreCase)) score += question.ExamQuestion.Points;
        }
        return decimal.Round(Math.Clamp(score, 0, 100), 2, MidpointRounding.AwayFromZero);
    }

    private static void ValidateScore(decimal score)
    {
        if (score is < 0 or > 100) throw new ArgumentException("Düzeltilmiş puan 0 ile 100 arasında olmalıdır.");
    }

    private static GradesResponse ToResponse(IReadOnlyCollection<ExamResult> results, IReadOnlyCollection<ImportedStudent> students, IReadOnlyCollection<ProjectGradeImport> projectGrades, DateTime? calculatedAt)
    {
        var resultMap = results.ToDictionary(item => item.ImportedStudentId);
        var projectMap = projectGrades.ToDictionary(item => item.ImportedStudentId);
        var rows = students.Select(student =>
        {
            resultMap.TryGetValue(student.Id, out var result);
            var status = result?.Status ?? GradeStatus.NotEntered;
            projectMap.TryGetValue(student.Id, out var project);
            var score = status == GradeStatus.Graded ? result?.AdjustedScore ?? result?.RawScore : null;
            return new GradeRowResponse(result?.Id ?? Guid.Empty, student.Id, student.StudentNumber, $@"{student.FirstName} {student.LastName}".Trim(), result?.RawScore, result?.AdjustedScore, result?.Adjustments.Sum(item => item.Amount) ?? 0, project?.ProjectScore, status, result?.UpdatedAt ?? DateTime.MinValue, GradeLetterCalculator.Calculate(score));
        }).OrderBy(item => item.StudentNumber, StringComparer.Ordinal).ToList();
        var scores = rows.Where(item => item.Status == GradeStatus.Graded && item.AdjustedScore.HasValue).Select(item => item.AdjustedScore!.Value).OrderBy(item => item).ToList();
        decimal? average = scores.Count == 0 ? null : scores.Average();
        decimal? median = scores.Count == 0 ? null : scores.Count % 2 == 1 ? scores[scores.Count / 2] : (scores[scores.Count / 2 - 1] + scores[scores.Count / 2]) / 2;
        double? variance = scores.Count == 0 ? null : scores.Sum(score => Math.Pow((double)(score - average!.Value), 2)) / scores.Count;
        return new GradesResponse(rows, new GradeSummaryResponse(rows.Count, scores.Count, rows.Count(item => item.Status == GradeStatus.Absent), average, median, variance is null ? null : (decimal)Math.Sqrt(variance.Value), scores.Count == 0 ? null : scores.Max(), scores.Count == 0 ? null : scores.Min()), calculatedAt);
    }

    private static IReadOnlyCollection<ProjectRow> ParseProjectGrades(Stream content, string fileName)
    {
        try
        {
            using var workbook = new XLWorkbook(content);
            var sheet = workbook.Worksheets.FirstOrDefault() ?? throw new ArgumentException("XLSX dosyasında çalışma sayfası bulunamadı.");
            var range = sheet.RangeUsed() ?? throw new ArgumentException("Proje notu dosyası boş.");
            var headers = FindHeaders(range);
            var rows = new List<ProjectRow>();
            for (var row = headers.Row + 1; row <= range.LastRow().RowNumber(); row++)
            {
                var number = sheet.Cell(row, headers.NumberColumn).GetFormattedString().Trim();
                var scoreText = sheet.Cell(row, headers.ScoreColumn).GetFormattedString().Trim();
                if (string.IsNullOrWhiteSpace(number) && string.IsNullOrWhiteSpace(scoreText)) continue;
                decimal? score = decimal.TryParse(scoreText, System.Globalization.NumberStyles.Number, new System.Globalization.CultureInfo("tr-TR"), out var value) || decimal.TryParse(scoreText, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out value) ? value : null;
                rows.Add(new ProjectRow(number, score, row));
            }
            return rows;
        }
        catch (ArgumentException) { throw; }
        catch (Exception exception) { throw new ArgumentException($@"{Path.GetFileName(fileName)} geçerli bir proje notu dosyası olarak okunamadı.", exception); }
    }

    private static (int Row, int NumberColumn, int ScoreColumn) FindHeaders(IXLRange range)
    {
        for (var row = range.FirstRow().RowNumber(); row <= Math.Min(range.LastRow().RowNumber(), range.FirstRow().RowNumber() + 10); row++)
        {
            var columns = range.Worksheet.Row(row).CellsUsed().ToDictionary(item => Normalize(item.GetFormattedString()), item => item.Address.ColumnNumber);
            var number = columns.FirstOrDefault(item => item.Key is "öğrencinumarası" or "öğrencino" or "ogrencinumarasi" or "ogrencino" or "studentnumber").Value;
            var score = columns.FirstOrDefault(item => item.Key is "projenotu" or "projeskoru" or "projectgrade" or "projectscore").Value;
            if (number > 0 && score > 0) return (row, number, score);
        }
        throw new ArgumentException("Proje notu başlıklarında öğrenci numarası ve proje notu sütunları bulunamadı.");
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant().Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);
    private Guid GetUserId() => currentUser.Get().Id ?? throw new InvalidOperationException("Oturum kullanıcısı bulunamadı.");
    private sealed record GradeData(Exam Exam, IReadOnlyCollection<ImportedStudent> Students, IReadOnlyCollection<ExamResult> Results, IReadOnlyCollection<ProjectGradeImport> ProjectGrades, DateTime? CalculatedAt);
    private sealed record CalculationData(Exam Exam, ExamAnswerKey? AnswerKey, IReadOnlyCollection<ImportedStudent> Students, IReadOnlyCollection<StudentMatch> Matches);
    private sealed record ProjectRow(string Number, decimal? Score, int RowNumber);
}
