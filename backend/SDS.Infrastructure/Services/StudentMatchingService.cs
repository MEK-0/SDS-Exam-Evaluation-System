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
        var manualStudentIds = manualMatches.Where(item => item.ImportedStudentId.HasValue).Select(item => item.ImportedStudentId!.Value).ToHashSet();
        var manualOpticalIds = manualMatches.Where(item => item.OpticalExamRecordId.HasValue).Select(item => item.OpticalExamRecordId!.Value).ToHashSet();
        var automaticMatches = classGroups.Where(item => IsDigitsOnly(item.Key) && item.Value.Count == 1 && opticalGroups.TryGetValue(item.Key, out var records) && records.Count == 1).Select(item => new { Student = item.Value[0], Record = opticalGroups[item.Key][0] }).Where(item => !manualStudentIds.Contains(item.Student.Id) && !manualOpticalIds.Contains(item.Record.Id)).Select(item => new StudentMatch { Id = Guid.NewGuid(), ExamId = examId, ImportedStudentId = item.Student.Id, OpticalExamRecordId = item.Record.Id, MatchType = string.Equals(item.Student.StudentNumber, item.Record.StudentNumber, StringComparison.Ordinal) ? StudentMatchType.Exact : StudentMatchType.Normalized, Status = StudentMatchStatus.Matched, MatchedAt = DateTime.UtcNow, MatchedByUserId = GetUserId() }).ToList();
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
        var ignored = new StudentMatch { Id = Guid.NewGuid(), ExamId = examId, ImportedStudentId = null, OpticalExamRecordId = opticalRecord.Id, MatchType = StudentMatchType.Manual, Status = StudentMatchStatus.Absent, IsAbsent = true, MatchedAt = DateTime.UtcNow, MatchedByUserId = GetUserId() };
        dbContext.StudentMatches.Add(ignored);
        dbContext.StudentMatchAudits.Add(CreateAudit(examId, ignored.Id, null, null, null, opticalRecord.Id, "IgnoreOptical"));
        data.Exam.MatchingConfirmed = false;
        data.Exam.MatchingUpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await GetAsync(examId, cancellationToken);
    }

    public async Task<MatchingResponse?> AcceptSuggestionAsync(Guid examId, AcceptSuggestionRequest request, CancellationToken cancellationToken)
    {
        var data = await GetDataAsync(examId, cancellationToken);
        if (data is null) return null;
        EnsureMultipleChoice(data.Exam);
        if (data.Import is null) throw new InvalidOperationException("Eşleştirme için önce dosya import işlemi tamamlanmalıdır.");
        var current = BuildResponse(data.Exam, data.Import, data.Matches);
        var isSuggested = current.Suggestions.Any(item => item.Kind == MatchSuggestionKind.SimilarNumber && item.ImportedStudentId == request.ImportedStudentId && item.OpticalExamRecordId == request.OpticalExamRecordId);
        if (!isSuggested) throw new InvalidOperationException("Bu eşleştirme önerisi artık geçerli değil. Eşleştirmeyi yenileyin.");
        return await CreateManualAsync(examId, new ManualMatchRequest { ImportedStudentId = request.ImportedStudentId, OpticalExamRecordId = request.OpticalExamRecordId }, cancellationToken);
    }

    public async Task<MatchingResponse?> AcceptAbsentSuggestionsAsync(Guid examId, CancellationToken cancellationToken)
    {
        var data = await GetDataAsync(examId, cancellationToken);
        if (data is null) return null;
        EnsureMultipleChoice(data.Exam);
        if (data.Import is null) throw new InvalidOperationException("Eşleştirme için önce dosya import işlemi tamamlanmalıdır.");
        if (!data.Exam.MatchingCalculated) throw new InvalidOperationException("Önce otomatik eşleştirme çalıştırılmalıdır.");
        var absentSuggestions = BuildResponse(data.Exam, data.Import, data.Matches).Suggestions.Where(item => item.Kind == MatchSuggestionKind.Absent).ToList();
        if (absentSuggestions.Count == 0) return BuildResponse(data.Exam, data.Import, data.Matches);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        foreach (var suggestion in absentSuggestions)
        {
            var absent = new StudentMatch { Id = Guid.NewGuid(), ExamId = examId, ImportedStudentId = suggestion.ImportedStudentId, MatchType = StudentMatchType.Manual, Status = StudentMatchStatus.Absent, IsAbsent = true, MatchedAt = DateTime.UtcNow, MatchedByUserId = GetUserId() };
            dbContext.StudentMatches.Add(absent);
            dbContext.StudentMatchAudits.Add(CreateAudit(examId, absent.Id, suggestion.ImportedStudentId, null, suggestion.ImportedStudentId, null, "MarkAbsent"));
        }
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
        if (import is null) return new MatchingResponse(new MatchingSummaryResponse(0, 0, 0, 0, 0, 0, 0, 0, exam.MatchingCalculated, exam.MatchingConfirmed, false), [], exam.MatchingUpdatedAt, []);
        var classGroups = import.ImportedStudents.GroupBy(item => Normalize(item.StudentNumber), StringComparer.Ordinal).ToDictionary(item => item.Key, item => item.ToList(), StringComparer.Ordinal);
        var opticalGroups = GroupOpticalRecords(import);
        var matchByStudent = matches.Where(item => item.ImportedStudentId.HasValue).ToDictionary(item => item.ImportedStudentId!.Value);
        var matchByOptical = matches.Where(item => item.OpticalExamRecordId.HasValue).ToDictionary(item => item.OpticalExamRecordId!.Value);
        var suggestions = exam.MatchingCalculated ? BuildSuggestions(import, matches, classGroups, opticalGroups) : [];
        var records = new List<MatchingRecordResponse>();
        foreach (var student in import.ImportedStudents)
        {
            matchByStudent.TryGetValue(student.Id, out var match);
            var key = Normalize(student.StudentNumber);
            var conflict = classGroups[key].Count > 1 || opticalGroups.TryGetValue(key, out var opticalCandidates) && opticalCandidates.Count > 1;
            var optical = match?.OpticalExamRecordId is Guid opticalId ? import.OpticalExamRecords.SingleOrDefault(item => item.Id == opticalId) : null;
            var status = match?.Status ?? (conflict ? StudentMatchStatus.Conflict : StudentMatchStatus.Unmatched);
            var suggestion = suggestions.FirstOrDefault(item => item.ImportedStudentId == student.Id);
            var issue = match?.IsAbsent == true ? "Sınava girmedi olarak işaretlendi." : status == StudentMatchStatus.Conflict ? "Öğrenci numarası birden fazla kayıtla çakışıyor." : suggestion is not null ? suggestion.Reason : optical is null ? "Optik kaydı bulunamadı; öğrenci sınava girmemiş olabilir." : null;
            records.Add(new MatchingRecordResponse(match?.Id, student.Id, optical?.Id, student.StudentNumber, $"{student.FirstName} {student.LastName}".Trim(), optical?.StudentNumber ?? string.Empty, optical?.StudentName ?? string.Empty, optical?.BookletType ?? string.Empty, match?.MatchType ?? StudentMatchType.Manual, status, match?.IsAbsent == true, issue));
        }
        foreach (var optical in import.OpticalExamRecords.Where(item => item.IsValid && !item.IsAnswerKey))
        {
            if (matchByOptical.TryGetValue(optical.Id, out var ignoredMatch))
            {
                if (ignoredMatch.ImportedStudentId.HasValue) continue;
                records.Add(new MatchingRecordResponse(ignoredMatch.Id, null, optical.Id, string.Empty, string.Empty, optical.StudentNumber, optical.StudentName, optical.BookletType, StudentMatchType.Manual, StudentMatchStatus.Absent, true, "Optik kaydı eşleştirme dışında bırakıldı."));
                continue;
            }
            var key = Normalize(optical.StudentNumber);
            var conflict = !classGroups.ContainsKey(key) || opticalGroups[key].Count > 1;
            var suggestion = suggestions.FirstOrDefault(item => item.OpticalExamRecordId == optical.Id);
            var issue = suggestion is not null ? suggestion.Reason : conflict ? "Optik kaydı sınıf listesiyle güvenli biçimde eşleşmedi." : "Optik kaydı eşleşmedi.";
            records.Add(new MatchingRecordResponse(null, null, optical.Id, string.Empty, string.Empty, optical.StudentNumber, optical.StudentName, optical.BookletType, StudentMatchType.Manual, conflict ? StudentMatchStatus.Conflict : StudentMatchStatus.Unmatched, false, issue));
        }
        foreach (var optical in import.OpticalExamRecords.Where(item => !item.IsValid && !item.IsAnswerKey)) records.Add(new MatchingRecordResponse(null, null, optical.Id, string.Empty, string.Empty, optical.StudentNumber, optical.StudentName, optical.BookletType, StudentMatchType.Manual, StudentMatchStatus.Conflict, false, optical.ErrorMessage ?? "Optik kayıt ayrıştırılamadı.", true));
        var importConflictCount = import.ValidationIssues.Count(item => item.Source == "classList");
        var blockingRecords = records.Where(item => !item.IsInvalidOptical).ToList();
        var summary = new MatchingSummaryResponse(import.ImportedStudents.Count, import.OpticalExamRecords.Count(item => !item.IsAnswerKey), records.Count(item => item.Status == StudentMatchStatus.Matched && item.MatchType == StudentMatchType.Exact), records.Count(item => item.Status == StudentMatchStatus.Matched && item.MatchType == StudentMatchType.Normalized), records.Count(item => item.Status == StudentMatchStatus.Matched && item.MatchType == StudentMatchType.Manual), records.Count(item => item.ImportedStudentId.HasValue && item.Status == StudentMatchStatus.Unmatched), records.Count(item => !item.ImportedStudentId.HasValue && item.Status == StudentMatchStatus.Unmatched), blockingRecords.Count(item => item.Status == StudentMatchStatus.Conflict) + importConflictCount, exam.MatchingCalculated, exam.MatchingConfirmed, exam.MatchingCalculated && importConflictCount == 0 && blockingRecords.All(item => item.Status is StudentMatchStatus.Matched or StudentMatchStatus.Absent), suggestions.Count(item => item.Kind == MatchSuggestionKind.SimilarNumber), suggestions.Count(item => item.Kind == MatchSuggestionKind.Absent), records.Count(item => item.IsInvalidOptical));
        return new MatchingResponse(summary, records, exam.MatchingUpdatedAt, suggestions);
    }

    private static Dictionary<string, List<OpticalExamRecord>> GroupOpticalRecords(ExamImport import)
    {
        return import.OpticalExamRecords
            .Where(item => item.IsValid && !item.IsAnswerKey && !string.IsNullOrWhiteSpace(item.StudentNumber))
            .GroupBy(item => Normalize(item.StudentNumber), StringComparer.Ordinal)
            .ToDictionary(item => item.Key, item => item.ToList(), StringComparer.Ordinal);
    }

    private static List<MatchSuggestionResponse> BuildSuggestions(ExamImport import, IReadOnlyCollection<StudentMatch> matches, Dictionary<string, List<ImportedStudent>> classGroups, Dictionary<string, List<OpticalExamRecord>> opticalGroups)
    {
        var matchedStudentIds = matches.Where(item => item.ImportedStudentId.HasValue).Select(item => item.ImportedStudentId!.Value).ToHashSet();
        var matchedOpticalIds = matches.Where(item => item.OpticalExamRecordId.HasValue).Select(item => item.OpticalExamRecordId!.Value).ToHashSet();
        var students = import.ImportedStudents
            .Where(item => !matchedStudentIds.Contains(item.Id))
            .Where(item => classGroups[Normalize(item.StudentNumber)].Count == 1)
            .Where(item => !opticalGroups.TryGetValue(Normalize(item.StudentNumber), out var group) || group.Count == 0)
            .ToList();
        var records = import.OpticalExamRecords
            .Where(item => item.IsValid && !item.IsAnswerKey && !string.IsNullOrWhiteSpace(item.StudentNumber))
            .Where(item => !matchedOpticalIds.Contains(item.Id))
            .Where(item => !classGroups.ContainsKey(Normalize(item.StudentNumber)) && opticalGroups[Normalize(item.StudentNumber)].Count == 1)
            .ToList();
        var candidates = new List<(ImportedStudent Student, OpticalExamRecord Record)>();
        foreach (var student in students)
        {
            foreach (var record in records)
            {
                if (IsOneCharacterApart(Normalize(student.StudentNumber), Normalize(record.StudentNumber)) && NamesMatch($"{student.FirstName} {student.LastName}", record.StudentName)) candidates.Add((student, record));
            }
        }
        var suggestions = new List<MatchSuggestionResponse>();
        foreach (var candidate in candidates)
        {
            var isUnique = candidates.Count(item => item.Student.Id == candidate.Student.Id) == 1 && candidates.Count(item => item.Record.Id == candidate.Record.Id) == 1;
            if (!isUnique) continue;
            var reason = HasOnlyDigits(candidate.Record.StudentNumber.Trim()) ? "Numarası tek hane farklı, adı aynı olan optik kaydı bulundu." : "Optik numarasında boş veya geçersiz hane var; adı aynı olan öğrenci bulundu.";
            suggestions.Add(new MatchSuggestionResponse(MatchSuggestionKind.SimilarNumber, candidate.Student.Id, candidate.Record.Id, candidate.Student.StudentNumber, $"{candidate.Student.FirstName} {candidate.Student.LastName}".Trim(), candidate.Record.StudentNumber, candidate.Record.StudentName, reason));
        }
        var withCandidates = candidates.Select(item => item.Student.Id).ToHashSet();
        foreach (var student in students.Where(item => !withCandidates.Contains(item.Id)))
        {
            suggestions.Add(new MatchSuggestionResponse(MatchSuggestionKind.Absent, student.Id, null, student.StudentNumber, $"{student.FirstName} {student.LastName}".Trim(), string.Empty, string.Empty, "Optik kaydı bulunamadı; öğrenci sınava girmemiş olabilir."));
        }
        return suggestions;
    }

    private static bool IsOneCharacterApart(string first, string second)
    {
        if (first.Length != second.Length || first.Length == 0) return false;
        var differences = 0;
        for (var index = 0; index < first.Length; index++)
        {
            if (first[index] != second[index]) differences++;
        }
        return differences == 1;
    }

    private static bool NamesMatch(string studentName, string opticalName)
    {
        var student = FoldName(studentName);
        var optical = FoldName(opticalName);
        if (optical.Length == 0 || student.Length == 0) return false;
        if (student.OrderBy(item => item, StringComparer.Ordinal).SequenceEqual(optical.OrderBy(item => item, StringComparer.Ordinal))) return true;
        var studentText = string.Join(' ', student);
        var opticalText = string.Join(' ', optical);
        return opticalText.Length >= 20 && studentText.StartsWith(opticalText, StringComparison.Ordinal);
    }

    private static string[] FoldName(string value)
    {
        var upper = value.ToUpper(new CultureInfo("tr-TR"));
        var folded = new StringBuilder();
        foreach (var character in upper)
        {
            folded.Append(character switch
            {
                'İ' or 'I' => 'I',
                'Ş' => 'S',
                'Ğ' => 'G',
                'Ü' => 'U',
                'Ö' => 'O',
                'Ç' => 'C',
                _ when char.IsLetterOrDigit(character) => character,
                _ => ' '
            });
        }
        return folded.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    private static bool IsDigitsOnly(string value) => value.Length > 0 && value.All(char.IsAsciiDigit);
    private static bool HasOnlyDigits(string value) => IsDigitsOnly(value);

    private async Task<bool> CanAccessCourseAsync(Guid courseId, CancellationToken cancellationToken)
    {
        var user = currentUser.Get();
        return user.IsAdministrator || user.Id.HasValue && await dbContext.CourseInstructors.AnyAsync(item => item.CourseId == courseId && item.UserId == user.Id.Value, cancellationToken);
    }

    private Guid GetUserId() => currentUser.Get().Id ?? throw new InvalidOperationException("Oturum kullanıcısı bulunamadı.");
    private static void EnsureMultipleChoice(Exam exam) { if (exam.EvaluationType != EvaluationType.MultipleChoice) throw new InvalidOperationException("Öğrenci eşleştirme yalnızca çoktan seçmeli sınavlarda kullanılabilir."); }
    private static string Normalize(string value) => string.Concat(value.Normalize(NormalizationForm.FormKC).Where(character => CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.Format)).Trim();
    private StudentMatchAudit CreateAudit(Guid examId, Guid? matchId, Guid? previousStudentId, Guid? previousOpticalId, Guid? newStudentId, Guid? newOpticalId, string action) => new() { Id = Guid.NewGuid(), ExamId = examId, StudentMatchId = matchId, PreviousImportedStudentId = previousStudentId, PreviousOpticalExamRecordId = previousOpticalId, NewImportedStudentId = newStudentId, NewOpticalExamRecordId = newOpticalId, UserId = GetUserId(), OccurredAt = DateTime.UtcNow, Action = action };
    private sealed record MatchingData(Exam Exam, ExamImport? Import, IReadOnlyCollection<StudentMatch> Matches);
}
