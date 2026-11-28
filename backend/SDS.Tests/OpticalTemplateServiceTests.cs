// Optik şablon sürümlemesini, varsayılan şablonu, yetkiyi ve import'un sınav şablon sürümünü okumasını doğrular.
using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SDS.Application.Admin;
using SDS.Application.Exams;
using SDS.Application.Imports;
using SDS.Application.OpticalTemplates;
using SDS.Application.Security;
using SDS.Domain.Entities;
using SDS.Infrastructure.Importing;
using SDS.Infrastructure.Persistence;
using SDS.Infrastructure.Seeding;
using SDS.Infrastructure.Services;
using Xunit;

namespace SDS.Tests;

public sealed class OpticalTemplateServiceTests
{
    private const string StandardName = "Selçuk Standart Optik Form";
    private const string SampleLayoutName = "Örnek dosya düzeni (ornek_optik_cikti.txt)";

    [Fact]
    public async Task KK4_K11_TwoTemplatesOnlyOneDefault()
    {
        await using var setup = await CreateSetupAsync(seed: true);
        var templates = await setup.TemplateService(setup.AdminId, UserRole.Admin).GetAllAsync(default);
        Assert.Equal(2, templates.Count);
        Assert.Contains(templates, item => item.Name == StandardName);
        Assert.Contains(templates, item => item.Name == SampleLayoutName);
        Assert.Single(templates, item => item.IsDefault);
        Assert.True(templates.Single(item => item.Name == StandardName).IsDefault);
    }

    [Fact]
    public async Task KK4_K11_SetDefaultClearsPrevious()
    {
        await using var setup = await CreateSetupAsync(seed: true);
        var service = setup.TemplateService(setup.AdminId, UserRole.Admin);
        var sample = (await service.GetAllAsync(default)).Single(item => item.Name == SampleLayoutName);
        var result = await service.SetDefaultAsync(sample.Id, default);
        Assert.NotNull(result);
        Assert.True(result.IsDefault);
        var templates = await service.GetAllAsync(default);
        Assert.Single(templates, item => item.IsDefault);
        Assert.Equal(sample.Id, templates.Single(item => item.IsDefault).Id);
        Assert.Equal(1, await setup.Context.OpticalTemplates.AsNoTracking().CountAsync(item => item.IsDefault));
    }

    [Fact]
    public async Task KK4_K11_CreateAsDefaultClearsPrevious()
    {
        await using var setup = await CreateSetupAsync(seed: true);
        var service = setup.TemplateService(setup.AdminId, UserRole.Admin);
        var request = ShortLayout("Yeni varsayılan");
        request.IsDefault = true;
        var created = await service.CreateAsync(request, default);
        Assert.True(created.IsDefault);
        Assert.Equal(1, created.CurrentVersion.VersionNo);
        var templates = await service.GetAllAsync(default);
        Assert.Equal(3, templates.Count);
        Assert.Single(templates, item => item.IsDefault);
        Assert.Equal(created.Id, templates.Single(item => item.IsDefault).Id);
    }

    [Fact]
    public async Task KK4_Y6_10_EditCreatesNewVersionOldExamKeepsVersion()
    {
        await using var setup = await CreateSetupAsync(seed: false);
        var admin = setup.TemplateService(setup.AdminId, UserRole.Admin);
        var created = await admin.CreateAsync(ShortLayout("Kısa düzen"), default);
        var firstVersionId = created.CurrentVersion.Id;
        var examService = setup.ExamService(setup.InstructorId);
        var oldExam = await examService.CreateExamAsync(setup.CourseId, NewExam(created.Id), default);
        Assert.NotNull(oldExam);
        var edited = ShortLayout("Kısa düzen");
        edited.StudentNumberLength = 9;
        edited.BookletTypeStart = 9;
        edited.AnswersStart = 10;
        var updated = await admin.UpdateAsync(created.Id, edited, default);
        Assert.NotNull(updated);
        Assert.Equal(2, updated.CurrentVersion.VersionNo);
        Assert.NotEqual(firstVersionId, updated.CurrentVersion.Id);
        var versions = await setup.Context.OpticalTemplateVersions.AsNoTracking().Where(item => item.OpticalTemplateId == created.Id).OrderBy(item => item.VersionNo).ToListAsync();
        Assert.Equal(2, versions.Count);
        Assert.Equal(8, versions[0].StudentNumberLength);
        Assert.Equal(8, versions[0].BookletTypeStart);
        Assert.Equal(9, versions[0].AnswersStart);
        Assert.Equal(9, versions[1].StudentNumberLength);
        var oldExamRow = await setup.Context.Exams.AsNoTracking().SingleAsync(item => item.Id == oldExam.Id);
        Assert.Equal(firstVersionId, oldExamRow.OpticalTemplateVersionId);
        var newExam = await examService.CreateExamAsync(setup.CourseId, NewExam(created.Id), default);
        var newExamRow = await setup.Context.Exams.AsNoTracking().SingleAsync(item => item.Id == newExam!.Id);
        Assert.Equal(updated.CurrentVersion.Id, newExamRow.OpticalTemplateVersionId);
        var import = setup.ImportService(setup.InstructorId);
        var oldResult = await ImportShortLineAsync(import, oldExam.Id);
        Assert.Equal(1, oldResult.ValidOpticalRecordCount);
        Assert.Equal("00123456", oldResult.OpticalRecords.Single().StudentNumber);
        Assert.Equal("A", oldResult.OpticalRecords.Single().BookletType);
        Assert.Equal("ABCD", oldResult.OpticalRecords.Single().Answers);
        var newResult = await ImportShortLineAsync(import, newExam!.Id);
        var newRecord = newResult.OpticalRecords.Single();
        Assert.Equal("00123456A", newRecord.StudentNumber);
        Assert.NotEqual(oldResult.OpticalRecords.Single().StudentNumber, newRecord.StudentNumber);
        Assert.NotEqual(oldResult.OpticalRecords.Single().Answers, newRecord.Answers);
    }

    [Fact]
    public async Task KK4_K11_ExamWithoutTemplateGetsDefault()
    {
        await using var setup = await CreateSetupAsync(seed: true);
        var admin = setup.TemplateService(setup.AdminId, UserRole.Admin);
        var standard = (await admin.GetAllAsync(default)).Single(item => item.IsDefault);
        var exam = await setup.ExamService(setup.InstructorId).CreateExamAsync(setup.CourseId, NewExam(null), default);
        Assert.NotNull(exam);
        var row = await setup.Context.Exams.AsNoTracking().SingleAsync(item => item.Id == exam.Id);
        Assert.Equal(standard.CurrentVersion.Id, row.OpticalTemplateVersionId);
        var updated = await admin.UpdateAsync(standard.Id, ShortLayout(standard.Name), default);
        var later = await setup.ExamService(setup.InstructorId).CreateExamAsync(setup.CourseId, NewExam(null), default);
        var laterRow = await setup.Context.Exams.AsNoTracking().SingleAsync(item => item.Id == later!.Id);
        Assert.Equal(updated!.CurrentVersion.Id, laterRow.OpticalTemplateVersionId);
        Assert.Equal(standard.CurrentVersion.VersionNo + 1, updated.CurrentVersion.VersionNo);
    }

    [Fact]
    public async Task KK4_H1_ImportUsesExamTemplateNotClientConfig()
    {
        await using var setup = await CreateSetupAsync(seed: true);
        var templates = await setup.TemplateService(setup.AdminId, UserRole.Admin).GetAllAsync(default);
        var sample = templates.Single(item => item.Name == SampleLayoutName);
        var exam = await setup.ExamService(setup.InstructorId).CreateExamAsync(setup.CourseId, NewExam(sample.Id), default);
        var result = await ImportFixtureAsync(setup.ImportService(setup.InstructorId), exam!.Id);
        Assert.Equal(17, result.TotalOpticalRecordCount);
        Assert.Equal(17, result.ValidOpticalRecordCount);
        Assert.Equal(0, result.InvalidOpticalRecordCount);
        Assert.Equal(2, result.OpticalRecords.Count(item => item.IsAnswerKey));
        var student = result.OpticalRecords.Single(item => item.LineNumber == 3);
        Assert.Equal("866407542", student.StudentNumber);
        Assert.Equal("DECYUM OM", student.StudentName);
        Assert.Equal("A", student.BookletType);
        Assert.Equal(25, student.Answers.Length);
    }

    [Fact]
    public async Task KK4_H1_ImportReadsTemplateOfExamNotDefaultTemplate()
    {
        await using var setup = await CreateSetupAsync(seed: true);
        var templates = await setup.TemplateService(setup.AdminId, UserRole.Admin).GetAllAsync(default);
        var standard = templates.Single(item => item.Name == StandardName);
        var exam = await setup.ExamService(setup.InstructorId).CreateExamAsync(setup.CourseId, NewExam(standard.Id), default);
        var result = await ImportFixtureAsync(setup.ImportService(setup.InstructorId), exam!.Id);
        Assert.NotEqual("DECYUM OM", result.OpticalRecords.Single(item => item.LineNumber == 3).StudentName);
    }

    [Fact]
    public async Task KK4_K22_TemplateHasBlankAndDoubleMarkDefaults()
    {
        await using var setup = await CreateSetupAsync(seed: false);
        var admin = setup.TemplateService(setup.AdminId, UserRole.Admin);
        var request = new SaveOpticalTemplateRequest { Name = "Varsayılanlar" };
        Assert.Equal(' ', request.BlankMarkChar);
        Assert.Equal('*', request.DoubleMarkChar);
        Assert.Equal(OpticalEncodingMode.Auto, request.EncodingMode);
        var created = await admin.CreateAsync(ShortLayout("Varsayılanlar"), default);
        Assert.Equal(' ', created.CurrentVersion.BlankMarkChar);
        Assert.Equal('*', created.CurrentVersion.DoubleMarkChar);
        Assert.Equal(OpticalEncodingMode.Auto, created.CurrentVersion.EncodingMode);
        var row = await setup.Context.OpticalTemplateVersions.AsNoTracking().SingleAsync(item => item.Id == created.CurrentVersion.Id);
        Assert.Equal(' ', row.BlankMarkChar);
        Assert.Equal('*', row.DoubleMarkChar);
        Assert.Equal(OpticalEncodingMode.Auto, row.EncodingMode);
    }

    [Fact]
    public async Task KK4_K22_CustomBlankAndDoubleMarkCharsArePersisted()
    {
        await using var setup = await CreateSetupAsync(seed: false);
        var request = ShortLayout("Özel işaret");
        request.BlankMarkChar = '.';
        request.DoubleMarkChar = '#';
        var created = await setup.TemplateService(setup.AdminId, UserRole.Admin).CreateAsync(request, default);
        var row = await setup.Context.OpticalTemplateVersions.AsNoTracking().SingleAsync(item => item.Id == created.CurrentVersion.Id);
        Assert.Equal('.', row.BlankMarkChar);
        Assert.Equal('#', row.DoubleMarkChar);
    }

    [Fact]
    public async Task KK4_H8_Windows1254AcceptedOnFreshProcess()
    {
        await using var setup = await CreateSetupAsync(seed: false);
        var admin = setup.TemplateService(setup.AdminId, UserRole.Admin);
        var request = ShortLayout("Windows-1254");
        request.EncodingMode = OpticalEncodingMode.Windows1254;
        var created = await admin.CreateAsync(request, default);
        Assert.Equal(OpticalEncodingMode.Windows1254, created.CurrentVersion.EncodingMode);
        var row = await setup.Context.OpticalTemplateVersions.AsNoTracking().SingleAsync(item => item.Id == created.CurrentVersion.Id);
        Assert.Equal(OpticalEncodingMode.Windows1254, row.EncodingMode);
        var edited = ShortLayout("Windows-1254");
        edited.EncodingMode = OpticalEncodingMode.Utf8;
        var updated = await admin.UpdateAsync(created.Id, edited, default);
        Assert.Equal(OpticalEncodingMode.Utf8, updated!.CurrentVersion.EncodingMode);
    }

    [Fact]
    public async Task KK4_K11_SeedMatchesTemplateJsonValues()
    {
        await using var setup = await CreateSetupAsync(seed: true);
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(FixturePath("optik-sablonlari.json")));
        var expected = document.RootElement.GetProperty("sablonlar").EnumerateArray().Where(item => item.GetProperty("ad").GetString() is StandardName or SampleLayoutName).ToList();
        Assert.Equal(2, expected.Count);
        var templates = await setup.Context.OpticalTemplates.AsNoTracking().Include(item => item.Versions).ToListAsync();
        Assert.Equal(2, templates.Count);
        foreach (var item in expected)
        {
            var name = item.GetProperty("ad").GetString();
            var template = templates.Single(candidate => candidate.Name == name);
            var version = Assert.Single(template.Versions);
            Assert.Equal(1, version.VersionNo);
            Assert.Equal(item.GetProperty("studentNumberStart").GetInt32(), version.StudentNumberStart);
            Assert.Equal(item.GetProperty("studentNumberLength").GetInt32(), version.StudentNumberLength);
            Assert.Equal(item.GetProperty("studentNameStart").GetInt32(), version.StudentNameStart);
            Assert.Equal(item.GetProperty("studentNameLength").GetInt32(), version.StudentNameLength);
            Assert.Equal(item.GetProperty("bookletTypeStart").GetInt32(), version.BookletTypeStart);
            Assert.Equal(item.GetProperty("bookletTypeLength").GetInt32(), version.BookletTypeLength);
            Assert.Equal(item.GetProperty("examTypeStart").GetInt32(), version.ExamTypeStart);
            Assert.Equal(item.GetProperty("examTypeLength").GetInt32(), version.ExamTypeLength);
            Assert.Equal(item.GetProperty("answersStart").GetInt32(), version.AnswersStart);
            Assert.Equal(item.GetProperty("answersLength").GetInt32(), version.AnswersLength);
            Assert.Equal(' ', version.BlankMarkChar);
            Assert.Equal('*', version.DoubleMarkChar);
            Assert.Equal(OpticalEncodingMode.Auto, version.EncodingMode);
            Assert.Equal(item.TryGetProperty("varsayilan", out var isDefault) && isDefault.GetBoolean(), template.IsDefault);
        }
    }

    [Fact]
    public async Task KK4_K11_SeedIsIdempotent()
    {
        await using var setup = await CreateSetupAsync(seed: true);
        await new OpticalTemplateSeeder(setup.Context).SeedAsync(default);
        Assert.Equal(2, await setup.Context.OpticalTemplates.CountAsync());
        Assert.Equal(2, await setup.Context.OpticalTemplateVersions.CountAsync());
    }

    [Fact]
    public async Task KK4_K11_InstructorCanReadTemplates()
    {
        await using var setup = await CreateSetupAsync(seed: true);
        var templates = await setup.TemplateService(setup.InstructorId, UserRole.Instructor).GetAllAsync(default);
        Assert.Equal(2, templates.Count);
        Assert.Single(templates, item => item.IsDefault);
    }

    [Fact]
    public async Task KK4_K11_InstructorCannotWriteTemplates()
    {
        await using var setup = await CreateSetupAsync(seed: true);
        var instructor = setup.TemplateService(setup.InstructorId, UserRole.Instructor);
        var existing = (await instructor.GetAllAsync(default)).First(item => !item.IsDefault);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => instructor.CreateAsync(ShortLayout("Yetkisiz"), default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => instructor.UpdateAsync(existing.Id, ShortLayout(existing.Name), default));
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => instructor.SetDefaultAsync(existing.Id, default));
        Assert.Equal(2, await setup.Context.OpticalTemplates.CountAsync());
        Assert.Equal(2, await setup.Context.OpticalTemplateVersions.CountAsync());
        Assert.False((await setup.Context.OpticalTemplates.AsNoTracking().SingleAsync(item => item.Id == existing.Id)).IsDefault);
    }

    [Fact]
    public async Task KK4_K11_InstructorCannotBindTemplateToUnassignedCourseExam()
    {
        await using var setup = await CreateSetupAsync(seed: true);
        var templates = await setup.TemplateService(setup.AdminId, UserRole.Admin).GetAllAsync(default);
        var sample = templates.Single(item => item.Name == SampleLayoutName);
        var outsider = setup.TemplateService(setup.OutsiderId, UserRole.Instructor);
        var bound = await outsider.BindToExamAsync(setup.ExistingExamId, sample.Id, default);
        Assert.Null(bound);
        var examRow = await setup.Context.Exams.AsNoTracking().SingleAsync(item => item.Id == setup.ExistingExamId);
        Assert.Equal(setup.ExistingExamVersionId, examRow.OpticalTemplateVersionId);
        var created = await setup.ExamService(setup.OutsiderId).CreateExamAsync(setup.CourseId, NewExam(sample.Id), default);
        Assert.Null(created);
        Assert.Equal(1, await setup.Context.Exams.CountAsync());
    }

    [Fact]
    public async Task KK4_K11_AssignedInstructorCanBindTemplateToOwnCourseExam()
    {
        await using var setup = await CreateSetupAsync(seed: true);
        var templates = await setup.TemplateService(setup.AdminId, UserRole.Admin).GetAllAsync(default);
        var sample = templates.Single(item => item.Name == SampleLayoutName);
        var bound = await setup.TemplateService(setup.InstructorId, UserRole.Instructor).BindToExamAsync(setup.ExistingExamId, sample.Id, default);
        Assert.NotNull(bound);
        var examRow = await setup.Context.Exams.AsNoTracking().SingleAsync(item => item.Id == setup.ExistingExamId);
        Assert.Equal(sample.CurrentVersion.Id, examRow.OpticalTemplateVersionId);
    }

    [Fact]
    public async Task KK4_K11_AdminOpticalSettingsReadsDefaultTemplateCurrentVersion()
    {
        await using var setup = await CreateSetupAsync(seed: true);
        var standard = (await setup.TemplateService(setup.AdminId, UserRole.Admin).GetAllAsync(default)).Single(item => item.IsDefault);
        var settings = await setup.AdminService(setup.AdminId, UserRole.Admin).GetOpticalSettingsAsync(default);
        Assert.Equal(standard.CurrentVersion.StudentNumberStart, settings.StudentNumberStart);
        Assert.Equal(standard.CurrentVersion.StudentNumberLength, settings.StudentNumberLength);
        Assert.Equal(standard.CurrentVersion.StudentNameStart, settings.StudentNameStart);
        Assert.Equal(standard.CurrentVersion.StudentNameLength, settings.StudentNameLength);
        Assert.Equal(standard.CurrentVersion.BookletTypeStart, settings.BookletTypeStart);
        Assert.Equal(standard.CurrentVersion.BookletTypeLength, settings.BookletTypeLength);
        Assert.Equal(standard.CurrentVersion.ExamTypeStart, settings.ExamTypeStart);
        Assert.Equal(standard.CurrentVersion.ExamTypeLength, settings.ExamTypeLength);
        Assert.Equal(standard.CurrentVersion.AnswersStart, settings.AnswersStart);
        Assert.Equal(standard.CurrentVersion.AnswersLength, settings.AnswersLength);
        Assert.Equal("auto", settings.EncodingName, ignoreCase: true);
    }

    [Fact]
    public async Task KK4_Y6_10_AdminOpticalSettingsUpdateCreatesNewDefaultTemplateVersion()
    {
        await using var setup = await CreateSetupAsync(seed: true);
        var standard = (await setup.TemplateService(setup.AdminId, UserRole.Admin).GetAllAsync(default)).Single(item => item.IsDefault);
        var request = new UpdateOpticalSystemSettingsRequest
        {
            StudentNumberStart = 0,
            StudentNumberLength = 9,
            StudentNameStart = 10,
            StudentNameLength = 25,
            BookletTypeStart = 40,
            BookletTypeLength = 1,
            ExamTypeStart = 9,
            ExamTypeLength = 1,
            AnswersStart = 41,
            AnswersLength = 30,
            EncodingName = "utf-8"
        };
        var result = await setup.AdminService(setup.AdminId, UserRole.Admin).UpdateOpticalSettingsAsync(request, default);
        Assert.Equal(25, result.StudentNameLength);
        Assert.Equal("utf-8", result.EncodingName, ignoreCase: true);
        var versions = await setup.Context.OpticalTemplateVersions.AsNoTracking().Where(item => item.OpticalTemplateId == standard.Id).OrderBy(item => item.VersionNo).ToListAsync();
        Assert.Equal(2, versions.Count);
        Assert.Equal(standard.CurrentVersion.Id, versions[0].Id);
        Assert.Equal(standard.CurrentVersion.StudentNameLength, versions[0].StudentNameLength);
        Assert.Equal(OpticalEncodingMode.Auto, versions[0].EncodingMode);
        Assert.Equal(25, versions[1].StudentNameLength);
        Assert.Equal(OpticalEncodingMode.Utf8, versions[1].EncodingMode);
        Assert.Equal(2, await setup.Context.OpticalTemplates.CountAsync());
        var exam = await setup.Context.Exams.AsNoTracking().SingleAsync(item => item.Id == setup.ExistingExamId);
        Assert.Equal(standard.CurrentVersion.Id, exam.OpticalTemplateVersionId);
    }

    [Fact]
    public async Task KK4_H8_AdminOpticalSettingsAcceptsWindows1254()
    {
        await using var setup = await CreateSetupAsync(seed: true);
        var request = new UpdateOpticalSystemSettingsRequest
        {
            StudentNumberStart = 0,
            StudentNumberLength = 9,
            BookletTypeStart = 9,
            BookletTypeLength = 1,
            AnswersStart = 10,
            AnswersLength = 25,
            EncodingName = "windows-1254"
        };
        var admin = setup.AdminService(setup.AdminId, UserRole.Admin);
        var result = await admin.UpdateOpticalSettingsAsync(request, default);
        Assert.Equal("windows-1254", result.EncodingName, ignoreCase: true);
        var latest = await setup.Context.OpticalTemplates.AsNoTracking().Where(item => item.IsDefault).SelectMany(item => item.Versions).OrderByDescending(item => item.VersionNo).FirstAsync();
        Assert.Equal(OpticalEncodingMode.Windows1254, latest.EncodingMode);
        request.EncodingName = "auto";
        var auto = await admin.UpdateOpticalSettingsAsync(request, default);
        Assert.Equal("auto", auto.EncodingName, ignoreCase: true);
    }

    [Fact]
    public async Task KK4_K11_InstructorCannotUpdateAdminOpticalSettings()
    {
        await using var setup = await CreateSetupAsync(seed: true);
        var request = new UpdateOpticalSystemSettingsRequest
        {
            StudentNumberStart = 0,
            StudentNumberLength = 9,
            BookletTypeStart = 9,
            BookletTypeLength = 1,
            AnswersStart = 10,
            AnswersLength = 25,
            EncodingName = "utf-8"
        };
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => setup.AdminService(setup.InstructorId, UserRole.Instructor).UpdateOpticalSettingsAsync(request, default));
        Assert.Equal(2, await setup.Context.OpticalTemplateVersions.CountAsync());
    }

    private static CreateExamRequest NewExam(Guid? templateId)
    {
        return new CreateExamRequest { ExamType = ExamType.Midterm, EvaluationType = EvaluationType.MultipleChoice, OpticalTemplateId = templateId };
    }

    private static SaveOpticalTemplateRequest ShortLayout(string name)
    {
        return new SaveOpticalTemplateRequest
        {
            Name = name,
            StudentNumberStart = 0,
            StudentNumberLength = 8,
            BookletTypeStart = 8,
            BookletTypeLength = 1,
            AnswersStart = 9,
            AnswersLength = 4
        };
    }

    private static async Task<ImportPreviewResponse> ImportShortLineAsync(StudentDataImportService service, Guid examId)
    {
        await using var optical = new MemoryStream(Encoding.UTF8.GetBytes("00123456AABCD\n"));
        await using var classList = File.OpenRead(FixturePath("sinif-listesi-ornek-anonim.xlsx"));
        return await service.ImportAsync(examId, optical, "optik.txt", [new ClassListFileInput(classList, "sinif.xlsx")], default);
    }

    [Fact]
    public async Task KK4_K12_ExamTypeMismatchWarnsButImports()
    {
        await using var setup = await CreateSetupAsync(seed: true);
        var sample = (await setup.TemplateService(setup.AdminId, UserRole.Admin).GetAllAsync(default)).Single(item => item.Name == SampleLayoutName);
        var midterm = await setup.ExamService(setup.InstructorId).CreateExamAsync(setup.CourseId, NewExam(sample.Id), default);
        var matching = await ImportFixtureAsync(setup.ImportService(setup.InstructorId), midterm!.Id);
        Assert.Empty(matching.Warnings);
        var finalExam = await setup.ExamService(setup.InstructorId).CreateExamAsync(setup.CourseId, new CreateExamRequest { ExamType = ExamType.Final, EvaluationType = EvaluationType.MultipleChoice, OpticalTemplateId = sample.Id }, default);
        var mismatched = await ImportFixtureAsync(setup.ImportService(setup.InstructorId), finalExam!.Id);
        var warning = Assert.Single(mismatched.Warnings);
        Assert.Contains("15 optik satırında", warning.Message);
        Assert.Equal(17, mismatched.ValidOpticalRecordCount);
        Assert.DoesNotContain(mismatched.Issues, item => item.Source == "examType");
        Assert.Equal(17, await setup.Context.OpticalExamRecords.CountAsync(item => item.ExamImport.ExamId == finalExam.Id));
        Assert.Equal(15, await setup.Context.OpticalExamRecords.CountAsync(item => item.ExamImport.ExamId == finalExam.Id && item.ExamTypeValue == "1"));
    }

    [Fact]
    public async Task KK4_K11_BindTemplateAllowedBeforeImport()
    {
        await using var setup = await CreateSetupAsync(seed: true);
        var templates = await setup.TemplateService(setup.AdminId, UserRole.Admin).GetAllAsync(default);
        var exam = await setup.ExamService(setup.InstructorId).CreateExamAsync(setup.CourseId, NewExam(templates.Single(item => item.Name == StandardName).Id), default);
        var sample = templates.Single(item => item.Name == SampleLayoutName);
        var bound = await setup.TemplateService(setup.InstructorId, UserRole.Instructor).BindToExamAsync(exam!.Id, sample.Id, default);
        Assert.Equal(sample.CurrentVersion.Id, bound!.OpticalTemplateVersionId);
    }

    [Fact]
    public async Task KK4_Y6_10_BindTemplateRejectedWhenOpticalRecordsStored()
    {
        await using var setup = await CreateSetupAsync(seed: true);
        var templates = await setup.TemplateService(setup.AdminId, UserRole.Admin).GetAllAsync(default);
        var sample = templates.Single(item => item.Name == SampleLayoutName);
        var standard = templates.Single(item => item.Name == StandardName);
        var exam = await setup.ExamService(setup.InstructorId).CreateExamAsync(setup.CourseId, NewExam(sample.Id), default);
        await ImportFixtureAsync(setup.ImportService(setup.InstructorId), exam!.Id);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => setup.TemplateService(setup.InstructorId, UserRole.Instructor).BindToExamAsync(exam.Id, standard.Id, default));
        Assert.Contains("optik dosya", error.Message);
        var stored = await setup.Context.Exams.AsNoTracking().SingleAsync(item => item.Id == exam.Id);
        Assert.Equal(sample.CurrentVersion.Id, stored.OpticalTemplateVersionId);
    }

    [Fact]
    public async Task KK4_Y6_10_BindTemplateRejectedWhenExamCompleted()
    {
        await using var setup = await CreateSetupAsync(seed: true);
        var templates = await setup.TemplateService(setup.AdminId, UserRole.Admin).GetAllAsync(default);
        var sample = templates.Single(item => item.Name == SampleLayoutName);
        var standard = templates.Single(item => item.Name == StandardName);
        var exam = await setup.ExamService(setup.InstructorId).CreateExamAsync(setup.CourseId, NewExam(standard.Id), default);
        var tracked = await setup.Context.Exams.SingleAsync(item => item.Id == exam!.Id);
        tracked.Status = ExamStatus.Completed;
        await setup.Context.SaveChangesAsync();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => setup.TemplateService(setup.InstructorId, UserRole.Instructor).BindToExamAsync(exam!.Id, sample.Id, default));
        Assert.Contains("Tamamlanmış", error.Message);
        var stored = await setup.Context.Exams.AsNoTracking().SingleAsync(item => item.Id == exam.Id);
        Assert.Equal(standard.CurrentVersion.Id, stored.OpticalTemplateVersionId);
    }

    [Fact]
    public async Task KK4_Y6_10_BindTemplateAllowedAfterOpticalFileRemoved()
    {
        await using var setup = await CreateSetupAsync(seed: true);
        var templates = await setup.TemplateService(setup.AdminId, UserRole.Admin).GetAllAsync(default);
        var sample = templates.Single(item => item.Name == SampleLayoutName);
        var standard = templates.Single(item => item.Name == StandardName);
        var exam = await setup.ExamService(setup.InstructorId).CreateExamAsync(setup.CourseId, NewExam(sample.Id), default);
        var importService = setup.ImportService(setup.InstructorId);
        await ImportFixtureAsync(importService, exam!.Id);
        Assert.True(await importService.RemoveAsync(exam.Id, "optical", null, default));
        var import = await setup.Context.ExamImports.AsNoTracking().SingleAsync(item => item.ExamId == exam.Id);
        Assert.Equal(string.Empty, import.OpticalFileName);
        Assert.NotEmpty(import.ClassListFileName);
        var bound = await setup.TemplateService(setup.InstructorId, UserRole.Instructor).BindToExamAsync(exam.Id, standard.Id, default);
        Assert.Equal(standard.CurrentVersion.Id, bound!.OpticalTemplateVersionId);
    }

    private static async Task<ImportPreviewResponse> ImportFixtureAsync(StudentDataImportService service, Guid examId)
    {
        await using var optical = File.OpenRead(FixturePath("optik-ornek-anonim.txt"));
        await using var classList = File.OpenRead(FixturePath("sinif-listesi-ornek-anonim.xlsx"));
        return await service.ImportAsync(examId, optical, "optik-ornek-anonim.txt", [new ClassListFileInput(classList, "sinif-listesi-ornek-anonim.xlsx")], default);
    }

    private static string FixturePath(string name)
    {
        return Path.Combine(AppContext.BaseDirectory, "Fixtures", name);
    }

    private static async Task<TestSetup> CreateSetupAsync(bool seed)
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        var context = new SdsDbContext(new DbContextOptionsBuilder<SdsDbContext>().UseSqlite(connection).Options);
        await context.Database.EnsureCreatedAsync();
        var admin = new User { Id = Guid.NewGuid(), Username = "admin-" + Guid.NewGuid(), DisplayName = "Yönetici", Role = UserRole.Admin, CreatedAt = DateTime.UtcNow };
        var instructor = new User { Id = Guid.NewGuid(), Username = "ogretmen-" + Guid.NewGuid(), DisplayName = "Öğretim Elemanı", Role = UserRole.Instructor, CreatedAt = DateTime.UtcNow };
        var outsider = new User { Id = Guid.NewGuid(), Username = "dis-" + Guid.NewGuid(), DisplayName = "Atanmamış", Role = UserRole.Instructor, CreatedAt = DateTime.UtcNow };
        var year = new AcademicYear { Id = Guid.NewGuid(), Name = Guid.NewGuid().ToString(), IsActive = true };
        var course = new Course { Id = Guid.NewGuid(), Code = "OPT" + Guid.NewGuid().ToString("N")[..6], Name = "Ders", AcademicYearId = year.Id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        context.AddRange(admin, instructor, outsider, year, course);
        context.CourseInstructors.Add(new CourseInstructor { CourseId = course.Id, UserId = instructor.Id });
        await context.SaveChangesAsync();
        var setup = new TestSetup(context, connection, admin.Id, instructor.Id, outsider.Id, course.Id);
        if (!seed) return setup;
        await new OpticalTemplateSeeder(context).SeedAsync(default);
        var defaultVersion = await context.OpticalTemplates.AsNoTracking().Where(item => item.IsDefault).SelectMany(item => item.Versions).SingleAsync();
        var exam = new Exam { Id = Guid.NewGuid(), CourseId = course.Id, ExamType = ExamType.Final, EvaluationType = EvaluationType.MultipleChoice, OpticalTemplateVersionId = defaultVersion.Id, CreatedAt = DateTime.UtcNow };
        context.Exams.Add(exam);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        return setup with { ExistingExamId = exam.Id, ExistingExamVersionId = defaultVersion.Id };
    }

    private sealed record TestSetup(SdsDbContext Context, SqliteConnection Connection, Guid AdminId, Guid InstructorId, Guid OutsiderId, Guid CourseId) : IAsyncDisposable
    {
        public Guid ExistingExamId { get; init; }
        public Guid ExistingExamVersionId { get; init; }

        public OpticalTemplateService TemplateService(Guid userId, UserRole role)
        {
            return new OpticalTemplateService(Context, new TestCurrentUser(userId, role));
        }

        public AdminService AdminService(Guid userId, UserRole role)
        {
            return new AdminService(Context, new TestCurrentUser(userId, role));
        }

        public ExamService ExamService(Guid userId)
        {
            return new ExamService(Context, new TestCurrentUser(userId, UserRole.Instructor));
        }

        public StudentDataImportService ImportService(Guid userId)
        {
            return new StudentDataImportService(Context, new TestCurrentUser(userId, UserRole.Instructor), new OpticalFileParser(), new ClassListParser());
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await Connection.DisposeAsync();
        }
    }

    private sealed class TestCurrentUser(Guid userId, UserRole role) : ICurrentUser
    {
        public CurrentUser Get() => new(userId, role.ToString());
    }
}
