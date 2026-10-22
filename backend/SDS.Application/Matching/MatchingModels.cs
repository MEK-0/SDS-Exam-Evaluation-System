// Öğrenci eşleştirme kullanım senaryosunun istek ve yanıt modellerini tanımlar.
using SDS.Domain.Entities;

namespace SDS.Application.Matching;

public sealed record MatchingSummaryResponse(int TotalClassListCount, int TotalOpticalRecordCount, int ExactMatchCount, int NormalizedMatchCount, int ManualMatchCount, int UnmatchedStudentCount, int UnmatchedOpticalRecordCount, int ConflictCount, bool IsCalculated, bool IsConfirmed, bool CanConfirm);
public sealed record MatchingRecordResponse(Guid? MatchId, Guid? ImportedStudentId, Guid? OpticalExamRecordId, string StudentNumber, string StudentName, string OpticalStudentNumber, string OpticalStudentName, string BookletType, StudentMatchType MatchType, StudentMatchStatus Status, bool IsAbsent, string? Issue);
public sealed record MatchingResponse(MatchingSummaryResponse Summary, IReadOnlyCollection<MatchingRecordResponse> Records, DateTime? UpdatedAt);

public sealed class ManualMatchRequest
{
    public Guid ImportedStudentId { get; set; }
    public Guid OpticalExamRecordId { get; set; }
}

public sealed class MarkAbsentRequest
{
    public Guid ImportedStudentId { get; set; }
}

public sealed class IgnoreOpticalRequest
{
    public Guid OpticalExamRecordId { get; set; }
}

public interface IStudentMatchingService
{
    Task<MatchingResponse?> RunAsync(Guid examId, CancellationToken cancellationToken);
    Task<MatchingResponse?> GetAsync(Guid examId, CancellationToken cancellationToken);
    Task<MatchingResponse?> CreateManualAsync(Guid examId, ManualMatchRequest request, CancellationToken cancellationToken);
    Task<MatchingResponse?> MarkAbsentAsync(Guid examId, MarkAbsentRequest request, CancellationToken cancellationToken);
    Task<MatchingResponse?> IgnoreOpticalAsync(Guid examId, IgnoreOpticalRequest request, CancellationToken cancellationToken);
    Task<MatchingResponse?> RemoveManualAsync(Guid examId, Guid matchId, CancellationToken cancellationToken);
    Task<MatchingResponse?> ConfirmAsync(Guid examId, CancellationToken cancellationToken);
}
