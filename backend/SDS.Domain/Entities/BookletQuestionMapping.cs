// Kitapçık sorularının referans kitapçık sorularına eşleştirilmesini tanımlar.
namespace SDS.Domain.Entities;

public sealed class BookletQuestionMapping
{
    public Guid Id { get; set; }
    public Guid ExamId { get; set; }
    public Guid ReferenceBookletId { get; set; }
    public Guid BookletId { get; set; }
    public int ReferenceQuestionNumber { get; set; }
    public int BookletQuestionNumber { get; set; }
    public Exam Exam { get; set; } = null!;
    public ExamBooklet ReferenceBooklet { get; set; } = null!;
    public ExamBooklet Booklet { get; set; } = null!;
}
