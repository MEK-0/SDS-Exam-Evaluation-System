// Sınav içindeki öğrenci ve optik kayıt eşleştirmesini tanımlar.
namespace SDS.Domain.Entities;

public sealed class StudentMatch
{
    public Guid Id { get; set; }
    public Guid ExamId { get; set; }
    public Guid ImportedStudentId { get; set; }
    public Guid? OpticalExamRecordId { get; set; }
    public StudentMatchType MatchType { get; set; }
    public StudentMatchStatus Status { get; set; }
    public bool IsAbsent { get; set; }
    public DateTime MatchedAt { get; set; }
    public Guid MatchedByUserId { get; set; }
    public Exam Exam { get; set; } = null!;
    public ImportedStudent ImportedStudent { get; set; } = null!;
    public OpticalExamRecord? OpticalExamRecord { get; set; }
    public User MatchedByUser { get; set; } = null!;
    public ICollection<StudentMatchAudit> Audits { get; set; } = new List<StudentMatchAudit>();
}

public enum StudentMatchType
{
    Exact,
    Normalized,
    Manual
}

public enum StudentMatchStatus
{
    Matched,
    Unmatched,
    Conflict,
    Absent
}
