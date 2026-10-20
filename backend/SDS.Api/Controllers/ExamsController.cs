// Sınav taslağı endpointlerini sunar.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SDS.Api.Models;
using SDS.Application.AnswerKeys;
using SDS.Application.Common;
using SDS.Application.Exams;
using SDS.Application.Imports;

namespace SDS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public sealed class ExamsController(IExamService examService, IStudentDataImportService importService, IAnswerKeyService answerKeyService) : ControllerBase
{
    [HttpGet("courses/{courseId:guid}/exams")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<ExamResponse>>>> GetCourseExams(Guid courseId, CancellationToken cancellationToken)
    {
        var response = await examService.GetCourseExamsAsync(courseId, cancellationToken);
        return response is null ? NotFound(ApiResponse<IReadOnlyCollection<ExamResponse>>.Fail("Ders bulunamadı veya erişim yetkiniz yok.")) : Ok(ApiResponse<IReadOnlyCollection<ExamResponse>>.Ok(response));
    }

    [HttpPost("courses/{courseId:guid}/exams")]
    public async Task<ActionResult<ApiResponse<ExamResponse>>> CreateExam(Guid courseId, CreateExamRequest request, CancellationToken cancellationToken)
    {
        var response = await examService.CreateExamAsync(courseId, request, cancellationToken);
        return response is null ? NotFound(ApiResponse<ExamResponse>.Fail("Ders bulunamadı veya erişim yetkiniz yok.")) : Ok(ApiResponse<ExamResponse>.Ok(response, "Sınav taslağı oluşturuldu."));
    }

    [HttpGet("exams/{id:guid}")]
    public async Task<ActionResult<ApiResponse<ExamResponse>>> GetExam(Guid id, CancellationToken cancellationToken)
    {
        var response = await examService.GetExamAsync(id, cancellationToken);
        return response is null ? NotFound(ApiResponse<ExamResponse>.Fail("Sınav bulunamadı veya erişim yetkiniz yok.")) : Ok(ApiResponse<ExamResponse>.Ok(response));
    }

    [HttpPost("exams/{examId:guid}/files")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(20 * 1024 * 1024)]
    public async Task<ActionResult<ApiResponse<ImportPreviewResponse>>> UploadFiles(Guid examId, [FromForm] ExamFileUploadRequest request, CancellationToken cancellationToken)
    {
        ValidateFile(request.OpticalFile, ".txt", "Optik dosyası");
        ValidateFile(request.ClassListFile, ".xlsx", "Sınıf listesi");
        await using var opticalStream = request.OpticalFile!.OpenReadStream();
        await using var classListStream = request.ClassListFile!.OpenReadStream();
        var configuration = new OpticalFormConfigurationInput(request.StudentNumberStart, request.StudentNumberLength, request.StudentNameStart, request.StudentNameLength, request.BookletTypeStart, request.BookletTypeLength, request.ExamTypeStart, request.ExamTypeLength, request.AnswersStart, request.AnswersLength, request.EncodingName);
        var response = await importService.ImportAsync(examId, opticalStream, request.OpticalFile.FileName, classListStream, request.ClassListFile.FileName, configuration, cancellationToken);
        return Ok(ApiResponse<ImportPreviewResponse>.Ok(response, "Dosyalar işlendi."));
    }

    [HttpGet("exams/{examId:guid}/import-preview")]
    public async Task<ActionResult<ApiResponse<ImportPreviewResponse>>> GetImportPreview(Guid examId, CancellationToken cancellationToken)
    {
        var response = await importService.GetPreviewAsync(examId, cancellationToken);
        return response is null ? NotFound(ApiResponse<ImportPreviewResponse>.Fail("Import kaydı bulunamadı veya erişim yetkiniz yok.")) : Ok(ApiResponse<ImportPreviewResponse>.Ok(response));
    }

    [HttpGet("exams/{examId:guid}/import-issues")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<ImportIssue>>>> GetImportIssues(Guid examId, CancellationToken cancellationToken)
    {
        var response = await importService.GetIssuesAsync(examId, cancellationToken);
        return response is null ? NotFound(ApiResponse<IReadOnlyCollection<ImportIssue>>.Fail("Import kaydı bulunamadı veya erişim yetkiniz yok.")) : Ok(ApiResponse<IReadOnlyCollection<ImportIssue>>.Ok(response));
    }

    [HttpGet("exams/{examId:guid}/answer-key")]
    public async Task<ActionResult<ApiResponse<AnswerKeyResponse>>> GetAnswerKey(Guid examId, CancellationToken cancellationToken)
    {
        var response = await answerKeyService.GetAsync(examId, cancellationToken);
        return response is null ? NotFound(ApiResponse<AnswerKeyResponse>.Fail("Sınav bulunamadı veya erişim yetkiniz yok.")) : Ok(ApiResponse<AnswerKeyResponse>.Ok(response));
    }

    [HttpPut("exams/{examId:guid}/answer-key")]
    public async Task<ActionResult<ApiResponse<AnswerKeyResponse>>> UpdateAnswerKey(Guid examId, UpdateAnswerKeyRequest request, CancellationToken cancellationToken)
    {
        var response = await answerKeyService.UpdateAsync(examId, request, cancellationToken);
        return response is null ? NotFound(ApiResponse<AnswerKeyResponse>.Fail("Sınav bulunamadı veya erişim yetkiniz yok.")) : Ok(ApiResponse<AnswerKeyResponse>.Ok(response, response.IsComplete ? "Cevap anahtarı doğrulandı." : "Cevap anahtarı taslağı kaydedildi."));
    }

    [HttpGet("exams/{examId:guid}/booklets")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<AnswerKeyBookletResponse>>>> GetBooklets(Guid examId, CancellationToken cancellationToken)
    {
        var response = await answerKeyService.GetAsync(examId, cancellationToken);
        return response is null ? NotFound(ApiResponse<IReadOnlyCollection<AnswerKeyBookletResponse>>.Fail("Sınav bulunamadı veya erişim yetkiniz yok.")) : Ok(ApiResponse<IReadOnlyCollection<AnswerKeyBookletResponse>>.Ok(response.Booklets));
    }

    [HttpPut("exams/{examId:guid}/booklets")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<AnswerKeyBookletResponse>>>> UpdateBooklets(Guid examId, UpdateAnswerKeyRequest request, CancellationToken cancellationToken)
    {
        var response = await answerKeyService.UpdateAsync(examId, request, cancellationToken);
        return response is null ? NotFound(ApiResponse<IReadOnlyCollection<AnswerKeyBookletResponse>>.Fail("Sınav bulunamadı veya erişim yetkiniz yok.")) : Ok(ApiResponse<IReadOnlyCollection<AnswerKeyBookletResponse>>.Ok(response.Booklets, response.IsComplete ? "Kitapçıklar doğrulandı." : "Kitapçık taslağı kaydedildi."));
    }

    [HttpGet("exams/{examId:guid}/optical-records")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<OpticalRecordResponse>>>> GetOpticalRecords(Guid examId, CancellationToken cancellationToken)
    {
        var response = await importService.GetOpticalRecordsAsync(examId, cancellationToken);
        return response is null ? NotFound(ApiResponse<IReadOnlyCollection<OpticalRecordResponse>>.Fail("Optik import kaydı bulunamadı veya erişim yetkiniz yok.")) : Ok(ApiResponse<IReadOnlyCollection<OpticalRecordResponse>>.Ok(response));
    }

    private static void ValidateFile(IFormFile? file, string extension, string label)
    {
        if (file is null || file.Length == 0) throw new ArgumentException($"{label} boş bırakılamaz.");
        if (!string.Equals(Path.GetExtension(file.FileName), extension, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException($"{label} yalnızca {extension} uzantılı olmalıdır.");
        if (file.Length > 10 * 1024 * 1024) throw new ArgumentException($"{label} boyutu 10 MB sınırını aşamaz.");
    }
}
