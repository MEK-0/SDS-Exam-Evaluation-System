// Sınav öğrencileri ile optik kayıtların otomatik ve manuel eşleştirmesini yürütür.
using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using SDS.Application.Matching;
using SDS.Application.Security;
using SDS.Domain.Entities;
using SDS.Infrastructure.Persistence;

namespace SDS.Infrastructure.Services;

public sealed class StudentMatchingService(SdsDbContext dbContext, ICurrentUser currentUser) : IStudentMatchingService
{
    public async Task<MatchingResponse?> RunAsync(Guid examId, CancellationToken cancellationToken)
    {
        var data = await GetDataAsync(examId, cancellationToken);
        if (data is null) return null;
        EnsureMultipleChoice(data.Exam);
        if (data.Import is null) throw new InvalidOperationException("Eşleştirme için önce dosya import işlemi tamamlanmalıdır.");
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var manualMatches = data.Matches.Where(item => item.MatchType == StudentMatchType.Manual).ToList();
        dbContext.StudentMatches.RemoveRange(data.Matches.Where(item => item.MatchType != StudentMatchType.Manual));
        var classGroups = data.Import.ImportedStudents.GroupBy(item => Normalize(item.StudentNumber), StringComparer.Ordinal).ToDictionary(item => item.Key, item => item.ToList(), StringComparer.Ordinal);
        var opticalGroups = data.Import.OpticalExamRecords.Where(item => item.IsValid && !item.IsAnswerKey && !string.IsNullOrWhiteSpace(item.StudentNumber)).GroupBy(item => Normalize(item.StudentNumber), StringComparer.Ordinal).ToDictionary(item => item.Key, item => item.ToList(), StringComparer.Ordinal);
        var manualStudentIds = manualMatches.Select(item => item.ImportedStudentId).ToHashSet();
        var manualOpticalIds = manualMatches.Where(item => item.OpticalExamRecordId.HasValue).Select(item => item.OpticalExamRecordId!.Value).ToHashSet();
        var automaticMatches = classGroups.Where(item => item.Value.Count == 1 && opticalGroups.TryGetValue(item.Key, out var records) && records.Count == 1).Select(item => new { Student = item.Value[0], Record = opticalGroups[item.Key][0] }).Where(item => !manualStudentIds.Contains(item.Student.Id) && !manualOpticalIds.Contains(item.Record.Id)).Select(item => new StudentMatch { Id = Guid.NewGuid(), ExamId = examId, ImportedStudentId = item.Student.Id, OpticalExamRecordId = item.Record.Id, MatchType = string.Equals(item.Student.StudentNumber, item.Record.StudentNumber, StringComparison.Ordinal) ? StudentMatchType.Exact : StudentMatchType.Normalized, Status = StudentMatchStatus.Matched, MatchedAt = DateTime.UtcNow, MatchedByUserId = GetUserId() }).ToList();
        dbContext.StudentMatches.AddRange(automaticMatches);
        var exam = data.Exam;
        exam.MatchingCalculated = true;
        exam.MatchingConfirmed = false;
        exam.MatchingUpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(examId, cancellationToken);
    }

    public async Task<MatchingResponse?> GetAsync(Guid examId, CancellationToken cancellationToken)
    {
        var data = await GetDataAsync(examId, cancellationToken);
        if (data is null) return null;
        return BuildResponse(data.Exam, data.Import, data.Matches);
    }

    public async Task<MatchingResponse?> CreateManualAsync(Guid examId, ManualMatchRequest request, CancellationToken cancellationToken)
    {
        var data = await GetDataAsync(examId, cancellationToken);
        if (data is null) return null;
        EnsureMultipleChoice(data.Exam);
        if (data.Import is null) throw new InvalidOperationException("Eşleştirme için önce dosya import işlemi tamamlanmalıdır.");
        var student = data.Import.ImportedStudents.SingleOrDefault(item => item.Id == request.ImportedStudentId);
        var opticalRecord = data.Import.OpticalExamRecords.SingleOrDefault(item => item.Id == request.OpticalExamRecordId);
        if (student is null || opticalRecord is null) throw new KeyNotFoundException("Öğrenci veya optik kaydı bulunamadı.");
        if (!opticalRecord.IsValid || opticalRecord.IsAnswerKey || string.IsNullOrWhiteSpace(opticalRecord.StudentNumber)) throw new InvalidOperationException("Geçersiz veya cevap anahtarı kaydı manuel olarak eşleştirilemez.");
        var existingStudentMatch = data.Matches.SingleOrDefault(item => item.ImportedStudentId == student.Id);
        var existingOpticalMatch = data.Matches.SingleOrDefault(item => item.OpticalExamRecordId == opticalRecord.Id);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (existingStudentMatch is not null)
        {
            dbContext.StudentMatches.Remove(existingStudentMatch);
        }
        if (existingOpticalMatch is not null && existingOpticalMatch.Id != existingStudentMatch?.Id) dbContext.StudentMatches.Remove(existingOpticalMatch);
        var match = new StudentMatch { Id = Guid.NewGuid(), ExamId = examId, ImportedStudentId = student.Id, OpticalExamRecordId = opticalRecord.Id, MatchType = StudentMatchType.Manual, Status = StudentMatchStatus.Matched, MatchedAt = DateTime.UtcNow, MatchedByUserId = GetUserId() };
        dbContext.StudentMatches.Add(match);
        dbContext.StudentMatchAudits.Add(CreateAudit(examId, match.Id, existingStudentMatch?.ImportedStudentId ?? existingOpticalMatch?.ImportedStudentId, existingStudentMatch?.OpticalExamRecordId ?? existingOpticalMatch?.OpticalExamRecordId, student.Id, opticalRecord.Id, "ManualMatch"));
        data.Exam.MatchingConfirmed = false;
        data.Exam.MatchingUpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(examId, cancellationToken);
    }

    public async Task<MatchingResponse?> MarkAbsentAsync(Guid examId, MarkAbsentRequest request, CancellationToken cancellationToken)
    {
        var data = await GetDataAsync(examId, cancellationToken);
        if (data is null) return null;
        if (data.Import is null) throw new InvalidOperationException("Eşleştirme için önce dosya import işlemi tamamlanmalıdır.");
        var student = data.Import.ImportedStudents.SingleOrDefault(item => item.Id == request.ImportedStudentId);
        if (student is null) throw new KeyNotFoundException("Öğrenci bulunamadı.");
        var existing = data.Matches.SingleOrDefault(item => item.ImportedStudentId == student.Id);
        if (existing?.OpticalExamRecordId is not null) throw new InvalidOperationException("Optik kaydı bulunan öğrenci sınava girmedi olarak işaretlenemez.");
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (existing is null)
        {
            existing = new StudentMatch { Id = Guid.NewGuid(), ExamId = examId, ImportedStudentId = student.Id, MatchType = StudentMatchType.Manual, Status = StudentMatchStatus.Absent, IsAbsent = true, MatchedAt = DateTime.UtcNow, MatchedByUserId = GetUserId() };
            dbContext.StudentMatches.Add(existing);
        }
        else
        {
            existing.Status = StudentMatchStatus.Absent;
            existing.IsAbsent = true;
            existing.MatchType = StudentMatchType.Manual;
            existing.MatchedAt = DateTime.UtcNow;
            existing.MatchedByUserId = GetUserId();
        }
        dbContext.StudentMatchAudits.Add(CreateAudit(examId, existing.Id, student.Id, null, student.Id, null, "MarkAbsent"));
        data.Exam.MatchingConfirmed = false;
        data.Exam.MatchingUpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(examId, cancellationToken);
    }

    public async Task<MatchingResponse?> RemoveManualAsync(Guid examId, Guid matchId, CancellationToken cancellationToken)
    {
        var data = await GetDataAsync(examId, cancellationToken);
        if (data is null) return null;
        var match = data.Matches.SingleOrDefault(item => item.Id == matchId && item.MatchType == StudentMatchType.Manual);
        if (match is null) throw new KeyNotFoundException("Manuel eşleştirme bulunamadı.");
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        dbContext.StudentMatchAudits.Add(CreateAudit(examId, null, match.ImportedStudentId, match.OpticalExamRecordId, null, null, "RemoveManual"));
        dbContext.StudentMatches.Remove(match);
        data.Exam.MatchingConfirmed = false;
        data.Exam.MatchingUpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(examId, cancellationToken);
    }

    public async Task<MatchingResponse?> IgnoreOpticalAsync(Guid examId, IgnoreOpticalRequest request, CancellationToken cancellationToken)
    {
        var data = await GetDataAsync(examId, cancellationToken);
        if (data is null) return null;
        EnsureMultipleChoice(data.Exam);
        if (data.Import is null) throw new InvalidOperationException("Eşleştirme için önce dosya import işlemi tamamlanmalıdır.");
        var opticalRecord = data.Import.OpticalExamRecords.SingleOrDefault(item => item.Id == request.OpticalExamRecordId);
        if (opticalRecord is null || !opticalRecord.IsValid || opticalRecord.IsAnswerKey) throw new KeyNotFoundException("Geçerli optik kaydı bulunamadı.");
        if (data.Matches.Any(item => item.OpticalExamRecordId == opticalRecord.Id)) throw new InvalidOperationException("Bu optik kaydı zaten işlenmiş.");
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var ignored = new StudentMatch { Id = Guid.NewGuid(), ExamId = examId, OpticalExamRecordId = opticalRecord.Id, MatchType = StudentMatchType.Manual, Status = StudentMatchStatus.Absent, IsAbsent = true, MatchedAt = DateTime.UtcNow, MatchedByUserId = GetUserId() };
        dbContext.StudentMatches.Add(ignored);
        dbContext.StudentMatchAudits.Add(CreateAudit(examId, ignored.Id, null, null, null, opticalRecord.Id, "IgnoreOptical"));
        data.Exam.MatchingConfirmed = false;
        data.Exam.MatchingUpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(examId, cancellationToken);
    }

    public async Task<MatchingResponse?> ConfirmAsync(Guid examId, CancellationToken cancellationToken)
    {
        var response = await GetAsync(examId, cancellationToken);
        if (response is null) return null;
        if (!response.Summary.IsCalculated) throw new InvalidOperationException("Önce otomatik eşleştirme çalıştırılmalıdır.");
        if (!response.Summary.CanConfirm) throw new InvalidOperationException("Çakışan veya çözülmemiş öğrenci kayıtları varken eşleştirme onaylanamaz.");
        var exam = await dbContext.Exams.SingleAsync(item => item.Id == examId, cancellationToken);
        exam.MatchingConfirmed = true;
        exam.MatchingUpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetAsync(examId, cancellationToken);
    }

    private async Task<MatchingData?> GetDataAsync(Guid examId, CancellationToken cancellationToken)
    {
        var exam = await dbContext.Exams.SingleOrDefaultAsync(item => item.Id == examId, cancellationToken);
        if (exam is null || !await CanAccessCourseAsync(exam.CourseId, cancellationToken)) return null;
        var import = await dbContext.ExamImports.AsSplitQuery().Include(item => item.ImportedStudents).Include(item => item.OpticalExamRecords).Include(item => item.ValidationIssues).SingleOrDefaultAsync(item => item.ExamId == examId, cancellationToken);
        var matches = await dbContext.StudentMatches.Where(item => item.ExamId == examId).ToListAsync(cancellationToken);
        return new MatchingData(exam, import, matches);
    }

    private MatchingResponse BuildResponse(Exam exam, ExamImport? import, IReadOnlyCollection<StudentMatch> matches)
    {
        if (import is null) return new MatchingResponse(new MatchingSummaryResponse(0, 0, 0, 0, 0, 0, 0, 0, exam.MatchingCalculated, exam.MatchingConfirmed, false), [], exam.MatchingUpdatedAt);
        var classGroups = import.ImportedStudents.GroupBy(item => Normalize(item.StudentNumber), StringComparer.Ordinal).ToDictionary(item => item.Key, item => item.ToList(), StringComparer.Ordinal);
        var opticalGroups = import.OpticalExamRecords.Where(item => item.IsValid && !item.IsAnswerKey && !string.IsNullOrWhiteSpace(item.StudentNumber)).GroupBy(item => Normalize(item.StudentNumber), StringComparer.Ordinal).ToDictionary(item => item.Key, item => item.ToList(), StringComparer.Ordinal);
        var matchByStudent = matches.ToDictionary(item => item.ImportedStudentId);
        var matchByOptical = matches.Where(item => item.OpticalExamRecordId.HasValue).ToDictionary(item => item.OpticalExamRecordId!.Value);
        var records = new List<MatchingRecordResponse>();
        foreach (var student in import.ImportedStudents)
        {
            matchByStudent.TryGetValue(student.Id, out var match);
            var key = Normalize(student.StudentNumber);
            var conflict = classGroups[key].Count > 1 || opticalGroups.TryGetValue(key, out var opticalCandidates) && opticalCandidates.Count > 1;
            var optical = match?.OpticalExamRecordId is Guid opticalId ? import.OpticalExamRecords.SingleOrDefault(item => item.Id == opticalId) : null;
            var status = match?.Status ?? (conflict ? StudentMatchStatus.Conflict : StudentMatchStatus.Unmatched);
            var issue = match?.IsAbsent == true ? "Sınava girmedi olarak işaretlendi." : status == StudentMatchStatus.Conflict ? "Öğrenci numarası birden fazla kayıtla çakışıyor." : optical is null ? "Optik kaydı bulunamadı; öğrenci sınava girmemiş olabilir." : null;
            records.Add(new MatchingRecordResponse(match?.Id, student.Id, optical?.Id, student.StudentNumber, $"{student.FirstName} {student.LastName}".Trim(), optical?.StudentNumber ?? string.Empty, optical?.StudentName ?? string.Empty, optical?.BookletType ?? string.Empty, match?.MatchType ?? StudentMatchType.Manual, status, match?.IsAbsent == true, issue));
        }
        foreach (var optical in import.OpticalExamRecords.Where(item => item.IsValid && !item.IsAnswerKey && !matchByOptical.ContainsKey(item.Id)))
        {
            var key = Normalize(optical.StudentNumber);
            var conflict = !classGroups.ContainsKey(key) || opticalGroups[key].Count > 1;
            records.Add(new MatchingRecordResponse(null, null, optical.Id, string.Empty, string.Empty, optical.StudentNumber, optical.StudentName, optical.BookletType, StudentMatchType.Manual, conflict ? StudentMatchStatus.Conflict : StudentMatchStatus.Unmatched, false, conflict ? "Optik kaydı sınıf listesiyle güvenli biçimde eşleşmedi." : "Optik kaydı eşleşmedi."));
        }
        foreach (var optical in import.OpticalExamRecords.Where(item => !item.IsValid && !item.IsAnswerKey)) records.Add(new MatchingRecordResponse(null, null, optical.Id, string.Empty, string.Empty, optical.StudentNumber, optical.StudentName, optical.BookletType, StudentMatchType.Manual, StudentMatchStatus.Conflict, false, optical.ErrorMessage ?? "Optik kayıt ayrıştırılamadı."));
        var importConflictCount = import.ValidationIssues.Count(item => item.Source == "classList");
        var summary = new MatchingSummaryResponse(import.ImportedStudents.Count, import.OpticalExamRecords.Count(item => !item.IsAnswerKey), records.Count(item => item.Status == StudentMatchStatus.Matched && item.MatchType == StudentMatchType.Exact), records.Count(item => item.Status == StudentMatchStatus.Matched && item.MatchType == StudentMatchType.Normalized), records.Count(item => item.Status == StudentMatchStatus.Matched && item.MatchType == StudentMatchType.Manual), records.Count(item => item.ImportedStudentId.HasValue && item.Status == StudentMatchStatus.Unmatched), records.Count(item => !item.ImportedStudentId.HasValue && item.Status == StudentMatchStatus.Unmatched), records.Count(item => item.Status == StudentMatchStatus.Conflict) + importConflictCount, exam.MatchingCalculated, exam.MatchingConfirmed, exam.MatchingCalculated && importConflictCount == 0 && records.All(item => item.Status is StudentMatchStatus.Matched or StudentMatchStatus.Absent));
        return new MatchingResponse(summary, records, exam.MatchingUpdatedAt);
    }

    private async Task<bool> CanAccessCourseAsync(Guid courseId, CancellationToken cancellationToken)
    {
        var user = currentUser.Get();
        return user.IsAdministrator || user.Id.HasValue && await dbContext.CourseInstructors.AnyAsync(item => item.CourseId == courseId && item.UserId == user.Id.Value, cancellationToken);
    }

    private Guid GetUserId() => currentUser.Get().Id ?? throw new InvalidOperationException("Oturum kullanıcısı bulunamadı.");
    private static void EnsureMultipleChoice(Exam exam) { if (exam.EvaluationType != EvaluationType.MultipleChoice) throw new InvalidOperationException("Öğrenci eşleştirme yalnızca çoktan seçmeli sınavlarda kullanılabilir."); }
    private static string Normalize(string value) => string.Concat(value.Normalize(NormalizationForm.FormKC).Where(character => !char.IsWhiteSpace(character) && CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.Format));
    private StudentMatchAudit CreateAudit(Guid examId, Guid? matchId, Guid? previousStudentId, Guid? previousOpticalId, Guid? newStudentId, Guid? newOpticalId, string action) => new() { Id = Guid.NewGuid(), ExamId = examId, StudentMatchId = matchId, PreviousImportedStudentId = previousStudentId, PreviousOpticalExamRecordId = previousOpticalId, NewImportedStudentId = newStudentId, NewOpticalExamRecordId = newOpticalId, UserId = GetUserId(), OccurredAt = DateTime.UtcNow, Action = action };
    private sealed record MatchingData(Exam Exam, ExamImport? Import, IReadOnlyCollection<StudentMatch> Matches);
}
