// Kitapçığın kendi anahtarıyla puanlanmasını, analiz eşlemesini ve soru iptal önerisi akışını doğrular.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SDS.Application.AnswerKeys;
using SDS.Application.LearningOutcomes;
using SDS.Domain.Entities;
using SDS.Infrastructure.Services;
using Xunit;

namespace SDS.Tests;

public sealed class BookletScoringTests
{
    [Fact]
    public async Task KK4_Y3_19_DifferentBookletOrdersScoreEqually_H15()
    {
        await using var fixture = await ScoringFixture.CreateAsync();
        await fixture.SaveKeyAsync(ScoringFixture.KeyRequest(100, 4, ("A", "ABCD", new[] { 1, 2, 3, 4 }), ("B", "CADB", new[] { 3, 1, 4, 2 })));
        await fixture.AddStudentAsync("001", "A", "AACD");
        await fixture.AddStudentAsync("002", "B", "CADA");
        var rows = await fixture.CalculateAsync();
        Assert.Equal(75m, rows["001"].RawScore);
        Assert.Equal(rows["001"].RawScore, rows["002"].RawScore);
    }

    [Fact]
    public async Task K23_ScoringWorksWithoutCrossBookletMapping()
    {
        await using var fixture = await ScoringFixture.CreateAsync();
        await fixture.SaveKeyAsync(ScoringFixture.KeyRequest(100, 4, ("A", "ABCD"), ("B", "DCBA")));
        await fixture.AddStudentAsync("001", "B", "DCBA");
        await fixture.AddStudentAsync("002", "B", "DCBB");
        var rows = await fixture.CalculateAsync();
        Assert.Equal(100m, rows["001"].RawScore);
        Assert.Equal(75m, rows["002"].RawScore);
        Assert.Equal(0, await fixture.Context.BookletQuestionMappings.CountAsync());
    }

    [Fact]
    public async Task K23_MappingChangeDoesNotChangeScores()
    {
        await using var fixture = await ScoringFixture.CreateAsync();
        await fixture.SaveKeyAsync(ScoringFixture.KeyRequest(100, 4, ("A", "AAAA"), ("B", "AAAA")));
        await fixture.AddStudentAsync("001", "A", "AAAB");
        await fixture.AddStudentAsync("002", "B", "ABBB");
        var before = await fixture.CalculateAsync();
        var exam = await fixture.Context.Exams.SingleAsync(item => item.Id == fixture.ExamId);
        exam.Status = ExamStatus.Completed;
        await fixture.Context.SaveChangesAsync();
        var analysisBefore = await fixture.Analysis.GetAsync(fixture.ExamId, null, default);
        await fixture.Outcomes.ApplyReverseMappingAsync(fixture.ExamId, new ReverseBookletMappingRequest { BookletCode = "B" }, default);
        var after = await fixture.CalculateAsync();
        var analysisAfter = await fixture.Analysis.GetAsync(fixture.ExamId, null, default);
        Assert.Equal(before["001"].RawScore, after["001"].RawScore);
        Assert.Equal(before["002"].RawScore, after["002"].RawScore);
        Assert.Equal(100m, analysisBefore!.Questions.Single(item => item.QuestionNumber == 1).SuccessRate);
        Assert.Equal(50m, analysisAfter!.Questions.Single(item => item.QuestionNumber == 1).SuccessRate);
    }

    [Fact]
    public async Task K23_DefaultMappingIsSameOrder()
    {
        await using var fixture = await ScoringFixture.CreateAsync();
        await fixture.SaveKeyAsync(ScoringFixture.KeyRequest(100, 4, ("A", "ABCD"), ("B", "DCBA")));
        var page = await fixture.Outcomes.GetExamPageAsync(fixture.ExamId, default);
        var mapping = Assert.Single(page!.BookletMappings);
        Assert.Equal("B", mapping.BookletCode);
        Assert.Equal(new[] { (1, 1), (2, 2), (3, 3), (4, 4) }, mapping.Items.Select(item => (item.ReferenceQuestionNumber, item.BookletQuestionNumber)).ToArray());
    }

    [Fact]
    public async Task K23_ReverseShortcutMapsReversed()
    {
        await using var fixture = await ScoringFixture.CreateAsync();
        await fixture.SaveKeyAsync(ScoringFixture.KeyRequest(100, 4, ("A", "ABCD"), ("B", "DCBA")));
        var page = await fixture.Outcomes.ApplyReverseMappingAsync(fixture.ExamId, new ReverseBookletMappingRequest { BookletCode = "b" }, default);
        var mapping = Assert.Single(page!.BookletMappings);
        Assert.Equal(new[] { (1, 4), (2, 3), (3, 2), (4, 1) }, mapping.Items.Select(item => (item.ReferenceQuestionNumber, item.BookletQuestionNumber)).ToArray());
        Assert.Equal(4, await fixture.Context.BookletQuestionMappings.CountAsync());
        await Assert.ThrowsAsync<ArgumentException>(() => fixture.Outcomes.ApplyReverseMappingAsync(fixture.ExamId, new ReverseBookletMappingRequest { BookletCode = "A" }, default));
    }

    [Fact]
    public async Task K23_UnlimitedBookletsSupported()
    {
        await using var fixture = await ScoringFixture.CreateAsync();
        var codes = new[] { "A", "B", "C", "D", "E", "F", "G", "H" };
        var keys = codes.Select((code, index) => (code, new string((char)('A' + index % 4), 4))).ToArray();
        var response = await fixture.SaveKeyAsync(ScoringFixture.KeyRequest(100, 4, keys));
        Assert.Equal(8, response.Booklets.Count);
        foreach (var code in codes) await fixture.AddStudentAsync($"10{code}", code, new string((char)('A' + Array.IndexOf(codes, code) % 4), 4));
        var rows = await fixture.CalculateAsync();
        Assert.All(rows.Values, item => Assert.Equal(100m, item.RawScore));
        foreach (var code in codes.Skip(1)) await fixture.Outcomes.ApplyReverseMappingAsync(fixture.ExamId, new ReverseBookletMappingRequest { BookletCode = code }, default);
        var page = await fixture.Outcomes.GetExamPageAsync(fixture.ExamId, default);
        Assert.Equal(7, page!.BookletMappings.Count);
    }

    [Fact]
    public async Task K26_CancelledQuestionIndependentOfMapping()
    {
        await using var fixture = await ScoringFixture.CreateAsync();
        var request = ScoringFixture.KeyRequest(100, 4, ("A", "AAAA"), ("B", "AAAA"));
        request.Booklets.Single(item => item.BookletCode == "B").Questions[1].Status = ExamQuestionStatus.Cancelled;
        await fixture.SaveKeyAsync(request);
        await fixture.AddStudentAsync("001", "A", "AABB");
        await fixture.AddStudentAsync("002", "B", "ABBB");
        var before = await fixture.CalculateAsync();
        Assert.Equal(50m, before["001"].RawScore);
        Assert.Equal(50m, before["002"].RawScore);
        await fixture.Outcomes.ApplyReverseMappingAsync(fixture.ExamId, new ReverseBookletMappingRequest { BookletCode = "B" }, default);
        var after = await fixture.CalculateAsync();
        Assert.Equal(before["001"].RawScore, after["001"].RawScore);
        Assert.Equal(before["002"].RawScore, after["002"].RawScore);
        var key = await fixture.AnswerKeys.GetAsync(fixture.ExamId, default);
        var bookletB = key!.Booklets.Single(item => item.BookletCode == "B");
        Assert.Equal(ExamQuestionStatus.Cancelled, bookletB.Questions.Single(item => item.DisplayQuestionNumber == 2).Status);
        Assert.Equal(ExamQuestionStatus.Active, bookletB.Questions.Single(item => item.DisplayQuestionNumber == 3).Status);
    }

    [Fact]
    public async Task K26_CancellingCreatesSuggestionsForOtherBooklets()
    {
        await using var fixture = await ScoringFixture.CreateAsync();
        await fixture.SaveKeyAsync(ScoringFixture.KeyRequest(100, 4, ("A", "AAAA"), ("B", "AAAA"), ("C", "AAAA")));
        await fixture.Outcomes.ApplyReverseMappingAsync(fixture.ExamId, new ReverseBookletMappingRequest { BookletCode = "B" }, default);
        var request = ScoringFixture.KeyRequest(100, 4, ("A", "AAAA"), ("B", "AAAA"), ("C", "AAAA"));
        request.Booklets.Single(item => item.BookletCode == "A").Questions[0].Status = ExamQuestionStatus.Cancelled;
        var response = await fixture.SaveKeyAsync(request);
        Assert.Equal(2, response.PendingCancellationSuggestionCount);
        var targets = response.CancellationSuggestions.Select(item => (item.SourceBookletCode, item.SourceQuestionNumber, item.TargetBookletCode, item.TargetQuestionNumber, item.Status)).OrderBy(item => item.TargetBookletCode).ToArray();
        Assert.Equal(new[] { ("A", 1, "B", 4, CancellationSuggestionStatus.Pending), ("A", 1, "C", 1, CancellationSuggestionStatus.Pending) }, targets);
        Assert.All(response.Booklets.Where(item => item.BookletCode != "A"), booklet => Assert.All(booklet.Questions, question => Assert.Equal(ExamQuestionStatus.Active, question.Status)));
    }

    [Fact]
    public async Task K26_SuggestionDoesNotChangeScoresUntilApproved()
    {
        await using var fixture = await ScoringFixture.CreateAsync();
        await SetUpPendingSuggestionAsync(fixture);
        var rows = await fixture.CalculateAsync();
        Assert.Equal(100m, rows["001"].RawScore);
        Assert.Equal(0m, rows["002"].RawScore);
        var grades = await fixture.Grades.GetAsync(fixture.ExamId, default);
        Assert.Equal(1, grades!.PendingCancellationSuggestionCount);
    }

    [Fact]
    public async Task K26_CompleteExamBlockedWhilePendingCancellationSuggestions()
    {
        await using var fixture = await ScoringFixture.CreateAsync();
        await SetUpPendingSuggestionAsync(fixture);
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Outcomes.CompleteExamAsync(fixture.ExamId, new CompleteExamRequest(), default));
        Assert.Contains("B kitapçığı 1. soru", exception.Message);
        Assert.Equal(ExamStatus.Draft, await fixture.Context.Exams.Where(item => item.Id == fixture.ExamId).Select(item => item.Status).SingleAsync());
    }

    [Fact]
    public async Task K26_ApprovedSuggestionCancelsAndRecalculates()
    {
        await using var fixture = await ScoringFixture.CreateAsync();
        await SetUpPendingSuggestionAsync(fixture);
        await fixture.CalculateAsync();
        var suggestion = (await fixture.Suggestions.GetAsync(fixture.ExamId, default))!.Items.Single();
        var response = await fixture.Suggestions.ApproveAsync(fixture.ExamId, suggestion.Id, default);
        Assert.Equal(0, response!.PendingCount);
        Assert.Equal(CancellationSuggestionStatus.Approved, response.Items.Single().Status);
        var key = await fixture.AnswerKeys.GetAsync(fixture.ExamId, default);
        Assert.Equal(ExamQuestionStatus.Cancelled, key!.Booklets.Single(item => item.BookletCode == "B").Questions.Single(item => item.DisplayQuestionNumber == 1).Status);
        var grades = await fixture.Grades.GetAsync(fixture.ExamId, default);
        Assert.Equal(25m, grades!.Rows.Single(item => item.StudentNumber == "002").RawScore);
        Assert.Equal(0, grades.PendingCancellationSuggestionCount);
    }

    [Fact]
    public async Task K26_RejectedSuggestionKeepsQuestionActive()
    {
        await using var fixture = await ScoringFixture.CreateAsync();
        await SetUpPendingSuggestionAsync(fixture);
        await fixture.CalculateAsync();
        var suggestion = (await fixture.Suggestions.GetAsync(fixture.ExamId, default))!.Items.Single();
        var response = await fixture.Suggestions.RejectAsync(fixture.ExamId, suggestion.Id, default);
        Assert.Equal(0, response!.PendingCount);
        Assert.Equal(CancellationSuggestionStatus.Rejected, response.Items.Single().Status);
        var key = await fixture.AnswerKeys.GetAsync(fixture.ExamId, default);
        Assert.Equal(ExamQuestionStatus.Active, key!.Booklets.Single(item => item.BookletCode == "B").Questions.Single(item => item.DisplayQuestionNumber == 1).Status);
        var rows = await fixture.CalculateAsync();
        Assert.Equal(0m, rows["002"].RawScore);
        var resaved = await fixture.SaveKeyAsync(CancelledRequest());
        Assert.Equal(0, resaved.PendingCancellationSuggestionCount);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Suggestions.ApproveAsync(fixture.ExamId, suggestion.Id, default));
    }

    [Fact]
    public async Task K26_CompleteExamSucceedsWhenAllSuggestionsResolved()
    {
        await using var fixture = await ScoringFixture.CreateAsync();
        await SetUpPendingSuggestionAsync(fixture);
        await fixture.CalculateAsync();
        var outcome = await fixture.Outcomes.CreateOutcomeAsync(fixture.CourseId, new CreateLearningOutcomeRequest { Description = "Temel kavramları uygular" }, default);
        var request = new CompleteExamRequest
        {
            BookletMappings = new SaveBookletQuestionMappingsRequest { Mappings = [new BookletMappingInput { ReferenceBookletCode = "A", BookletCode = "B", Items = Enumerable.Range(1, 4).Select(number => new BookletQuestionMappingItem(number, number)).ToList() }] },
            LearningOutcomeMappings = new SaveLearningOutcomeMappingsRequest { Mappings = [new LearningOutcomeQuestionMapInput { LearningOutcomeId = outcome!.Id, QuestionNumbers = [1, 2] }] }
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Outcomes.CompleteExamAsync(fixture.ExamId, request, default));
        var suggestion = (await fixture.Suggestions.GetAsync(fixture.ExamId, default))!.Items.Single();
        await fixture.Suggestions.RejectAsync(fixture.ExamId, suggestion.Id, default);
        var page = await fixture.Outcomes.CompleteExamAsync(fixture.ExamId, request, default);
        Assert.True(page!.IsComplete);
    }

    [Fact]
    public async Task KK5_H10_ResaveAnswerKeyAfterBookletMapping()
    {
        await using var fixture = await ScoringFixture.CreateAsync();
        await fixture.SaveKeyAsync(ScoringFixture.KeyRequest(100, 4, ("A", "ABCD"), ("B", "DCBA")));
        var outcome = await fixture.Outcomes.CreateOutcomeAsync(fixture.CourseId, new CreateLearningOutcomeRequest { Description = "Algoritma kurar" }, default);
        await fixture.Outcomes.ApplyReverseMappingAsync(fixture.ExamId, new ReverseBookletMappingRequest { BookletCode = "B" }, default);
        await fixture.Outcomes.SaveOutcomeMappingsAsync(fixture.ExamId, new SaveLearningOutcomeMappingsRequest { Mappings = [new LearningOutcomeQuestionMapInput { LearningOutcomeId = outcome!.Id, QuestionNumbers = [1, 2] }] }, default);
        var response = await fixture.SaveKeyAsync(ScoringFixture.KeyRequest(100, 4, ("A", "ABCE"), ("B", "DCBA")));
        Assert.Equal(2, response.Booklets.Count);
        Assert.Equal(2, await fixture.Context.QuestionLearningOutcomes.CountAsync());
        var page = await fixture.Outcomes.GetExamPageAsync(fixture.ExamId, default);
        var mapping = Assert.Single(page!.BookletMappings);
        Assert.Equal(new[] { (1, 4), (2, 3), (3, 2), (4, 1) }, mapping.Items.Select(item => (item.ReferenceQuestionNumber, item.BookletQuestionNumber)).ToArray());
        Assert.Equal(new[] { 1, 2 }, Assert.Single(page.QuestionMappings).QuestionNumbers);
    }

    [Fact]
    public async Task K26_SuggestionEndpointsAndReverseMapping_UnassignedInstructorGetsNotFound()
    {
        await using var fixture = await ScoringFixture.CreateAsync();
        await SetUpPendingSuggestionAsync(fixture);
        var suggestion = (await fixture.Suggestions.GetAsync(fixture.ExamId, default))!.Items.Single();
        var outsiderSuggestions = new CancellationSuggestionService(fixture.Context, fixture.Outsider, new GradeService(fixture.Context, fixture.Outsider));
        Assert.Null(await outsiderSuggestions.GetAsync(fixture.ExamId, default));
        Assert.Null(await outsiderSuggestions.ApproveAsync(fixture.ExamId, suggestion.Id, default));
        Assert.Null(await outsiderSuggestions.RejectAsync(fixture.ExamId, suggestion.Id, default));
        var outsiderOutcomes = new LearningOutcomeService(fixture.Context, fixture.Outsider);
        Assert.Null(await outsiderOutcomes.ApplyReverseMappingAsync(fixture.ExamId, new ReverseBookletMappingRequest { BookletCode = "B" }, default));
        Assert.Equal(CancellationSuggestionStatus.Pending, await fixture.Context.QuestionCancellationSuggestions.Select(item => item.Status).SingleAsync());
    }

    private static UpdateAnswerKeyRequest CancelledRequest()
    {
        var request = ScoringFixture.KeyRequest(100, 4, ("A", "AAAA"), ("B", "AAAA"));
        request.Booklets.Single(item => item.BookletCode == "A").Questions[0].Status = ExamQuestionStatus.Cancelled;
        return request;
    }

    private static async Task SetUpPendingSuggestionAsync(ScoringFixture fixture)
    {
        await fixture.SaveKeyAsync(ScoringFixture.KeyRequest(100, 4, ("A", "AAAA"), ("B", "AAAA")));
        await fixture.AddStudentAsync("001", "A", "BAAA");
        await fixture.AddStudentAsync("002", "B", "BBBB");
        await fixture.CalculateAsync();
        await fixture.SaveKeyAsync(CancelledRequest());
    }
}
