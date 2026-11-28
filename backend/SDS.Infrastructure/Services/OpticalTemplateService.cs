// Optik şablonları ve değişmez sürümlerini yönetir; sınavlara şablon sürümü bağlar.
using Microsoft.EntityFrameworkCore;
using SDS.Application.Exams;
using SDS.Application.OpticalTemplates;
using SDS.Application.Security;
using SDS.Domain.Entities;
using SDS.Infrastructure.Persistence;

namespace SDS.Infrastructure.Services;

public sealed class OpticalTemplateService(SdsDbContext dbContext, ICurrentUser currentUser) : IOpticalTemplateService
{
    public async Task<IReadOnlyCollection<OpticalTemplateResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        var templates = await dbContext.OpticalTemplates
            .AsNoTracking()
            .Include(item => item.Versions)
            .OrderByDescending(item => item.IsDefault)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken);
        return templates.Select(ToResponse).ToList();
    }

    public async Task<OpticalTemplateResponse> CreateAsync(SaveOpticalTemplateRequest request, CancellationToken cancellationToken)
    {
        var actorId = EnsureAdministrator();
        ValidateRequest(request);
        var name = request.Name.Trim();
        if (await dbContext.OpticalTemplates.AnyAsync(item => item.Name == name, cancellationToken))
        {
            throw new InvalidOperationException("Bu adla bir optik şablon zaten kayıtlı.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var makeDefault = request.IsDefault || !await dbContext.OpticalTemplates.AnyAsync(item => item.IsDefault, cancellationToken);
        if (makeDefault) await ClearDefaultAsync(null, cancellationToken);
        var template = new OpticalTemplate
        {
            Id = Guid.NewGuid(),
            Name = name,
            IsDefault = makeDefault,
            CreatedAt = DateTime.UtcNow
        };
        var version = CreateVersion(template.Id, 1, request, actorId);
        dbContext.OpticalTemplates.Add(template);
        dbContext.OpticalTemplateVersions.Add(version);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(template);
    }

    public async Task<OpticalTemplateResponse?> UpdateAsync(Guid templateId, SaveOpticalTemplateRequest request, CancellationToken cancellationToken)
    {
        var actorId = EnsureAdministrator();
        ValidateRequest(request);
        var template = await dbContext.OpticalTemplates.Include(item => item.Versions).SingleOrDefaultAsync(item => item.Id == templateId, cancellationToken);
        if (template is null) return null;
        var name = request.Name.Trim();
        if (await dbContext.OpticalTemplates.AnyAsync(item => item.Id != templateId && item.Name == name, cancellationToken))
        {
            throw new InvalidOperationException("Bu adla bir optik şablon zaten kayıtlı.");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        if (request.IsDefault && !template.IsDefault) await ClearDefaultAsync(template.Id, cancellationToken);
        template.Name = name;
        if (request.IsDefault) template.IsDefault = true;
        var nextVersionNo = template.Versions.Max(item => item.VersionNo) + 1;
        var version = CreateVersion(template.Id, nextVersionNo, request, actorId);
        dbContext.OpticalTemplateVersions.Add(version);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(template);
    }

    public async Task<OpticalTemplateResponse?> SetDefaultAsync(Guid templateId, CancellationToken cancellationToken)
    {
        EnsureAdministrator();
        var template = await dbContext.OpticalTemplates.Include(item => item.Versions).SingleOrDefaultAsync(item => item.Id == templateId, cancellationToken);
        if (template is null) return null;
        if (template.IsDefault) return ToResponse(template);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await ClearDefaultAsync(template.Id, cancellationToken);
        template.IsDefault = true;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(template);
    }

    public async Task<ExamResponse?> BindToExamAsync(Guid examId, Guid templateId, CancellationToken cancellationToken)
    {
        var exam = await dbContext.Exams.SingleOrDefaultAsync(item => item.Id == examId, cancellationToken);
        if (exam is null || !await CanAccessCourseAsync(exam.CourseId, cancellationToken)) return null;
        if (exam.Status == ExamStatus.Completed)
        {
            throw new InvalidOperationException("Tamamlanmış sınavın optik form şablonu değiştirilemez.");
        }

        var hasImport = await dbContext.ExamImports.AnyAsync(item => item.ExamId == examId, cancellationToken);
        if (hasImport)
        {
            throw new InvalidOperationException("Dosyaları yüklenmiş sınavın optik form şablonu değiştirilemez. Önce yüklenen dosyaları kaldırın.");
        }

        var version = await dbContext.OpticalTemplateVersions
            .AsNoTracking()
            .Include(item => item.OpticalTemplate)
            .Where(item => item.OpticalTemplateId == templateId)
            .OrderByDescending(item => item.VersionNo)
            .FirstOrDefaultAsync(cancellationToken);
        if (version is null) throw new KeyNotFoundException("Seçilen optik şablon bulunamadı.");
        exam.OpticalTemplateVersionId = version.Id;
        await dbContext.SaveChangesAsync(cancellationToken);
        return InfrastructureServiceSupport.ToResponse(exam, version);
    }

    private async Task ClearDefaultAsync(Guid? exceptTemplateId, CancellationToken cancellationToken)
    {
        var exceptId = exceptTemplateId ?? Guid.Empty;
        var current = await dbContext.OpticalTemplates
            .Where(item => item.IsDefault && item.Id != exceptId)
            .ToListAsync(cancellationToken);
        if (current.Count == 0) return;
        foreach (var item in current) item.IsDefault = false;
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private Guid EnsureAdministrator()
    {
        var user = currentUser.Get();
        if (!user.IsAdministrator || !user.Id.HasValue) throw new UnauthorizedAccessException("Bu işlem için yönetici yetkisi gerekir.");
        return user.Id.Value;
    }

    private async Task<bool> CanAccessCourseAsync(Guid courseId, CancellationToken cancellationToken)
    {
        var user = currentUser.Get();
        return user.IsAdministrator || user.Id.HasValue && await dbContext.CourseInstructors.AnyAsync(item => item.CourseId == courseId && item.UserId == user.Id.Value, cancellationToken);
    }

    private static OpticalTemplateVersion CreateVersion(Guid templateId, int versionNo, SaveOpticalTemplateRequest request, Guid actorId)
    {
        return new OpticalTemplateVersion
        {
            Id = Guid.NewGuid(),
            OpticalTemplateId = templateId,
            VersionNo = versionNo,
            StudentNumberStart = request.StudentNumberStart,
            StudentNumberLength = request.StudentNumberLength,
            StudentNameStart = request.StudentNameStart,
            StudentNameLength = request.StudentNameLength,
            BookletTypeStart = request.BookletTypeStart,
            BookletTypeLength = request.BookletTypeLength,
            ExamTypeStart = request.ExamTypeStart,
            ExamTypeLength = request.ExamTypeLength,
            AnswersStart = request.AnswersStart,
            AnswersLength = request.AnswersLength,
            BlankMarkChar = request.BlankMarkChar,
            DoubleMarkChar = request.DoubleMarkChar,
            EncodingMode = request.EncodingMode,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = actorId
        };
    }

    private static void ValidateRequest(SaveOpticalTemplateRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name)) throw new ArgumentException("Şablon adı boş olamaz.");
        if (request.Name.Trim().Length > 160) throw new ArgumentException("Şablon adı en fazla 160 karakter olabilir.");
        if (!Enum.IsDefined(request.EncodingMode)) throw new ArgumentException("Geçersiz dosya kodlaması.");
        if (request.StudentNameStart.HasValue != request.StudentNameLength.HasValue || request.ExamTypeStart.HasValue != request.ExamTypeLength.HasValue)
        {
            throw new ArgumentException("İsteğe bağlı optik alanların başlangıç ve uzunluk değerleri birlikte girilmelidir.");
        }

        if (request.BlankMarkChar == request.DoubleMarkChar) throw new ArgumentException("Boş işaret ve çift işaret karakterleri farklı olmalıdır.");
        var fields = new List<(int Start, int Length)>
        {
            (request.StudentNumberStart, request.StudentNumberLength),
            (request.BookletTypeStart, request.BookletTypeLength),
            (request.AnswersStart, request.AnswersLength)
        };
        if (request.StudentNameStart.HasValue) fields.Add((request.StudentNameStart.Value, request.StudentNameLength!.Value));
        if (request.ExamTypeStart.HasValue) fields.Add((request.ExamTypeStart.Value, request.ExamTypeLength!.Value));
        if (fields.Any(item => item.Start < 0 || item.Length <= 0)) throw new ArgumentException("Optik alan konumları geçersiz.");
        for (var first = 0; first < fields.Count; first++)
        {
            for (var second = first + 1; second < fields.Count; second++)
            {
                var overlaps = fields[first].Start < fields[second].Start + fields[second].Length
                    && fields[second].Start < fields[first].Start + fields[first].Length;
                if (overlaps) throw new ArgumentException("Optik alanlar çakışamaz.");
            }
        }
    }

    private static OpticalTemplateResponse ToResponse(OpticalTemplate template)
    {
        var current = template.Versions.OrderByDescending(item => item.VersionNo).First();
        return new OpticalTemplateResponse(template.Id, template.Name, template.IsDefault, template.Versions.Count, ToVersionResponse(current), template.CreatedAt);
    }

    private static OpticalTemplateVersionResponse ToVersionResponse(OpticalTemplateVersion item)
    {
        return new OpticalTemplateVersionResponse(
            item.Id,
            item.VersionNo,
            item.StudentNumberStart,
            item.StudentNumberLength,
            item.StudentNameStart,
            item.StudentNameLength,
            item.BookletTypeStart,
            item.BookletTypeLength,
            item.ExamTypeStart,
            item.ExamTypeLength,
            item.AnswersStart,
            item.AnswersLength,
            item.BlankMarkChar,
            item.DoubleMarkChar,
            item.EncodingMode,
            item.CreatedAt);
    }
}
