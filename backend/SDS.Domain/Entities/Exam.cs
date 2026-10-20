// SDS sınav varlığını ve sınav türlerini tanımlar.
namespace SDS.Domain.Entities;

public sealed class Exam
{
    public Guid Id { get; set; }
    public Guid CourseId { get; set; }
    public ExamType ExamType { get; set; }
    public EvaluationType EvaluationType { get; set; }
    public ExamStatus Status { get; set; } = ExamStatus.Draft;
    public DateTime CreatedAt { get; set; }
    public Course Course { get; set; } = null!;
    public OpticalFormConfiguration? OpticalFormConfiguration { get; set; }
    public ICollection<ExamImport> Imports { get; set; } = new List<ExamImport>();
    public ExamAnswerKey? AnswerKey { get; set; }
}

public enum ExamType
{
    Midterm,
    Final,
    MakeUp
}

public enum EvaluationType
{
    MultipleChoice,
    OpenEnded
}

public enum ExamStatus
{
    Draft
}
