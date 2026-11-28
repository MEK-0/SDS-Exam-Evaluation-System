// Testlerde sınavlara bağlanacak optik şablon ve değişmez şablon sürümü kaydını oluşturur.
using System;
using SDS.Domain.Entities;
using SDS.Infrastructure.Persistence;

namespace SDS.Tests;

internal static class TestOpticalTemplates
{
    public static Guid AddVersion(
        SdsDbContext context,
        int studentNumberLength = 9,
        int bookletTypeStart = 9,
        int answersStart = 10,
        int answersLength = 25)
    {
        var template = new OpticalTemplate
        {
            Id = Guid.NewGuid(),
            Name = "Test şablonu " + Guid.NewGuid(),
            IsDefault = false,
            CreatedAt = DateTime.UtcNow
        };
        var version = new OpticalTemplateVersion
        {
            Id = Guid.NewGuid(),
            OpticalTemplateId = template.Id,
            VersionNo = 1,
            StudentNumberStart = 0,
            StudentNumberLength = studentNumberLength,
            BookletTypeStart = bookletTypeStart,
            BookletTypeLength = 1,
            AnswersStart = answersStart,
            AnswersLength = answersLength,
            BlankMarkChar = ' ',
            DoubleMarkChar = '*',
            EncodingMode = OpticalEncodingMode.Auto,
            CreatedAt = DateTime.UtcNow
        };
        context.OpticalTemplates.Add(template);
        context.OpticalTemplateVersions.Add(version);
        return version.Id;
    }
}
