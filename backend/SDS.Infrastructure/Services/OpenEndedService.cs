// Açık uçlu sınav ayarlarını ve öğrenci ham notlarını yönetir.
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using SDS.Application.Grades;
using SDS.Application.OpenEnded;
using SDS.Application.Security;
using SDS.Domain.Entities;
using SDS.Infrastructure.Persistence;

namespace SDS.Infrastructure.Services;

public sealed class OpenEndedService(SdsDbContext dbContext, ICurrentUser currentUser, IGradeService gradeService) : IOpenEndedService
{
    public async Task<OpenEndedSettingsResponse?> GetSettingsAsync(Guid examId, CancellationToken cancellationToken)
    {
        var exam = await GetAccessibleExamAsync(examId, cancellationToken);
        if (exam is null) return null;
        EnsureOpenEnded(exam);
        var key = await dbContext.ExamAnswerKeys.AsNoTracking().SingleOrDefaultAsync(item => item.ExamId == examId, cancellationToken);
        return key is null ? null : new OpenEndedSettingsResponse(examId, key.QuestionCount, key.MaximumScore, key.UpdatedAt);
    }

    public async Task<OpenEndedSettingsResponse?> SaveSettingsAsync(Guid examId, SaveOpenEndedSettingsRequest request, CancellationToken cancellationToken)
    {
        var exam = await GetAccessibleExamAsync(examId, cancellationToken);
        if (exam is null) return null;
        EnsureOpenEnded(exam);
        if (request.QuestionCount <= 0 || request.MaximumScore <= 0) throw new ArgumentException("Soru sayısı ve maksimum puan pozitif olmalıdır.");
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var existing = await dbContext.ExamAnswerKeys.Include(item => item.Questions).SingleOrDefaultAsync(item => item.ExamId == examId, cancellationToken);
        if (existing is not null) dbContext.ExamAnswerKeys.Remove(existing);
        var key = new ExamAnswerKey { Id = Guid.NewGuid(), ExamId = examId, MaximumScore = request.MaximumScore, QuestionCount = request.QuestionCount, DynamicScoring = false, AllowMultipleAnswers = false, Status = AnswerKeyStatus.Validated, UpdatedAt = DateTime.UtcNow };
        key.Questions = Enumerable.Range(1, request.QuestionCount).Select(number => new ExamQuestion { Id = Guid.NewGuid(), ExamAnswerKeyId = key.Id, CanonicalQuestionNumber = number, Points = 0, Status = ExamQuestionStatus.Active }).ToList();
        dbContext.ExamAnswerKeys.Add(key);
        var results = await dbContext.ExamResults.Where(item => item.ExamId == examId).ToListAsync(cancellationToken);
        dbContext.ExamResults.RemoveRange(results);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new OpenEndedSettingsResponse(examId, key.QuestionCount, key.MaximumScore, key.UpdatedAt);
    }

    public async Task<GradesResponse?> SaveGradeAsync(Guid examId, Guid studentId, OpenEndedGradeRequest request, CancellationToken cancellationToken)
    {
        var exam = await GetAccessibleExamAsync(examId, cancellationToken);
        if (exam is null) return null;
        EnsureOpenEnded(exam);
        var key = await dbContext.ExamAnswerKeys.AsNoTracking().SingleOrDefaultAsync(item => item.ExamId == examId, cancellationToken) ?? throw new InvalidOperationException("Önce açık uçlu sınav ayarlarını kaydedin.");
        var studentExists = await dbContext.ExamImports.Where(item => item.ExamId == examId).SelectMany(item => item.ImportedStudents).AnyAsync(item => item.Id == studentId, cancellationToken);
        if (!studentExists) throw new KeyNotFoundException("Öğrenci bulunamadı.");
        if (!request.IsAbsent && (!request.RawScore.HasValue || request.RawScore < 0 || request.RawScore > key.MaximumScore)) throw new ArgumentException($"Not 0 ile {key.MaximumScore:0.##} arasında olmalıdır.");
        var result = await dbContext.ExamResults.SingleOrDefaultAsync(item => item.ExamId == examId && item.ImportedStudentId == studentId, cancellationToken);
        if (result is null) { result = new ExamResult { Id = Guid.NewGuid(), ExamId = examId, ImportedStudentId = studentId }; dbContext.ExamResults.Add(result); }
        result.Status = request.IsAbsent ? GradeStatus.Absent : GradeStatus.Graded;
        result.RawScore = request.IsAbsent ? null : request.RawScore;
        result.AdjustedScore = request.IsAbsent ? null : request.RawScore;
        result.CalculatedAt = DateTime.UtcNow;
        result.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return await gradeService.GetAsync(examId, cancellationToken);
    }

    public async Task<OpenEndedGradeImportResponse?> ImportGradesAsync(Guid examId, Stream content, string fileName, CancellationToken cancellationToken)
    {
        var exam = await GetAccessibleExamAsync(examId, cancellationToken);
        if (exam is null) return null;
        EnsureOpenEnded(exam);
        var key = await dbContext.ExamAnswerKeys.AsNoTracking().SingleOrDefaultAsync(item => item.ExamId == examId, cancellationToken) ?? throw new InvalidOperationException("Önce açık uçlu sınav ayarlarını kaydedin.");
        var students = await dbContext.ExamImports.Where(item => item.ExamId == examId).SelectMany(item => item.ImportedStudents).ToDictionaryAsync(item => item.StudentNumber, StringComparer.Ordinal, cancellationToken);
        var rows = ParseGrades(content, fileName);
        var issues = new List<string>();
        var validRows = new Dictionary<string, (decimal Score, int Row)>();
        foreach (var row in rows)
        {
            if (validRows.ContainsKey(row.Number)) { issues.Add($"{row.RowNumber}. satır: Öğrenci numarası tekrar ediyor."); continue; }
            if (!students.ContainsKey(row.Number)) { issues.Add($"{row.RowNumber}. satır: Öğrenci sınıf listesinde bulunamadı."); continue; }
            if (row.Score is null || row.Score < 0 || row.Score > key.MaximumScore) { issues.Add($"{row.RowNumber}. satır: Not 0 ile {key.MaximumScore:0.##} arasında olmalıdır."); continue; }
            validRows[row.Number] = (row.Score.Value, row.RowNumber);
        }
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        foreach (var row in validRows)
        {
            var student = students[row.Key];
            var result = await dbContext.ExamResults.SingleOrDefaultAsync(item => item.ExamId == examId && item.ImportedStudentId == student.Id, cancellationToken);
            if (result is null) { result = new ExamResult { Id = Guid.NewGuid(), ExamId = examId, ImportedStudentId = student.Id }; dbContext.ExamResults.Add(result); }
            result.RawScore = row.Value.Score;
            result.AdjustedScore = row.Value.Score;
            result.Status = GradeStatus.Graded;
            result.CalculatedAt = DateTime.UtcNow;
            result.UpdatedAt = DateTime.UtcNow;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var grades = await gradeService.GetAsync(examId, cancellationToken);
        return grades is null ? null : new OpenEndedGradeImportResponse(grades, validRows.Count, issues);
    }

    private async Task<Exam?> GetAccessibleExamAsync(Guid examId, CancellationToken cancellationToken)
    {
        var exam = await dbContext.Exams.AsNoTracking().SingleOrDefaultAsync(item => item.Id == examId, cancellationToken);
        if (exam is null) return null;
        var user = currentUser.Get();
        return user.IsAdministrator || user.Id.HasValue && await dbContext.CourseInstructors.AnyAsync(item => item.CourseId == exam.CourseId && item.UserId == user.Id.Value, cancellationToken) ? exam : null;
    }

    private static void EnsureOpenEnded(Exam exam)
    {
        if (exam.EvaluationType != EvaluationType.OpenEnded) throw new InvalidOperationException("Bu işlem yalnızca açık uçlu sınavlarda kullanılabilir.");
    }

    private static IReadOnlyCollection<GradeRow> ParseGrades(Stream content, string fileName)
    {
        try
        {
            using var workbook = new XLWorkbook(content);
            var sheet = workbook.Worksheets.FirstOrDefault() ?? throw new ArgumentException("XLSX dosyasında çalışma sayfası bulunamadı.");
            var range = sheet.RangeUsed() ?? throw new ArgumentException("Not dosyası boş.");
            var headers = FindHeaders(range);
            var rows = new List<GradeRow>();
            for (var row = headers.Row + 1; row <= range.LastRow().RowNumber(); row++)
            {
                var number = sheet.Cell(row, headers.NumberColumn).GetFormattedString().Trim();
                var scoreText = sheet.Cell(row, headers.ScoreColumn).GetFormattedString().Trim();
                if (string.IsNullOrWhiteSpace(number) && string.IsNullOrWhiteSpace(scoreText)) continue;
                var parsed = decimal.TryParse(scoreText, System.Globalization.NumberStyles.Number, new System.Globalization.CultureInfo("tr-TR"), out var score) || decimal.TryParse(scoreText, System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out score) ? score : (decimal?)null;
                rows.Add(new GradeRow(number, parsed, row));
            }
            return rows;
        }
        catch (ArgumentException) { throw; }
        catch (Exception exception) { throw new ArgumentException($"{Path.GetFileName(fileName)} geçerli bir açık uçlu not dosyası olarak okunamadı.", exception); }
    }

    private static (int Row, int NumberColumn, int ScoreColumn) FindHeaders(IXLRange range)
    {
        for (var row = range.FirstRow().RowNumber(); row <= Math.Min(range.LastRow().RowNumber(), range.FirstRow().RowNumber() + 10); row++)
        {
            var columns = range.Worksheet.Row(row).CellsUsed().ToDictionary(item => Normalize(item.GetFormattedString()), item => item.Address.ColumnNumber);
            var number = columns.FirstOrDefault(item => item.Key is "öğrencinumarası" or "öğrencino" or "ogrencinumarasi" or "ogrencino" or "studentnumber").Value;
            var score = columns.FirstOrDefault(item => item.Key is "aldığınot" or "aldığınnot" or "sinavnotu" or "sınavnotu" or "not" or "grade" or "score").Value;
            if (number > 0 && score > 0) return (row, number, score);
        }
        throw new ArgumentException("Not dosyası başlıklarında öğrenci numarası ve aldığı not sütunları bulunamadı.");
    }

    private static string Normalize(string value) => value.Trim().ToLowerInvariant().Replace(" ", string.Empty).Replace("_", string.Empty).Replace("-", string.Empty);
    private sealed record GradeRow(string Number, decimal? Score, int RowNumber);
}
