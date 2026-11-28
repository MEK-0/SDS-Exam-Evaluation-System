// Optik şablon listeleme, yönetim ve sınava şablon bağlama endpointlerini sunar.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SDS.Application.Common;
using SDS.Application.Exams;
using SDS.Application.OpticalTemplates;
using SDS.Application.Security;

namespace SDS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public sealed class OpticalTemplatesController(IOpticalTemplateService templateService) : ControllerBase
{
    [HttpGet("optical-templates")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<OpticalTemplateResponse>>>> GetAll(CancellationToken cancellationToken)
    {
        var response = await templateService.GetAllAsync(cancellationToken);
        return Ok(ApiResponse<IReadOnlyCollection<OpticalTemplateResponse>>.Ok(response));
    }

    [HttpPost("optical-templates")]
    [Authorize(Roles = RoleNames.Management)]
    public async Task<ActionResult<ApiResponse<OpticalTemplateResponse>>> Create(SaveOpticalTemplateRequest request, CancellationToken cancellationToken)
    {
        var response = await templateService.CreateAsync(request, cancellationToken);
        return Ok(ApiResponse<OpticalTemplateResponse>.Ok(response, "Optik şablon oluşturuldu."));
    }

    [HttpPut("optical-templates/{id:guid}")]
    [Authorize(Roles = RoleNames.Management)]
    public async Task<ActionResult<ApiResponse<OpticalTemplateResponse>>> Update(Guid id, SaveOpticalTemplateRequest request, CancellationToken cancellationToken)
    {
        var response = await templateService.UpdateAsync(id, request, cancellationToken);
        return response is null
            ? NotFound(ApiResponse<OpticalTemplateResponse>.Fail("Optik şablon bulunamadı veya erişim yetkiniz yok."))
            : Ok(ApiResponse<OpticalTemplateResponse>.Ok(response, "Optik şablonun yeni sürümü kaydedildi."));
    }

    [HttpPost("optical-templates/{id:guid}/default")]
    [Authorize(Roles = RoleNames.Management)]
    public async Task<ActionResult<ApiResponse<OpticalTemplateResponse>>> SetDefault(Guid id, CancellationToken cancellationToken)
    {
        var response = await templateService.SetDefaultAsync(id, cancellationToken);
        return response is null
            ? NotFound(ApiResponse<OpticalTemplateResponse>.Fail("Optik şablon bulunamadı veya erişim yetkiniz yok."))
            : Ok(ApiResponse<OpticalTemplateResponse>.Ok(response, "Varsayılan optik şablon güncellendi."));
    }

    [HttpPut("exams/{examId:guid}/optical-template")]
    public async Task<ActionResult<ApiResponse<ExamResponse>>> BindToExam(Guid examId, BindExamTemplateRequest request, CancellationToken cancellationToken)
    {
        var response = await templateService.BindToExamAsync(examId, request.TemplateId!.Value, cancellationToken);
        return response is null
            ? NotFound(ApiResponse<ExamResponse>.Fail("Sınav bulunamadı veya erişim yetkiniz yok."))
            : Ok(ApiResponse<ExamResponse>.Ok(response, "Optik şablon sınava bağlandı."));
    }
}
