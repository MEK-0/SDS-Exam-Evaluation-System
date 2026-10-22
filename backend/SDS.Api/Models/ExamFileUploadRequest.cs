// Sınav dosyası yükleme form alanlarını taşır.
using Microsoft.AspNetCore.Http;

namespace SDS.Api.Models;

public sealed class ExamFileUploadRequest
{
    public IFormFile? OpticalFile { get; set; }
    public List<IFormFile> ClassListFiles { get; set; } = [];
    public IFormFile? ClassListFile { get; set; }
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
}
