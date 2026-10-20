// Optik ve sınıf listesi dosyalarını doğrulayıp sınav import kayıtlarına dönüştürür.
using Microsoft.EntityFrameworkCore;
using SDS.Application.Imports;
using SDS.Application.Security;
using SDS.Domain.Entities;
using SDS.Infrastructure.Persistence;

namespace SDS.Infrastructure.Services;

public sealed class StudentDataImportService(SdsDbContext dbContext, ICurrentUser currentUser, IOpticalFileParser opticalFileParser, IClassListParser classListParser) : IStudentDataImportService
{
    public async Task<ImportPreviewResponse> ImportAsync(Guid examId, Stream opticalFile, string opticalFileName, Stream classListFile, string classListFileName, OpticalFormConfigurationInput configuration, CancellationToken cancellationToken)
    {
        var exam = await dbContext.Exams.AsNoTracking().SingleOrDefaultAsync(item => item.Id == examId, cancellationToken);
        if (exam is null || !await CanAccessCourseAsync(exam.CourseId, cancellationToken)) throw new KeyNotFoundException("Sınav bulunamadı veya erişim yetkiniz yok.");
        if (exam.EvaluationType != EvaluationType.MultipleChoice) throw new InvalidOperationException("Optik dosyası yalnızca çoktan seçmeli sınavlarda kullanılabilir.");
        var opticalResult = await opticalFileParser.ParseAsync(opticalFile, configuration, cancellationToken);
        var classListResult = await classListParser.ParseAsync(classListFile, cancellationToken);
        var issues = opticalResult.Issues.Concat(classListResult.Issues).ToList();
        var classNumbers = classListResult.Students.Select(item => item.StudentNumber).ToHashSet(StringComparer.Ordinal);
        issues.AddRange(opticalResult.Records.Where(item => item.IsValid && !classNumbers.Contains(item.StudentNumber)).Select(item => new ImportIssue("matching", item.LineNumber, "Optik kaydındaki öğrenci numarası sınıf listesinde bulunamadı.")));
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var previousImport = await dbContext.ExamImports.SingleOrDefaultAsync(item => item.ExamId == examId, cancellationToken);
        if (previousImport is not null) dbContext.ExamImports.Remove(previousImport);
        var import = new ExamImport
        {
            Id = Guid.NewGuid(),
            ExamId = examId,
            OpticalFileName = SafeFileName(opticalFileName),
            ClassListFileName = SafeFileName(classListFileName),
            CreatedAt = DateTime.UtcNow,
            TotalOpticalRecordCount = opticalResult.Records.Count,
            ValidOpticalRecordCount = opticalResult.Records.Count(item => item.IsValid),
            InvalidOpticalRecordCount = opticalResult.Records.Count(item => !item.IsValid)
        };
        import.ImportedStudents = classListResult.Students.Select(item => new ImportedStudent { Id = Guid.NewGuid(), ExamImportId = import.Id, StudentNumber = item.StudentNumber, FirstName = item.FirstName, LastName = item.LastName, RowNumber = item.RowNumber }).ToList();
        import.OpticalExamRecords = opticalResult.Records.Select(item => new OpticalExamRecord { Id = Guid.NewGuid(), ExamImportId = import.Id, LineNumber = item.LineNumber, StudentNumber = item.StudentNumber, StudentName = item.StudentName, BookletType = item.BookletType, Answers = item.Answers, IsValid = item.IsValid, ErrorMessage = item.ErrorMessage }).ToList();
        import.ValidationIssues = issues.Select(item => new ImportValidationIssue { Id = Guid.NewGuid(), ExamImportId = import.Id, Source = item.Source, RowNumber = item.RowNumber, Message = item.Message }).ToList();
        dbContext.ExamImports.Add(import);
        var formConfiguration = await dbContext.OpticalFormConfigurations.SingleOrDefaultAsync(item => item.ExamId == examId, cancellationToken);
        if (formConfiguration is null) dbContext.OpticalFormConfigurations.Add(CreateConfiguration(examId, configuration));
        else UpdateConfiguration(formConfiguration, configuration);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(import, opticalResult.Records, classListResult.Students, issues);
    }

    public async Task<ImportPreviewResponse?> GetPreviewAsync(Guid examId, CancellationToken cancellationToken)
    {
        var import = await GetAccessibleImportAsync(examId, cancellationToken);
        return import is null ? null : ToResponse(import, import.OpticalExamRecords.Select(ToParsedRecord).ToList(), import.ImportedStudents.Select(item => new ParsedStudent(item.RowNumber, item.StudentNumber, item.FirstName, item.LastName)).ToList(), import.ValidationIssues.Select(item => new ImportIssue(item.Source, item.RowNumber, item.Message)).ToList());
    }

    public async Task<IReadOnlyCollection<ImportIssue>?> GetIssuesAsync(Guid examId, CancellationToken cancellationToken)
    {
        var import = await GetAccessibleImportAsync(examId, cancellationToken);
        return import?.ValidationIssues.Select(item => new ImportIssue(item.Source, item.RowNumber, item.Message)).ToList();
    }

    public async Task<IReadOnlyCollection<OpticalRecordResponse>?> GetOpticalRecordsAsync(Guid examId, CancellationToken cancellationToken)
    {
        var import = await GetAccessibleImportAsync(examId, cancellationToken);
        return import?.OpticalExamRecords.OrderBy(item => item.LineNumber).Select(item => new OpticalRecordResponse(item.Id, item.LineNumber, item.StudentNumber, item.StudentName, item.BookletType, item.Answers, item.IsValid, item.ErrorMessage)).ToList();
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
    private static ParsedOpticalRecord ToParsedRecord(OpticalExamRecord item) => new(item.LineNumber, item.StudentNumber, item.StudentName, item.BookletType, item.Answers, item.IsValid, item.ErrorMessage);
    private static ImportPreviewResponse ToResponse(ExamImport import, IReadOnlyCollection<ParsedOpticalRecord> records, IReadOnlyCollection<ParsedStudent> students, IReadOnlyCollection<ImportIssue> issues) => new(import.Id, import.OpticalFileName, import.ClassListFileName, import.TotalOpticalRecordCount, import.ValidOpticalRecordCount, import.InvalidOpticalRecordCount, students.Count, records.Take(10).Select(item => new OpticalRecordPreview(item.LineNumber, item.StudentNumber, item.StudentName, item.BookletType, item.Answers, item.IsValid, item.ErrorMessage)).ToList(), students.Take(10).Select(item => new StudentPreview(item.RowNumber, item.StudentNumber, item.FirstName, item.LastName)).ToList(), issues, import.CreatedAt);
}
