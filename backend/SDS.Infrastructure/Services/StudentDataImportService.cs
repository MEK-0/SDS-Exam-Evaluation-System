// Optik ve sınıf listesi dosyalarını doğrulayıp sınav import kayıtlarına dönüştürür.
using Microsoft.EntityFrameworkCore;
using SDS.Application.Imports;
using SDS.Application.Security;
using SDS.Domain.Entities;
using SDS.Infrastructure.Persistence;

namespace SDS.Infrastructure.Services;

public sealed class StudentDataImportService(SdsDbContext dbContext, ICurrentUser currentUser, IOpticalFileParser opticalFileParser, IClassListParser classListParser) : IStudentDataImportService
{
    public async Task<ImportPreviewResponse> ImportAsync(Guid examId, Stream opticalFile, string opticalFileName, IReadOnlyCollection<ClassListFileInput> classListFiles, OpticalFormConfigurationInput configuration, CancellationToken cancellationToken)
    {
        var exam = await dbContext.Exams.SingleOrDefaultAsync(item => item.Id == examId, cancellationToken);
        if (exam is null || !await CanAccessCourseAsync(exam.CourseId, cancellationToken)) throw new KeyNotFoundException("Sınav bulunamadı veya erişim yetkiniz yok.");
        if (exam.EvaluationType != EvaluationType.MultipleChoice) throw new InvalidOperationException("Optik dosyası yalnızca çoktan seçmeli sınavlarda kullanılabilir.");
        var opticalResult = await opticalFileParser.ParseAsync(opticalFile, configuration, cancellationToken);
        var classListResults = new List<(ClassListFileInput File, ClassListParseResult Result)>();
        foreach (var classListFile in classListFiles)
        {
            classListFile.Content.Position = 0;
            classListResults.Add((classListFile, await classListParser.ParseAsync(classListFile.Content, cancellationToken)));
        }
        var classListStudents = new List<ParsedStudent>();
        var issues = opticalResult.Issues.ToList();
        foreach (var classListResult in classListResults)
        {
            classListStudents.AddRange(classListResult.Result.Students.Select(student => student with { SourceFileName = SafeFileName(classListResult.File.FileName) }));
            issues.AddRange(classListResult.Result.Issues.Select(issue => issue with { Message = $"{SafeFileName(classListResult.File.FileName)}: {issue.Message}" }));
        }
        var duplicateStudents = classListStudents.GroupBy(item => item.StudentNumber, StringComparer.Ordinal).Where(group => group.Count() > 1).ToList();
        issues.AddRange(duplicateStudents.Select(group => new ImportIssue("classList", null, $"Mükerrer öğrenci numarası {group.Key} birden fazla sınıf listesinde bulundu.")));
        classListStudents = classListStudents.GroupBy(item => item.StudentNumber, StringComparer.Ordinal).Select(group => group.First()).ToList();
        var classNumbers = classListStudents.Select(item => item.StudentNumber).ToHashSet(StringComparer.Ordinal);
        issues.AddRange(opticalResult.Records.Where(item => item.IsValid && !item.IsAnswerKey && !classNumbers.Contains(item.StudentNumber)).Select(item => new ImportIssue("matching", item.LineNumber, "Optik kaydındaki öğrenci numarası sınıf listesinde bulunamadı.")));
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var previousImport = await dbContext.ExamImports.SingleOrDefaultAsync(item => item.ExamId == examId, cancellationToken);
        dbContext.StudentMatches.RemoveRange(dbContext.StudentMatches.Where(item => item.ExamId == examId));
        exam.MatchingCalculated = false;
        exam.MatchingConfirmed = false;
        exam.MatchingUpdatedAt = DateTime.UtcNow;
        if (previousImport is not null)
        {
            dbContext.ExamImports.Remove(previousImport);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        var import = new ExamImport
        {
            Id = Guid.NewGuid(),
            ExamId = examId,
            OpticalFileName = SafeFileName(opticalFileName),
            ClassListFileName = string.Join(", ", classListFiles.Select(item => SafeFileName(item.FileName))),
            CreatedAt = DateTime.UtcNow,
            TotalOpticalRecordCount = opticalResult.Records.Count,
            ValidOpticalRecordCount = opticalResult.Records.Count(item => item.IsValid),
            InvalidOpticalRecordCount = opticalResult.Records.Count(item => !item.IsValid)
        };
        import.ImportedStudents = classListStudents.Select(item => new ImportedStudent { Id = Guid.NewGuid(), ExamImportId = import.Id, StudentNumber = item.StudentNumber, FirstName = item.FirstName, LastName = item.LastName, SourceFileName = item.SourceFileName, RowNumber = item.RowNumber }).ToList();
        import.OpticalExamRecords = opticalResult.Records.Select(item => new OpticalExamRecord { Id = Guid.NewGuid(), ExamImportId = import.Id, LineNumber = item.LineNumber, StudentNumber = item.StudentNumber, StudentName = item.StudentName, BookletType = item.BookletType, Answers = item.Answers, IsValid = item.IsValid, IsAnswerKey = item.IsAnswerKey, ErrorMessage = item.ErrorMessage }).ToList();
        import.ValidationIssues = issues.Select(item => new ImportValidationIssue { Id = Guid.NewGuid(), ExamImportId = import.Id, Source = item.Source, RowNumber = item.RowNumber, Message = item.Message }).ToList();
        dbContext.ExamImports.Add(import);
        var formConfiguration = await dbContext.OpticalFormConfigurations.SingleOrDefaultAsync(item => item.ExamId == examId, cancellationToken);
        if (formConfiguration is null) dbContext.OpticalFormConfigurations.Add(CreateConfiguration(examId, configuration));
        else UpdateConfiguration(formConfiguration, configuration);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(import, opticalResult.Records, classListStudents, issues);
    }

    public async Task<ImportPreviewResponse> ImportClassListAsync(Guid examId, IReadOnlyCollection<ClassListFileInput> classListFiles, CancellationToken cancellationToken)
    {
        var exam = await dbContext.Exams.SingleOrDefaultAsync(item => item.Id == examId, cancellationToken);
        if (exam is null || !await CanAccessCourseAsync(exam.CourseId, cancellationToken)) throw new KeyNotFoundException("Sınav bulunamadı veya erişim yetkiniz yok.");
        if (exam.EvaluationType != EvaluationType.OpenEnded) throw new InvalidOperationException("Bu sınıf listesi akışı yalnızca açık uçlu sınavlarda kullanılabilir.");
        if (classListFiles.Count is < 1 or > 2) throw new ArgumentException("En az bir, en fazla iki sınıf listesi yükleyebilirsiniz.");
        var parsedStudents = new List<ParsedStudent>();
        var issues = new List<ImportIssue>();
        foreach (var file in classListFiles)
        {
            file.Content.Position = 0;
            var parsed = await classListParser.ParseAsync(file.Content, cancellationToken);
            parsedStudents.AddRange(parsed.Students.Select(student => student with { SourceFileName = SafeFileName(file.FileName) }));
            issues.AddRange(parsed.Issues.Select(issue => issue with { Message = $"{SafeFileName(file.FileName)}: {issue.Message}" }));
        }
        var duplicateNumbers = parsedStudents.GroupBy(item => item.StudentNumber, StringComparer.Ordinal).Where(group => group.Count() > 1).ToList();
        issues.AddRange(duplicateNumbers.Select(group => new ImportIssue("classList", null, $"Mükerrer öğrenci numarası {group.Key} birden fazla sınıf listesinde bulundu.")));
        var uniqueStudents = parsedStudents.GroupBy(item => item.StudentNumber, StringComparer.Ordinal).Select(group => group.First()).ToList();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var oldImport = await dbContext.ExamImports.SingleOrDefaultAsync(item => item.ExamId == examId, cancellationToken);
        dbContext.StudentMatches.RemoveRange(dbContext.StudentMatches.Where(item => item.ExamId == examId));
        dbContext.ExamResults.RemoveRange(dbContext.ExamResults.Where(item => item.ExamId == examId));
        if (oldImport is not null) { dbContext.ExamImports.Remove(oldImport); await dbContext.SaveChangesAsync(cancellationToken); }
        var import = new ExamImport { Id = Guid.NewGuid(), ExamId = examId, OpticalFileName = string.Empty, ClassListFileName = string.Join(", ", classListFiles.Select(item => SafeFileName(item.FileName))), CreatedAt = DateTime.UtcNow, TotalOpticalRecordCount = 0, ValidOpticalRecordCount = 0, InvalidOpticalRecordCount = 0 };
        import.ImportedStudents = uniqueStudents.Select(student => new ImportedStudent { Id = Guid.NewGuid(), ExamImportId = import.Id, StudentNumber = student.StudentNumber, FirstName = student.FirstName, LastName = student.LastName, SourceFileName = student.SourceFileName, RowNumber = student.RowNumber }).ToList();
        import.ValidationIssues = issues.Select(issue => new ImportValidationIssue { Id = Guid.NewGuid(), ExamImportId = import.Id, Source = issue.Source, RowNumber = issue.RowNumber, Message = issue.Message }).ToList();
        dbContext.ExamImports.Add(import);
        exam.MatchingCalculated = false; exam.MatchingConfirmed = false; exam.MatchingUpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(import, [], uniqueStudents, issues);
    }

    public async Task<bool?> RemoveAsync(Guid examId, string fileType, string? fileName, CancellationToken cancellationToken)
    {
        var exam = await dbContext.Exams.SingleOrDefaultAsync(item => item.Id == examId, cancellationToken);
        if (exam is null || !await CanAccessCourseAsync(exam.CourseId, cancellationToken)) return null;
        var import = await dbContext.ExamImports.Include(item => item.ImportedStudents).Include(item => item.OpticalExamRecords).SingleOrDefaultAsync(item => item.ExamId == examId, cancellationToken);
        if (import is null) return true;
        if (!string.Equals(fileType, "optical", StringComparison.OrdinalIgnoreCase) && !string.Equals(fileType, "classList", StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Dosya türü optical veya classList olmalıdır.");
        dbContext.StudentMatches.RemoveRange(dbContext.StudentMatches.Where(item => item.ExamId == examId));
        exam.MatchingCalculated = false;
        exam.MatchingConfirmed = false;
        exam.MatchingUpdatedAt = DateTime.UtcNow;
        if (string.Equals(fileType, "optical", StringComparison.OrdinalIgnoreCase))
        {
            dbContext.OpticalExamRecords.RemoveRange(import.OpticalExamRecords);
            import.OpticalFileName = string.Empty;
            import.TotalOpticalRecordCount = 0;
            import.ValidOpticalRecordCount = 0;
            import.InvalidOpticalRecordCount = 0;
        }
        else
        {
            var names = import.ClassListFileName.Split(", ", StringSplitOptions.RemoveEmptyEntries).ToList();
            var targetName = string.IsNullOrWhiteSpace(fileName) ? null : SafeFileName(fileName);
            if (targetName is not null && !names.Contains(targetName, StringComparer.Ordinal)) return false;
            var removedStudents = targetName is null ? import.ImportedStudents : import.ImportedStudents.Where(item => item.SourceFileName == targetName).ToList();
            dbContext.ImportedStudents.RemoveRange(removedStudents);
            import.ClassListFileName = targetName is null ? string.Empty : string.Join(", ", names.Where(name => !string.Equals(name, targetName, StringComparison.Ordinal)));
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<ImportPreviewResponse?> GetPreviewAsync(Guid examId, CancellationToken cancellationToken)
    {
        var import = await GetAccessibleImportAsync(examId, cancellationToken);
        return import is null ? null : ToResponse(import, import.OpticalExamRecords.Select(ToParsedRecord).ToList(), import.ImportedStudents.Select(item => new ParsedStudent(item.RowNumber, item.StudentNumber, item.FirstName, item.LastName, item.SourceFileName)).ToList(), import.ValidationIssues.Select(item => new ImportIssue(item.Source, item.RowNumber, item.Message)).ToList());
    }

    public async Task<IReadOnlyCollection<ImportIssue>?> GetIssuesAsync(Guid examId, CancellationToken cancellationToken)
    {
        var import = await GetAccessibleImportAsync(examId, cancellationToken);
        return import?.ValidationIssues.Select(item => new ImportIssue(item.Source, item.RowNumber, item.Message)).ToList();
    }

    public async Task<IReadOnlyCollection<OpticalRecordResponse>?> GetOpticalRecordsAsync(Guid examId, CancellationToken cancellationToken)
    {
        var import = await GetAccessibleImportAsync(examId, cancellationToken);
        return import?.OpticalExamRecords.OrderBy(item => item.LineNumber).Select(item =>
        {
            var importedStudent = item.IsAnswerKey ? null : import.ImportedStudents.FirstOrDefault(candidate => string.Equals(candidate.StudentNumber.Trim(), item.StudentNumber.Trim(), StringComparison.Ordinal));
            var studentName = string.IsNullOrWhiteSpace(item.StudentName)
                ? importedStudent is null ? string.Empty : $"{importedStudent.FirstName} {importedStudent.LastName}".Trim()
                : item.StudentName;

            return new OpticalRecordResponse(item.Id, item.LineNumber, item.StudentNumber, studentName, item.BookletType, item.Answers, item.IsValid, item.ErrorMessage, item.IsAnswerKey);
        }).ToList();
    }

    private async Task<ExamImport?> GetAccessibleImportAsync(Guid examId, CancellationToken cancellationToken)
    {
        var exam = await dbContext.Exams.AsNoTracking().SingleOrDefaultAsync(item => item.Id == examId, cancellationToken);
        if (exam is null || !await CanAccessCourseAsync(exam.CourseId, cancellationToken)) return null;
        return await dbContext.ExamImports.AsNoTracking().Include(item => item.OpticalExamRecords).Include(item => item.ImportedStudents).Include(item => item.ValidationIssues).SingleOrDefaultAsync(item => item.ExamId == examId, cancellationToken);
    }

    private async Task<bool> CanAccessCourseAsync(Guid courseId, CancellationToken cancellationToken)
    {
        var user = currentUser.Get();
        return user.IsAdministrator || user.Id.HasValue && await dbContext.CourseInstructors.AnyAsync(item => item.CourseId == courseId && item.UserId == user.Id.Value, cancellationToken);
    }

    private static OpticalFormConfiguration CreateConfiguration(Guid examId, OpticalFormConfigurationInput input) => new() { Id = Guid.NewGuid(), ExamId = examId, StudentNumberStart = input.StudentNumberStart, StudentNumberLength = input.StudentNumberLength, StudentNameStart = input.StudentNameStart, StudentNameLength = input.StudentNameLength, BookletTypeStart = input.BookletTypeStart, BookletTypeLength = input.BookletTypeLength, ExamTypeStart = input.ExamTypeStart, ExamTypeLength = input.ExamTypeLength, AnswersStart = input.AnswersStart, AnswersLength = input.AnswersLength, EncodingName = input.EncodingName };
    private static void UpdateConfiguration(OpticalFormConfiguration target, OpticalFormConfigurationInput input) { target.StudentNumberStart = input.StudentNumberStart; target.StudentNumberLength = input.StudentNumberLength; target.StudentNameStart = input.StudentNameStart; target.StudentNameLength = input.StudentNameLength; target.BookletTypeStart = input.BookletTypeStart; target.BookletTypeLength = input.BookletTypeLength; target.ExamTypeStart = input.ExamTypeStart; target.ExamTypeLength = input.ExamTypeLength; target.AnswersStart = input.AnswersStart; target.AnswersLength = input.AnswersLength; target.EncodingName = input.EncodingName; }
    private static string SafeFileName(string fileName) => Path.GetFileName(fileName);
    private static ParsedOpticalRecord ToParsedRecord(OpticalExamRecord item) => new(item.LineNumber, item.StudentNumber, item.StudentName, item.BookletType, item.Answers, item.IsValid, item.ErrorMessage, item.IsAnswerKey);
    private static ImportPreviewResponse ToResponse(ExamImport import, IReadOnlyCollection<ParsedOpticalRecord> records, IReadOnlyCollection<ParsedStudent> students, IReadOnlyCollection<ImportIssue> issues) => new(import.Id, import.OpticalFileName, import.ClassListFileName, import.ClassListFileName.Split(", ", StringSplitOptions.RemoveEmptyEntries), import.TotalOpticalRecordCount, import.ValidOpticalRecordCount, import.InvalidOpticalRecordCount, students.Count, records.Take(10).Select(item => new OpticalRecordPreview(item.LineNumber, item.StudentNumber, item.StudentName, item.BookletType, item.Answers, item.IsValid, item.ErrorMessage, item.IsAnswerKey)).ToList(), students.Take(10).Select(item => new StudentPreview(item.RowNumber, item.StudentNumber, item.FirstName, item.LastName, item.SourceFileName)).ToList(), issues, import.CreatedAt);
}
