// Yeniden hesaplama, 100 dışı toplam puan, çift işaret ve kaynak ayrımı puanlama kurallarını doğrular.
using System;
using System.Linq;
using System.Threading.Tasks;
using SDS.Application.Grades;
using SDS.Domain.Entities;
using Xunit;

namespace SDS.Tests;

public sealed class GradeScoringTests
{
    [Fact]
    public async Task KK7_H9_RecalculationUpdatesUnadjustedScores()
    {
        await using var fixture = await ScoringFixture.CreateAsync();
        await fixture.SaveKeyAsync(ScoringFixture.KeyRequest(100, 4, ("A", "AAAA")));
        await fixture.AddStudentAsync("001", "A", "AAAA");
        var first = await fixture.CalculateAsync();
        Assert.Equal(100m, first["001"].AdjustedScore);
        await fixture.SaveKeyAsync(ScoringFixture.KeyRequest(100, 4, ("A", "AABB")));
        var second = await fixture.CalculateAsync();
        Assert.Equal(50m, second["001"].RawScore);
        Assert.Equal(second["001"].RawScore, second["001"].AdjustedScore);
        Assert.False(second["001"].IsManuallyAdjusted);
    }

    [Fact]
    public async Task KK7_H9_RecalculationKeepsManualOverride()
    {
        await using var fixture = await ScoringFixture.CreateAsync();
        await fixture.SaveKeyAsync(ScoringFixture.KeyRequest(100, 4, ("A", "AAAA")));
        var overridden = await fixture.AddStudentAsync("001", "A", "AAAA");
        await fixture.AddStudentAsync("002", "A", "AAAA");
        await fixture.CalculateAsync();
        await fixture.Grades.UpdateAsync(fixture.ExamId, overridden, new UpdateGradeRequest { AdjustedScore = 70 }, default);
        await fixture.SaveKeyAsync(ScoringFixture.KeyRequest(100, 4, ("A", "AABB")));
        var response = await fixture.Grades.CalculateAsync(fixture.ExamId, default);
        var rows = response!.Rows.ToDictionary(item => item.StudentNumber);
        Assert.Equal(70m, rows["001"].AdjustedScore);
        Assert.Equal(50m, rows["001"].RawScore);
        Assert.True(rows["001"].IsManuallyAdjusted);
        Assert.Equal(50m, rows["002"].AdjustedScore);
        Assert.False(rows["002"].IsManuallyAdjusted);
        Assert.Equal(new[] { overridden }, response.ManuallyAdjustedStudentIds);
    }

    [Fact]
    public async Task Y3_16_K7_RawScoreNotClampedAbove100()
    {
        await using var fixture = await ScoringFixture.CreateAsync();
        await fixture.SaveKeyAsync(ScoringFixture.KeyRequest(120, 4, ("A", "AAAA")));
        await fixture.AddStudentAsync("001", "A", "AAAA");
        var rows = await fixture.CalculateAsync();
        Assert.Equal(120m, rows["001"].RawScore);
        Assert.Equal(100m, rows["001"].ScaledScore);
        Assert.Equal(100m, rows["001"].AdjustedScore);
    }

    [Fact]
    public async Task Y3_16_K7_ScaledScoreIs100Based()
    {
        await using var fixture = await ScoringFixture.CreateAsync();
        await fixture.SaveKeyAsync(ScoringFixture.KeyRequest(120, 4, ("A", "AAAA")));
        await fixture.AddStudentAsync("001", "A", "AABB");
        var rows = await fixture.CalculateAsync();
        Assert.Equal(60m, rows["001"].RawScore);
        Assert.Equal(50m, rows["001"].ScaledScore);
        Assert.Equal(50m, rows["001"].AdjustedScore);
    }

    [Fact]
    public async Task Y3_16_K7_FinalScoreClampedTo100WithBonus()
    {
        await using var fixture = await ScoringFixture.CreateAsync();
        await fixture.SaveKeyAsync(ScoringFixture.KeyRequest(120, 4, ("A", "AAAA")));
        var studentId = await fixture.AddStudentAsync("001", "A", "AAAB");
        await fixture.CalculateAsync();
        var response = await fixture.Grades.AddBonusAsync(fixture.ExamId, new BonusRequest { ImportedStudentIds = [studentId], Amount = 40, IdempotencyKey = "bonus-1" }, default);
        var row = response!.Rows.Single();
        Assert.Equal(90m, row.RawScore);
        Assert.Equal(100m, row.AdjustedScore);
        var recalculated = (await fixture.CalculateAsync())["001"];
        Assert.Equal(90m, recalculated.RawScore);
        Assert.Equal(100m, recalculated.AdjustedScore);
    }

    [Fact]
    public async Task KK4_K22_DoubleMarkScoresZero()
    {
        await using var fixture = await ScoringFixture.CreateAsync();
        await fixture.SaveKeyAsync(ScoringFixture.KeyRequest(100, 4, ("A", "AAAA")));
        await fixture.AddStudentAsync("001", "A", "A*A ");
        var rows = await fixture.CalculateAsync();
        Assert.Equal(50m, rows["001"].RawScore);
    }

    [Fact]
    public async Task R12_1_MaximumScoreDistributesQuestionPoints()
    {
        await using var fixture = await ScoringFixture.CreateAsync();
        var response = await fixture.SaveKeyAsync(ScoringFixture.KeyRequest(120, 4, ("A", "AAAA")));
        Assert.Equal(120m, response.MaximumScore);
        Assert.All(response.Questions, item => Assert.Equal(30m, item.Points));
        Assert.All(response.Booklets.Single().Questions, item => Assert.Equal(30m, item.Points));
    }

    [Fact]
    public async Task Y3_16_K24_ManualAndOpticalSourcesAreSummed()
    {
        await using var fixture = await ScoringFixture.CreateAsync();
        var request = ScoringFixture.KeyRequest(100, 4, ("A", "AAAA"));
        request.Questions.Single(item => item.CanonicalQuestionNumber == 4).Source = QuestionScoreSource.Manual;
        await fixture.SaveKeyAsync(request);
        await fixture.AddStudentAsync("001", "A", "AAAA");
        var first = await fixture.CalculateAsync();
        Assert.Equal(75m, first["001"].RawScore);
        var result = fixture.Context.ExamResults.Single();
        result.ManualScore = 20m;
        await fixture.Context.SaveChangesAsync();
        var second = await fixture.CalculateAsync();
        Assert.Equal(95m, second["001"].RawScore);
        Assert.Equal(95m, second["001"].AdjustedScore);
    }

    [Fact]
    public async Task K9_CancelledQuestionPoliciesApplyPerQuestion()
    {
        await using var fixture = await ScoringFixture.CreateAsync();
        var request = ScoringFixture.KeyRequest(100, 4, ("A", "AAAA"));
        request.Booklets.Single().Questions[3].Status = ExamQuestionStatus.Cancelled;
        await fixture.SaveKeyAsync(request);
        await fixture.AddStudentAsync("001", "A", "AAAB");
        await fixture.AddStudentAsync("002", "A", "AABB");
        var awardFull = await fixture.CalculateAsync();
        Assert.Equal(100m, awardFull["001"].RawScore);
        Assert.Equal(75m, awardFull["002"].RawScore);
        request.Booklets.Single().Questions[3].CancellationPolicy = QuestionCancellationPolicy.DeductAndScale;
        await fixture.SaveKeyAsync(request);
        var deducted = await fixture.CalculateAsync();
        Assert.Equal(100m, deducted["001"].RawScore);
        Assert.Equal(66.67m, deducted["002"].RawScore);
    }

    [Fact]
    public async Task Grades_ForUnassignedInstructor_ReturnsNull()
    {
        await using var fixture = await ScoringFixture.CreateAsync();
        var service = new SDS.Infrastructure.Services.GradeService(fixture.Context, fixture.Outsider);
        Assert.Null(await service.GetAsync(fixture.ExamId, default));
    }
}
