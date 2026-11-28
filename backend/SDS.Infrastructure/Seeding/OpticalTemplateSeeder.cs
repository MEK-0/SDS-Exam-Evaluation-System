// Gömülü JSON'daki başlangıç optik şablonlarını (Selçuk Standart ve örnek dosya düzeni) idempotent biçimde oluşturur.
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SDS.Domain.Entities;
using SDS.Infrastructure.Persistence;

namespace SDS.Infrastructure.Seeding;

public sealed class OpticalTemplateSeeder(SdsDbContext dbContext)
{
    private const string ResourceName = "SDS.Infrastructure.Seeding.optik-sablonlari.seed.json";

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var seeds = await ReadSeedsAsync(cancellationToken);
        var hasDefault = await dbContext.OpticalTemplates.AnyAsync(item => item.IsDefault, cancellationToken);
        var existingNames = await dbContext.OpticalTemplates.Select(item => item.Name).ToListAsync(cancellationToken);
        var changed = false;
        foreach (var seed in seeds.Where(item => !existingNames.Contains(item.Ad)))
        {
            var makeDefault = seed.Varsayilan && !hasDefault;
            hasDefault |= makeDefault;
            var template = new OpticalTemplate
            {
                Id = Guid.NewGuid(),
                Name = seed.Ad,
                IsDefault = makeDefault,
                CreatedAt = DateTime.UtcNow
            };
            dbContext.OpticalTemplates.Add(template);
            dbContext.OpticalTemplateVersions.Add(ToVersion(template.Id, seed));
            changed = true;
        }

        if (changed) await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<IReadOnlyCollection<TemplateSeed>> ReadSeedsAsync(CancellationToken cancellationToken)
    {
        await using var stream = typeof(OpticalTemplateSeeder).Assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("Optik şablon seed dosyası bulunamadı.");
        var document = await JsonSerializer.DeserializeAsync<SeedDocument>(stream, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }, cancellationToken);
        return document?.Sablonlar ?? throw new InvalidOperationException("Optik şablon seed dosyası okunamadı.");
    }

    private static OpticalTemplateVersion ToVersion(Guid templateId, TemplateSeed seed)
    {
        return new OpticalTemplateVersion
        {
            Id = Guid.NewGuid(),
            OpticalTemplateId = templateId,
            VersionNo = 1,
            StudentNumberStart = seed.StudentNumberStart,
            StudentNumberLength = seed.StudentNumberLength,
            StudentNameStart = seed.StudentNameStart,
            StudentNameLength = seed.StudentNameLength,
            BookletTypeStart = seed.BookletTypeStart,
            BookletTypeLength = seed.BookletTypeLength,
            ExamTypeStart = seed.ExamTypeStart,
            ExamTypeLength = seed.ExamTypeLength,
            AnswersStart = seed.AnswersStart,
            AnswersLength = seed.AnswersLength,
            BlankMarkChar = ' ',
            DoubleMarkChar = '*',
            EncodingMode = OpticalEncodingMode.Auto,
            CreatedAt = DateTime.UtcNow
        };
    }

    private sealed record SeedDocument(List<TemplateSeed> Sablonlar);

    private sealed record TemplateSeed(
        string Ad,
        bool Varsayilan,
        int StudentNumberStart,
        int StudentNumberLength,
        int? StudentNameStart,
        int? StudentNameLength,
        int BookletTypeStart,
        int BookletTypeLength,
        int? ExamTypeStart,
        int? ExamTypeLength,
        int AnswersStart,
        int AnswersLength);
}
