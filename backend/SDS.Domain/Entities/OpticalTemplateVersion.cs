// Optik şablonun değişmez bir sürümünü: alan konumları, işaret karakterleri ve kodlama modunu tanımlar.
namespace SDS.Domain.Entities;

public sealed class OpticalTemplateVersion
{
    public Guid Id { get; set; }
    public Guid OpticalTemplateId { get; set; }
    public int VersionNo { get; set; }
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
    public char BlankMarkChar { get; set; } = ' ';
    public char DoubleMarkChar { get; set; } = '*';
    public OpticalEncodingMode EncodingMode { get; set; } = OpticalEncodingMode.Auto;
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public OpticalTemplate OpticalTemplate { get; set; } = null!;
}

public enum OpticalEncodingMode
{
    Auto,
    Utf8,
    Windows1254
}
