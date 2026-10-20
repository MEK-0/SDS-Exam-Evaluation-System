// Sınav istek ve yanıt modellerini tanımlar.
using System.ComponentModel.DataAnnotations;
using SDS.Domain.Entities;

namespace SDS.Application.Exams;

public sealed class CreateExamRequest
{
    [Required]
    public ExamType ExamType { get; set; }

    [Required]
    public EvaluationType EvaluationType { get; set; }
}

public sealed record ExamResponse(Guid Id, Guid CourseId, ExamType ExamType, EvaluationType EvaluationType, ExamStatus Status, DateTime CreatedAt);
