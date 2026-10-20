// Sınav taslağı oluşturma ve listeleme işlemlerini yürütür.
using Microsoft.EntityFrameworkCore;
using SDS.Application.Exams;
using SDS.Application.Security;
using SDS.Domain.Entities;
using SDS.Infrastructure.Persistence;

namespace SDS.Infrastructure.Services;

public sealed class ExamService(SdsDbContext dbContext, ICurrentUser currentUser) : IExamService
{
    public async Task<IReadOnlyCollection<ExamResponse>?> GetCourseExamsAsync(Guid courseId, CancellationToken cancellationToken)
    {
        if (!await CanAccessCourseAsync(courseId, cancellationToken)) return null;
        var exams = await dbContext.Exams.AsNoTracking().Where(item => item.CourseId == courseId).OrderByDescending(item => item.CreatedAt).ToListAsync(cancellationToken);
        return exams.Select(InfrastructureServiceSupport.ToResponse).ToList();
    }

    public async Task<ExamResponse?> GetExamAsync(Guid examId, CancellationToken cancellationToken)
    {
        var exam = await dbContext.Exams.AsNoTracking().SingleOrDefaultAsync(item => item.Id == examId, cancellationToken);
        return exam is null || !await CanAccessCourseAsync(exam.CourseId, cancellationToken) ? null : InfrastructureServiceSupport.ToResponse(exam);
    }

    public async Task<ExamResponse?> CreateExamAsync(Guid courseId, CreateExamRequest request, CancellationToken cancellationToken)
    {
        if (!await CanAccessCourseAsync(courseId, cancellationToken)) return null;
        if (!Enum.IsDefined(request.ExamType) || !Enum.IsDefined(request.EvaluationType)) throw new ArgumentException("Geçersiz sınav veya değerlendirme türü.");
        if (await dbContext.Exams.AnyAsync(item => item.CourseId == courseId && item.ExamType == request.ExamType, cancellationToken)) throw new InvalidOperationException("Bu sınav türü için zaten bir sınav taslağı bulunuyor.");
        var exam = new Exam { Id = Guid.NewGuid(), CourseId = courseId, ExamType = request.ExamType, EvaluationType = request.EvaluationType, Status = ExamStatus.Draft, CreatedAt = DateTime.UtcNow };
        dbContext.Exams.Add(exam);
        await dbContext.SaveChangesAsync(cancellationToken);
        return InfrastructureServiceSupport.ToResponse(exam);
    }

    private async Task<bool> CanAccessCourseAsync(Guid courseId, CancellationToken cancellationToken)
    {
        var user = currentUser.Get();
        return user.IsAdministrator || user.Id.HasValue && await dbContext.CourseInstructors.AnyAsync(item => item.CourseId == courseId && item.UserId == user.Id.Value, cancellationToken);
    }
}
