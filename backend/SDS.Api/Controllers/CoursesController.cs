// Ders ve akademik yıl endpointlerini sunar.
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SDS.Application.Common;
using SDS.Application.Courses;
using SDS.Application.Security;

namespace SDS.Api.Controllers;

[ApiController]
[Authorize]
[Route("api")]
public sealed class CoursesController(ICourseService courseService) : ControllerBase
{
    [HttpGet("academic-years")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<AcademicYearResponse>>>> AcademicYears(CancellationToken cancellationToken) => Ok(ApiResponse<IReadOnlyCollection<AcademicYearResponse>>.Ok(await courseService.GetAcademicYearsAsync(cancellationToken)));

    [HttpGet("courses")]
    public async Task<ActionResult<ApiResponse<IReadOnlyCollection<CourseResponse>>>> GetCourses(CancellationToken cancellationToken) => Ok(ApiResponse<IReadOnlyCollection<CourseResponse>>.Ok(await courseService.GetCoursesAsync(cancellationToken)));

    [HttpPost("courses")]
    [Authorize(Roles = RoleNames.Management)]
    public async Task<ActionResult<ApiResponse<CourseResponse>>> CreateCourse(CreateCourseRequest request, CancellationToken cancellationToken) => Ok(ApiResponse<CourseResponse>.Ok(await courseService.CreateCourseAsync(request, cancellationToken)));

    [HttpGet("courses/{id:guid}")]
    public async Task<ActionResult<ApiResponse<CourseResponse>>> GetCourse(Guid id, CancellationToken cancellationToken)
    {
        var response = await courseService.GetCourseAsync(id, cancellationToken);
        return response is null ? NotFound(ApiResponse<CourseResponse>.Fail("Ders bulunamadı veya erişim yetkiniz yok.")) : Ok(ApiResponse<CourseResponse>.Ok(response));
    }

    [HttpPut("courses/{id:guid}")]
    [Authorize(Roles = RoleNames.Management)]
    public async Task<ActionResult<ApiResponse<CourseResponse>>> UpdateCourse(Guid id, UpdateCourseRequest request, CancellationToken cancellationToken)
    {
        var response = await courseService.UpdateCourseAsync(id, request, cancellationToken);
        return response is null ? NotFound(ApiResponse<CourseResponse>.Fail("Ders bulunamadı.")) : Ok(ApiResponse<CourseResponse>.Ok(response));
    }

    [HttpPost("courses/{id:guid}/instructors")]
    [Authorize(Roles = RoleNames.Management)]
    public async Task<ActionResult<ApiResponse<object>>> AssignInstructor(Guid id, AssignInstructorRequest request, CancellationToken cancellationToken) => await courseService.AssignInstructorAsync(id, request, cancellationToken) ? Ok(ApiResponse<object>.Ok(new { }, "Eğitmen atandı.")) : NotFound(ApiResponse<object>.Fail("Ders veya eğitmen bulunamadı."));
}
