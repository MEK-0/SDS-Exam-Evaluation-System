// Ders bazında yeniden kullanılabilen öğrenme çıktısını tanımlar.
namespace SDS.Domain.Entities;

public sealed class LearningOutcome
{
    public Guid Id { get; set; }
    public Guid CourseId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Course Course { get; set; } = null!;
    public ICollection<QuestionLearningOutcome> QuestionLinks { get; set; } = new List<QuestionLearningOutcome>();
}
