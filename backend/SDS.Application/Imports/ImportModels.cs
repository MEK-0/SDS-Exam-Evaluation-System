// Dosya ayrıştırma sonuçlarını ve import önizleme modellerini tanımlar.
namespace SDS.Application.Imports;

public sealed record OpticalFormConfigurationInput(
    int StudentNumberStart,
    int StudentNumberLength,
    int? StudentNameStart,
    int? StudentNameLength,
    int BookletTypeStart,
    int BookletTypeLength,
    int? ExamTypeStart,
    int? ExamTypeLength,
    int AnswersStart,
    int AnswersLength,
    string EncodingName);

public sealed record ParsedOpticalRecord(int LineNumber, string StudentNumber, string StudentName, string BookletType, string Answers, bool IsValid, string? ErrorMessage, bool IsAnswerKey = false);
public sealed record OpticalParseResult(IReadOnlyCollection<ParsedOpticalRecord> Records, IReadOnlyCollection<ImportIssue> Issues);
public sealed record ParsedStudent(int RowNumber, string StudentNumber, string FirstName, string LastName, string SourceFileName = "");
public sealed record ClassListParseResult(IReadOnlyCollection<ParsedStudent> Students, IReadOnlyCollection<ImportIssue> Issues);
public sealed record ImportIssue(string Source, int? RowNumber, string Message);
public sealed record ClassListFileInput(Stream Content, string FileName);
public sealed record ImportPreviewResponse(Guid ImportId, string OpticalFileName, string ClassListFileName, IReadOnlyCollection<string> ClassListFileNames, int TotalOpticalRecordCount, int ValidOpticalRecordCount, int InvalidOpticalRecordCount, int StudentCount, IReadOnlyCollection<OpticalRecordPreview> OpticalRecords, IReadOnlyCollection<StudentPreview> Students, IReadOnlyCollection<ImportIssue> Issues, DateTime CreatedAt);
public sealed record OpticalRecordPreview(int LineNumber, string StudentNumber, string StudentName, string BookletType, string Answers, bool IsValid, string? ErrorMessage, bool IsAnswerKey = false);
public sealed record StudentPreview(int RowNumber, string StudentNumber, string FirstName, string LastName, string SourceFileName);
public sealed record OpticalRecordResponse(Guid Id, int LineNumber, string StudentNumber, string StudentName, string BookletType, string Answers, bool IsValid, string? ErrorMessage, bool IsAnswerKey = false);

public interface IOpticalFileParser
{
    Task<OpticalParseResult> ParseAsync(Stream content, OpticalFormConfigurationInput configuration, CancellationToken cancellationToken);
}

public interface IClassListParser
{
    Task<ClassListParseResult> ParseAsync(Stream content, CancellationToken cancellationToken);
}

public interface IStudentDataImportService
{
    Task<ImportPreviewResponse> ImportAsync(Guid examId, Stream opticalFile, string opticalFileName, IReadOnlyCollection<ClassListFileInput> classListFiles, OpticalFormConfigurationInput configuration, CancellationToken cancellationToken);
    Task<ImportPreviewResponse> ImportClassListAsync(Guid examId, IReadOnlyCollection<ClassListFileInput> classListFiles, CancellationToken cancellationToken);
    Task<bool?> RemoveAsync(Guid examId, string fileType, string? fileName, CancellationToken cancellationToken);
    Task<ImportPreviewResponse?> GetPreviewAsync(Guid examId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ImportIssue>?> GetIssuesAsync(Guid examId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<OpticalRecordResponse>?> GetOpticalRecordsAsync(Guid examId, CancellationToken cancellationToken);
}
