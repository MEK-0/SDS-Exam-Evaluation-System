// Sınav taslağı endpointlerini sunar.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SDS.Api.Models;
using SDS.Application.AnswerKeys;
using SDS.Application.Common;
using SDS.Application.Exams;
using SDS.Application.Imports;
using SDS.Application.Matching;
using SDS.Application.Grades;
using SDS.Application.LearningOutcomes;
using SDS.Application.Analysis;
using SDS.Application.OpenEnded;

namespace SDS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public sealed class ExamsController(IExamService examService, IStudentDataImportService importService, IAnswerKeyService answerKeyService, IStudentMatchingService matchingService, IGradeService gradeService, ILearningOutcomeService learningOutcomeService, IAnalysisService analysisService, IOpenEndedService openEndedService) : ControllerBase
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
        var classListFiles = request.ClassListFiles.Count > 0 ? request.ClassListFiles : request.ClassListFile is null ? [] : [request.ClassListFile];
        if (classListFiles.Count is < 1 or > 2) throw new ArgumentException("En az bir, en fazla iki sınıf listesi yükleyebilirsiniz.");
        foreach (var classListFile in classListFiles) ValidateFile(classListFile, ".xlsx", "Sınıf listesi");
        await using var opticalStream = request.OpticalFile!.OpenReadStream();
        var classListInputs = classListFiles.Select(file => new ClassListFileInput(file.OpenReadStream(), file.FileName)).ToList();
        await using var _ = new CompositeAsyncDisposable(classListInputs.Select(item => item.Content));
        var configuration = new OpticalFormConfigurationInput(request.StudentNumberStart, request.StudentNumberLength, request.StudentNameStart, request.StudentNameLength, request.BookletTypeStart, request.BookletTypeLength, request.ExamTypeStart, request.ExamTypeLength, request.AnswersStart, request.AnswersLength, request.EncodingName);
        var response = await importService.ImportAsync(examId, opticalStream, request.OpticalFile.FileName, classListInputs, configuration, cancellationToken);
        return Ok(ApiResponse<ImportPreviewResponse>.Ok(response, "Dosyalar işlendi."));
    }

    [HttpPost("exams/{examId:guid}/open-ended/class-list")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<ImportPreviewResponse>>> UploadOpenEndedClassList(Guid examId, [FromForm] List<IFormFile> classListFiles, CancellationToken cancellationToken)
    {
        if (classListFiles.Count is < 1 or > 2) throw new ArgumentException("En az bir, en fazla iki sınıf listesi yükleyebilirsiniz.");
        foreach (var file in classListFiles) ValidateFile(file, ".xlsx", "Sınıf listesi");
        var inputs = classListFiles.Select(file => new ClassListFileInput(file.OpenReadStream(), file.FileName)).ToList();
        await using var _ = new CompositeAsyncDisposable(inputs.Select(item => item.Content));
        var response = await importService.ImportClassListAsync(examId, inputs, cancellationToken);
        return Ok(ApiResponse<ImportPreviewResponse>.Ok(response, "Sınıf listesi işlendi."));
    }

    [HttpDelete("exams/{examId:guid}/files")]
    public async Task<ActionResult<ApiResponse<object>>> RemoveFiles(Guid examId, [FromQuery] string fileType, CancellationToken cancellationToken)
    {
        var removed = await importService.RemoveAsync(examId, fileType, Request.Query["fileName"].FirstOrDefault(), cancellationToken);
        return removed is null ? NotFound(ApiResponse<object>.Fail("Sınav bulunamadı veya erişim yetkiniz yok.")) : Ok(ApiResponse<object>.Ok(new { }, "Dosya verisi kaldırıldı."));
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

    [HttpPost("exams/{examId:guid}/matching/run")]
    public async Task<ActionResult<ApiResponse<MatchingResponse>>> RunMatching(Guid examId, CancellationToken cancellationToken)
    {
        var response = await matchingService.RunAsync(examId, cancellationToken);
        return response is null ? NotFound(ApiResponse<MatchingResponse>.Fail("Sınav bulunamadı veya erişim yetkiniz yok.")) : Ok(ApiResponse<MatchingResponse>.Ok(response, "Otomatik eşleştirme tamamlandı."));
    }

    [HttpGet("exams/{examId:guid}/matching")]
    public async Task<ActionResult<ApiResponse<MatchingResponse>>> GetMatching(Guid examId, CancellationToken cancellationToken)
    {
        var response = await matchingService.GetAsync(examId, cancellationToken);
        return response is null ? NotFound(ApiResponse<MatchingResponse>.Fail("Sınav bulunamadı veya erişim yetkiniz yok.")) : Ok(ApiResponse<MatchingResponse>.Ok(response));
    }

    [HttpGet("exams/{examId:guid}/matching/summary")]
    public async Task<ActionResult<ApiResponse<MatchingSummaryResponse>>> GetMatchingSummary(Guid examId, CancellationToken cancellationToken)
    {
        var response = await matchingService.GetAsync(examId, cancellationToken);
        return response is null ? NotFound(ApiResponse<MatchingSummaryResponse>.Fail("Sınav bulunamadı veya erişim yetkiniz yok.")) : Ok(ApiResponse<MatchingSummaryResponse>.Ok(response.Summary));
    }

    [HttpPut("exams/{examId:guid}/matching/manual")]
    public async Task<ActionResult<ApiResponse<MatchingResponse>>> CreateManualMatch(Guid examId, ManualMatchRequest request, CancellationToken cancellationToken)
    {
        var response = await matchingService.CreateManualAsync(examId, request, cancellationToken);
        return response is null ? NotFound(ApiResponse<MatchingResponse>.Fail("Sınav bulunamadı veya erişim yetkiniz yok.")) : Ok(ApiResponse<MatchingResponse>.Ok(response, "Manuel eşleştirme kaydedildi."));
    }

    [HttpPut("exams/{examId:guid}/matching/absent")]
    public async Task<ActionResult<ApiResponse<MatchingResponse>>> MarkAbsent(Guid examId, MarkAbsentRequest request, CancellationToken cancellationToken)
    {
        var response = await matchingService.MarkAbsentAsync(examId, request, cancellationToken);
        return response is null ? NotFound(ApiResponse<MatchingResponse>.Fail("Sınav bulunamadı veya erişim yetkiniz yok.")) : Ok(ApiResponse<MatchingResponse>.Ok(response, "Öğrenci sınava girmedi olarak işaretlendi."));
    }

    [HttpPut("exams/{examId:guid}/matching/ignore-optical")]
    public async Task<ActionResult<ApiResponse<MatchingResponse>>> IgnoreOptical(Guid examId, IgnoreOpticalRequest request, CancellationToken cancellationToken)
    {
        var response = await matchingService.IgnoreOpticalAsync(examId, request, cancellationToken);
        return response is null ? NotFound(ApiResponse<MatchingResponse>.Fail("Sınav bulunamadı veya erişim yetkiniz yok.")) : Ok(ApiResponse<MatchingResponse>.Ok(response, "Optik kaydı eşleştirme dışında bırakıldı."));
    }

    [HttpDelete("exams/{examId:guid}/matching/manual/{matchId:guid}")]
    public async Task<ActionResult<ApiResponse<MatchingResponse>>> RemoveManualMatch(Guid examId, Guid matchId, CancellationToken cancellationToken)
    {
        var response = await matchingService.RemoveManualAsync(examId, matchId, cancellationToken);
        return response is null ? NotFound(ApiResponse<MatchingResponse>.Fail("Sınav bulunamadı veya erişim yetkiniz yok.")) : Ok(ApiResponse<MatchingResponse>.Ok(response, "Manuel eşleştirme kaldırıldı."));
    }

    [HttpPost("exams/{examId:guid}/matching/confirm")]
    public async Task<ActionResult<ApiResponse<MatchingResponse>>> ConfirmMatching(Guid examId, CancellationToken cancellationToken)
    {
        var response = await matchingService.ConfirmAsync(examId, cancellationToken);
        return response is null ? NotFound(ApiResponse<MatchingResponse>.Fail("Sınav bulunamadı veya erişim yetkiniz yok.")) : Ok(ApiResponse<MatchingResponse>.Ok(response, "Eşleştirme onaylandı."));
    }

    [HttpGet("exams/{examId:guid}/grades")]
    public async Task<ActionResult<ApiResponse<GradesResponse>>> GetGrades(Guid examId, CancellationToken cancellationToken)
    {
        var response = await gradeService.GetAsync(examId, cancellationToken);
        return response is null ? NotFound(ApiResponse<GradesResponse>.Fail("Sınav bulunamadı veya erişim yetkiniz yok.")) : Ok(ApiResponse<GradesResponse>.Ok(response));
    }

    [HttpPost("exams/{examId:guid}/grades/calculate")]
    public async Task<ActionResult<ApiResponse<GradesResponse>>> CalculateGrades(Guid examId, CancellationToken cancellationToken)
    {
        var response = await gradeService.CalculateAsync(examId, cancellationToken);
        return response is null ? NotFound(ApiResponse<GradesResponse>.Fail("Sınav bulunamadı veya erişim yetkiniz yok.")) : Ok(ApiResponse<GradesResponse>.Ok(response, "Notlar hesaplandı."));
    }

    [HttpGet("exams/{examId:guid}/grades/summary")]
    public async Task<ActionResult<ApiResponse<GradeSummaryResponse>>> GetGradeSummary(Guid examId, CancellationToken cancellationToken)
    {
        var response = await gradeService.GetSummaryAsync(examId, cancellationToken);
        return response is null ? NotFound(ApiResponse<GradeSummaryResponse>.Fail("Sınav bulunamadı veya erişim yetkiniz yok.")) : Ok(ApiResponse<GradeSummaryResponse>.Ok(response));
    }

    [HttpPut("exams/{examId:guid}/grades/{studentId:guid}")]
    public async Task<ActionResult<ApiResponse<GradesResponse>>> UpdateGrade(Guid examId, Guid studentId, UpdateGradeRequest request, CancellationToken cancellationToken)
    {
        var response = await gradeService.UpdateAsync(examId, studentId, request, cancellationToken);
        return response is null ? NotFound(ApiResponse<GradesResponse>.Fail("Sınav bulunamadı veya erişim yetkiniz yok.")) : Ok(ApiResponse<GradesResponse>.Ok(response, "Not güncellendi."));
    }

    [HttpPost("exams/{examId:guid}/grades/bonus")]
    public async Task<ActionResult<ApiResponse<GradesResponse>>> AddBonus(Guid examId, BonusRequest request, CancellationToken cancellationToken)
    {
        var response = await gradeService.AddBonusAsync(examId, request, cancellationToken);
        return response is null ? NotFound(ApiResponse<GradesResponse>.Fail("Sınav bulunamadı veya erişim yetkiniz yok.")) : Ok(ApiResponse<GradesResponse>.Ok(response, "Bonus puanı uygulandı."));
    }

    [HttpPost("exams/{examId:guid}/grades/project-import")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<ApiResponse<ProjectGradeImportResponse>>> ImportProjectGrades(Guid examId, IFormFile? file, CancellationToken cancellationToken)
    {
        ValidateFile(file, ".xlsx", "Proje notu dosyası");
        await using var stream = file!.OpenReadStream();
        var response = await gradeService.ImportProjectGradesAsync(examId, stream, file.FileName, cancellationToken);
        return response is null ? NotFound(ApiResponse<ProjectGradeImportResponse>.Fail("Sınav bulunamadı veya erişim yetkiniz yok.")) : Ok(ApiResponse<ProjectGradeImportResponse>.Ok(response, "Proje notları işlendi."));
    }

    [HttpGet("courses/{courseId:guid}/learning-outcomes")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<LearningOutcomeResponse>>>> GetLearningOutcomes(Guid courseId, CancellationToken cancellationToken)
    {
        var response = await learningOutcomeService.GetCourseOutcomesAsync(courseId, cancellationToken);
        return response is null ? NotFound(ApiResponse<IReadOnlyCollection<LearningOutcomeResponse>>.Fail("Ders bulunamadı veya erişim yetkiniz yok.")) : Ok(ApiResponse<IReadOnlyCollection<LearningOutcomeResponse>>.Ok(response));
    }

    [HttpPost("courses/{courseId:guid}/learning-outcomes")]
    public async Task<ActionResult<ApiResponse<LearningOutcomeResponse>>> CreateLearningOutcome(Guid courseId, CreateLearningOutcomeRequest request, CancellationToken cancellationToken)
    {
        var response = await learningOutcomeService.CreateOutcomeAsync(courseId, request, cancellationToken);
        return response is null ? NotFound(ApiResponse<LearningOutcomeResponse>.Fail("Ders bulunamadı veya erişim yetkiniz yok.")) : Ok(ApiResponse<LearningOutcomeResponse>.Ok(response, "Öğrenme çıktısı eklendi."));
    }

    [HttpGet("exams/{examId:guid}/learning-outcomes")]
    public async Task<ActionResult<ApiResponse<LearningOutcomePageResponse>>> GetLearningOutcomePage(Guid examId, CancellationToken cancellationToken)
    {
        var response = await learningOutcomeService.GetExamPageAsync(examId, cancellationToken);
        return response is null ? NotFound(ApiResponse<LearningOutcomePageResponse>.Fail("Sınav bulunamadı veya erişim yetkiniz yok.")) : Ok(ApiResponse<LearningOutcomePageResponse>.Ok(response));
    }

    [HttpPut("exams/{examId:guid}/question-mappings")]
    public async Task<ActionResult<ApiResponse<LearningOutcomePageResponse>>> SaveBookletMappings(Guid examId, SaveBookletQuestionMappingsRequest request, CancellationToken cancellationToken)
    {
        var response = await learningOutcomeService.SaveBookletMappingsAsync(examId, request, cancellationToken);
        return response is null ? NotFound(ApiResponse<LearningOutcomePageResponse>.Fail("Sınav bulunamadı veya erişim yetkiniz yok.")) : Ok(ApiResponse<LearningOutcomePageResponse>.Ok(response, "Kitapçık soru eşleştirmeleri kaydedildi."));
    }

    [HttpPut("exams/{examId:guid}/learning-outcome-mappings")]
    public async Task<ActionResult<ApiResponse<LearningOutcomePageResponse>>> SaveLearningOutcomeMappings(Guid examId, SaveLearningOutcomeMappingsRequest request, CancellationToken cancellationToken)
    {
        var response = await learningOutcomeService.SaveOutcomeMappingsAsync(examId, request, cancellationToken);
        return response is null ? NotFound(ApiResponse<LearningOutcomePageResponse>.Fail("Sınav bulunamadı veya erişim yetkiniz yok.")) : Ok(ApiResponse<LearningOutcomePageResponse>.Ok(response, "Öğrenme çıktısı ilişkileri kaydedildi."));
    }

    [HttpPost("exams/{examId:guid}/complete")]
    public async Task<ActionResult<ApiResponse<LearningOutcomePageResponse>>> CompleteExam(Guid examId, CompleteExamRequest request, CancellationToken cancellationToken)
    {
        var response = await learningOutcomeService.CompleteExamAsync(examId, request, cancellationToken);
        return response is null ? NotFound(ApiResponse<LearningOutcomePageResponse>.Fail("Sınav bulunamadı veya erişim yetkiniz yok.")) : Ok(ApiResponse<LearningOutcomePageResponse>.Ok(response, "Sınav tamamlandı."));
    }

    [HttpGet("exams/{examId:guid}/open-ended/settings")]
    public async Task<ActionResult<ApiResponse<OpenEndedSettingsResponse>>> GetOpenEndedSettings(Guid examId, CancellationToken cancellationToken)
    {
        var response = await openEndedService.GetSettingsAsync(examId, cancellationToken);
        return response is null ? NotFound(ApiResponse<OpenEndedSettingsResponse>.Fail("Açık uçlu sınav ayarları bulunamadı.")) : Ok(ApiResponse<OpenEndedSettingsResponse>.Ok(response));
    }

    [HttpPut("exams/{examId:guid}/open-ended/settings")]
    public async Task<ActionResult<ApiResponse<OpenEndedSettingsResponse>>> SaveOpenEndedSettings(Guid examId, SaveOpenEndedSettingsRequest request, CancellationToken cancellationToken)
    {
        var response = await openEndedService.SaveSettingsAsync(examId, request, cancellationToken);
        return response is null ? NotFound(ApiResponse<OpenEndedSettingsResponse>.Fail("Sınav bulunamadı veya erişim yetkiniz yok.")) : Ok(ApiResponse<OpenEndedSettingsResponse>.Ok(response, "Açık uçlu sınav ayarları kaydedildi."));
    }

    [HttpPut("exams/{examId:guid}/open-ended/grades/{studentId:guid}")]
    public async Task<ActionResult<ApiResponse<GradesResponse>>> SaveOpenEndedGrade(Guid examId, Guid studentId, OpenEndedGradeRequest request, CancellationToken cancellationToken)
    {
        var response = await openEndedService.SaveGradeAsync(examId, studentId, request, cancellationToken);
        return response is null ? NotFound(ApiResponse<GradesResponse>.Fail("Sınav veya öğrenci bulunamadı.")) : Ok(ApiResponse<GradesResponse>.Ok(response, "Ham not kaydedildi."));
    }

    [HttpPost("exams/{examId:guid}/open-ended/grades/import")]
    public async Task<ActionResult<ApiResponse<OpenEndedGradeImportResponse>>> ImportOpenEndedGrades(Guid examId, IFormFile? file, CancellationToken cancellationToken)
    {
        ValidateFile(file, ".xlsx", "Açık uçlu not dosyası");
        await using var stream = file!.OpenReadStream();
        var response = await openEndedService.ImportGradesAsync(examId, stream, file.FileName, cancellationToken);
        return response is null ? NotFound(ApiResponse<OpenEndedGradeImportResponse>.Fail("Sınav bulunamadı veya erişim yetkiniz yok.")) : Ok(ApiResponse<OpenEndedGradeImportResponse>.Ok(response, "Açık uçlu notları işlendi."));
    }

    [HttpGet("exams/{examId:guid}/analysis")]
    public async Task<ActionResult<ApiResponse<AnalysisResponse>>> GetAnalysis(Guid examId, [FromQuery] Guid? studentId, CancellationToken cancellationToken)
    {
        var response = await analysisService.GetAsync(examId, studentId, cancellationToken);
        return response is null ? NotFound(ApiResponse<AnalysisResponse>.Fail("Sınav bulunamadı veya erişim yetkiniz yok.")) : Ok(ApiResponse<AnalysisResponse>.Ok(response));
    }

    [HttpGet("exams/{examId:guid}/analysis/export")]
    public async Task<IActionResult> ExportAnalysis(Guid examId, CancellationToken cancellationToken)
    {
        var response = await analysisService.ExportAsync(examId, cancellationToken);
        return response is null ? NotFound(ApiResponse<object>.Fail("Sınav bulunamadı veya erişim yetkiniz yok.")) : File(response, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "sinav-analiz-raporu.xlsx");
    }

    private static void ValidateFile(IFormFile? file, string extension, string label)
    {
        if (file is null || file.Length == 0) throw new ArgumentException($"{label} boş bırakılamaz.");
        if (!string.Equals(Path.GetExtension(file.FileName), extension, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException($"{label} yalnızca {extension} uzantılı olmalıdır.");
        if (file.Length > 10 * 1024 * 1024) throw new ArgumentException($"{label} boyutu 10 MB sınırını aşamaz.");
    }

    private sealed class CompositeAsyncDisposable(IEnumerable<Stream> streams) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            foreach (var stream in streams) await stream.DisposeAsync();
        }
    }
}
