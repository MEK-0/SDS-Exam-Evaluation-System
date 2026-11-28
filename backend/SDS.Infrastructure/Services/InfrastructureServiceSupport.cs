// Infrastructure servislerinin ortak entity dönüşümlerini sunar.
using SDS.Application.Courses;
using SDS.Application.Exams;
using SDS.Domain.Entities;

namespace SDS.Infrastructure.Services;

internal static class InfrastructureServiceSupport
{
    public static CourseResponse ToResponse(Course course) => new(course.Id, course.Code, course.Name, course.AcademicYearId, course.AcademicYear.Name, course.Instructors.Count, course.Exams.Count);
    public static ExamResponse ToResponse(Exam exam) => ToResponse(exam, exam.OpticalTemplateVersion);

    public static ExamResponse ToResponse(Exam exam, OpticalTemplateVersion version)
    {
        return new ExamResponse(
            exam.Id,
            exam.CourseId,
            exam.ExamType,
            exam.EvaluationType,
            exam.Status,
            exam.CreatedAt,
            version.OpticalTemplateId,
            version.OpticalTemplate.Name,
            version.Id,
            version.VersionNo);
    }
}
