// Infrastructure servislerinin ortak entity dönüşümlerini sunar.
using Microsoft.EntityFrameworkCore;
using SDS.Application.Courses;
using SDS.Application.Exams;
using SDS.Application.Grades;
using SDS.Domain.Entities;
using SDS.Infrastructure.Persistence;

namespace SDS.Infrastructure.Services;

internal static class InfrastructureServiceSupport
{
    public static CourseResponse ToResponse(Course course) => new(course.Id, course.Code, course.Name, course.AcademicYearId, course.AcademicYear.Name, course.Instructors.Count, course.Exams.Count, IsTermEvaluationAvailable(course.Exams.Select(item => (item.ExamType, item.Status))));

    public static bool IsTermEvaluationAvailable(IEnumerable<(ExamType Type, ExamStatus Status)> exams)
    {
        var completed = exams.Where(item => item.Status == ExamStatus.Completed).Select(item => item.Type).ToHashSet();
        return completed.Contains(ExamType.Midterm) && (completed.Contains(ExamType.Final) || completed.Contains(ExamType.MakeUp));
    }

    public static async Task<GradeThresholds> LoadThresholdsAsync(SdsDbContext dbContext, CancellationToken cancellationToken)
    {
        var policy = await dbContext.GradingPolicies.AsNoTracking().OrderByDescending(item => item.UpdatedAt).FirstOrDefaultAsync(cancellationToken);
        if (policy is null) return GradeThresholds.SeedDefault;
        return new GradeThresholds(policy.AaMinimum, policy.BaMinimum, policy.BbMinimum, policy.CbMinimum, policy.CcMinimum, policy.DcMinimum);
    }
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
