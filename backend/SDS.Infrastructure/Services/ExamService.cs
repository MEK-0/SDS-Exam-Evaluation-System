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
        var exams = await dbContext.Exams
            .AsNoTracking()
            .Include(item => item.OpticalTemplateVersion)
            .ThenInclude(item => item.OpticalTemplate)
            .Where(item => item.CourseId == courseId)
            .OrderByDescending(item => item.CreatedAt)
            .ToListAsync(cancellationToken);
        return exams.Select(InfrastructureServiceSupport.ToResponse).ToList();
    }

    public async Task<ExamResponse?> GetExamAsync(Guid examId, CancellationToken cancellationToken)
    {
        var exam = await dbContext.Exams
            .AsNoTracking()
            .Include(item => item.OpticalTemplateVersion)
            .ThenInclude(item => item.OpticalTemplate)
            .SingleOrDefaultAsync(item => item.Id == examId, cancellationToken);
        if (exam is null || !await CanAccessCourseAsync(exam.CourseId, cancellationToken)) return null;
        return InfrastructureServiceSupport.ToResponse(exam);
    }

    public async Task<ExamResponse?> CreateExamAsync(Guid courseId, CreateExamRequest request, CancellationToken cancellationToken)
    {
        if (!await CanAccessCourseAsync(courseId, cancellationToken)) return null;
        if (!Enum.IsDefined(request.ExamType) || !Enum.IsDefined(request.EvaluationType)) throw new ArgumentException("Geçersiz sınav veya değerlendirme türü.");
        var version = await ResolveTemplateVersionAsync(request.OpticalTemplateId, cancellationToken);
        var exam = new Exam
        {
            Id = Guid.NewGuid(),
            CourseId = courseId,
            ExamType = request.ExamType,
            EvaluationType = request.EvaluationType,
            Status = ExamStatus.Draft,
            CreatedAt = DateTime.UtcNow,
            OpticalTemplateVersionId = version.Id
        };
        dbContext.Exams.Add(exam);
        await dbContext.SaveChangesAsync(cancellationToken);
        return InfrastructureServiceSupport.ToResponse(exam, version);
    }

    private async Task<OpticalTemplateVersion> ResolveTemplateVersionAsync(Guid? templateId, CancellationToken cancellationToken)
    {
        var templates = dbContext.OpticalTemplates.AsNoTracking();
        var template = templateId.HasValue
            ? await templates.SingleOrDefaultAsync(item => item.Id == templateId.Value, cancellationToken)
            : await templates.SingleOrDefaultAsync(item => item.IsDefault, cancellationToken);
        if (template is null)
        {
            throw templateId.HasValue
                ? new KeyNotFoundException("Seçilen optik şablon bulunamadı.")
                : new InvalidOperationException("Varsayılan optik şablon tanımlı değil. Önce bir optik şablonu varsayılan yapın.");
        }

        var version = await dbContext.OpticalTemplateVersions
            .AsNoTracking()
            .Include(item => item.OpticalTemplate)
            .Where(item => item.OpticalTemplateId == template.Id)
            .OrderByDescending(item => item.VersionNo)
            .FirstAsync(cancellationToken);
        return version;
    }

    private async Task<bool> CanAccessCourseAsync(Guid courseId, CancellationToken cancellationToken)
    {
        var user = currentUser.Get();
        return user.IsAdministrator || user.Id.HasValue && await dbContext.CourseInstructors.AnyAsync(item => item.CourseId == courseId && item.UserId == user.Id.Value, cancellationToken);
    }
}
