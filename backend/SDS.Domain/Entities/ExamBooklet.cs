// Sınav cevap anahtarındaki kitapçık bilgilerini tanımlar.
namespace SDS.Domain.Entities;

public sealed class ExamBooklet
{
    public Guid Id { get; set; }
    public Guid ExamAnswerKeyId { get; set; }
    public string BookletCode { get; set; } = string.Empty;
    public int QuestionCount { get; set; }
    public ExamAnswerKey ExamAnswerKey { get; set; } = null!;
    public ICollection<BookletQuestion> Questions { get; set; } = new List<BookletQuestion>();
    public ICollection<BookletQuestionMapping> ReferenceMappings { get; set; } = new List<BookletQuestionMapping>();
    public ICollection<BookletQuestionMapping> Mappings { get; set; } = new List<BookletQuestionMapping>();
}
