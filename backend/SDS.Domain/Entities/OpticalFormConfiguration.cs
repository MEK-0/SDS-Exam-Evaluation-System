// Optik form alanlarının sabit genişlikli dosyadaki konumlarını tanımlar.
namespace SDS.Domain.Entities;

public sealed class OpticalFormConfiguration
{
    public Guid Id { get; set; }
    public Guid ExamId { get; set; }
    public int StudentNumberStart { get; set; }
    public int StudentNumberLength { get; set; }
    public int? StudentNameStart { get; set; }
    public int? StudentNameLength { get; set; }
    public int BookletTypeStart { get; set; }
    public int BookletTypeLength { get; set; }
    public int? ExamTypeStart { get; set; }
    public int? ExamTypeLength { get; set; }
    public int AnswersStart { get; set; }
    public int AnswersLength { get; set; }
    public string EncodingName { get; set; } = "utf-8";
    public Exam Exam { get; set; } = null!;
}
