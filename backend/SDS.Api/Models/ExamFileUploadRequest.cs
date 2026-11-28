// Sınav dosyası yükleme form alanlarını taşır.
using Microsoft.AspNetCore.Http;

namespace SDS.Api.Models;

public sealed class ExamFileUploadRequest
{
    public IFormFile? OpticalFile { get; set; }
    public List<IFormFile> ClassListFiles { get; set; } = [];
    public IFormFile? ClassListFile { get; set; }
}
