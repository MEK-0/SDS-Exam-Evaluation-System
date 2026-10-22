// Sınav öğrencisinin ham ve düzeltilmiş puan sonucunu tanımlar.
namespace SDS.Domain.Entities;

public sealed class ExamResult
{
    public Guid Id { get; set; }
    public Guid ExamId { get; set; }
    public Guid ImportedStudentId { get; set; }
    public decimal? RawScore { get; set; }
    public decimal? AdjustedScore { get; set; }
    public GradeStatus Status { get; set; }
    public DateTime CalculatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Exam Exam { get; set; } = null!;
    public ImportedStudent ImportedStudent { get; set; } = null!;
    public ICollection<GradeAdjustment> Adjustments { get; set; } = new List<GradeAdjustment>();
}

public enum GradeStatus
{
    Graded,
    Absent,
    NotEntered
}
