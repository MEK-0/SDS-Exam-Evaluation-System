// Optik şablon istek/yanıt modellerini ve şablon servis arayüzünü tanımlar.
using System.ComponentModel.DataAnnotations;
using SDS.Application.Exams;
using SDS.Domain.Entities;

namespace SDS.Application.OpticalTemplates;

public sealed class SaveOpticalTemplateRequest
{
    [Required, MaxLength(160)] public string Name { get; set; } = string.Empty;
    [Range(0, int.MaxValue)] public int StudentNumberStart { get; set; }
    [Range(1, int.MaxValue)] public int StudentNumberLength { get; set; }
    [Range(0, int.MaxValue)] public int? StudentNameStart { get; set; }
    [Range(1, int.MaxValue)] public int? StudentNameLength { get; set; }
    [Range(0, int.MaxValue)] public int BookletTypeStart { get; set; }
    [Range(1, int.MaxValue)] public int BookletTypeLength { get; set; }
    [Range(0, int.MaxValue)] public int? ExamTypeStart { get; set; }
    [Range(1, int.MaxValue)] public int? ExamTypeLength { get; set; }
    [Range(0, int.MaxValue)] public int AnswersStart { get; set; }
    [Range(1, int.MaxValue)] public int AnswersLength { get; set; }
    public char BlankMarkChar { get; set; } = ' ';
    public char DoubleMarkChar { get; set; } = '*';
    public OpticalEncodingMode EncodingMode { get; set; } = OpticalEncodingMode.Auto;
    public bool IsDefault { get; set; }
}

public sealed class BindExamTemplateRequest
{
    [Required] public Guid? TemplateId { get; set; }
}

public sealed record OpticalTemplateVersionResponse(
    Guid Id,
    int VersionNo,
    int StudentNumberStart,
    int StudentNumberLength,
    int? StudentNameStart,
    int? StudentNameLength,
    int BookletTypeStart,
    int BookletTypeLength,
    int? ExamTypeStart,
    int? ExamTypeLength,
    int AnswersStart,
    int AnswersLength,
    char BlankMarkChar,
    char DoubleMarkChar,
    OpticalEncodingMode EncodingMode,
    DateTime CreatedAt);

public sealed record OpticalTemplateResponse(Guid Id, string Name, bool IsDefault, int VersionCount, OpticalTemplateVersionResponse CurrentVersion, DateTime CreatedAt);

public interface IOpticalTemplateService
{
    Task<IReadOnlyCollection<OpticalTemplateResponse>> GetAllAsync(CancellationToken cancellationToken);
    Task<OpticalTemplateResponse> CreateAsync(SaveOpticalTemplateRequest request, CancellationToken cancellationToken);
    Task<OpticalTemplateResponse?> UpdateAsync(Guid templateId, SaveOpticalTemplateRequest request, CancellationToken cancellationToken);
    Task<OpticalTemplateResponse?> SetDefaultAsync(Guid templateId, CancellationToken cancellationToken);
    Task<ExamResponse?> BindToExamAsync(Guid examId, Guid templateId, CancellationToken cancellationToken);
}
