// Soru iptal önerilerini listeler, onaylar veya reddeder; onayda iptali uygular ve notları yeniden hesaplar.
using Microsoft.EntityFrameworkCore;
using SDS.Application.AnswerKeys;
using SDS.Application.Grades;
using SDS.Application.Security;
using SDS.Domain.Entities;
using SDS.Infrastructure.Persistence;

namespace SDS.Infrastructure.Services;

public sealed class CancellationSuggestionService(SdsDbContext dbContext, ICurrentUser currentUser, IGradeService gradeService) : ICancellationSuggestionService
{
    public async Task<CancellationSuggestionsResponse?> GetAsync(Guid examId, CancellationToken cancellationToken)
    {
        var exam = await GetAccessibleExamAsync(examId, cancellationToken);
        return exam is null ? null : await BuildResponseAsync(examId, cancellationToken);
    }

    public async Task<CancellationSuggestionsResponse?> ApproveAsync(Guid examId, Guid suggestionId, CancellationToken cancellationToken)
    {
        var exam = await GetAccessibleExamAsync(examId, cancellationToken);
        if (exam is null) return null;
        var suggestion = await LoadPendingSuggestionAsync(examId, suggestionId, cancellationToken);
        var target = await dbContext.BookletQuestions
            .Include(item => item.ExamQuestion)
            .SingleOrDefaultAsync(item => item.ExamBooklet.ExamAnswerKey.ExamId == examId && item.ExamBooklet.BookletCode == suggestion.TargetBookletCode && item.DisplayQuestionNumber == suggestion.TargetQuestionNumber, cancellationToken);
        if (target is null) throw new InvalidOperationException($"{suggestion.TargetBookletCode} kitapçığındaki {suggestion.TargetQuestionNumber}. soru bulunamadı; öneri uygulanamaz.");
        await using (var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken))
        {
            target.Status = ExamQuestionStatus.Cancelled;
            target.CancellationPolicy = suggestion.Policy;
            suggestion.Status = CancellationSuggestionStatus.Approved;
            suggestion.DecidedAt = DateTime.UtcNow;
            suggestion.DecidedByUserId = currentUser.Get().Id;
            await dbContext.SaveChangesAsync(cancellationToken);
            if (await dbContext.ExamResults.AnyAsync(item => item.ExamId == examId, cancellationToken)) await gradeService.CalculateAsync(examId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        return await BuildResponseAsync(examId, cancellationToken);
    }

    public async Task<CancellationSuggestionsResponse?> RejectAsync(Guid examId, Guid suggestionId, CancellationToken cancellationToken)
    {
        var exam = await GetAccessibleExamAsync(examId, cancellationToken);
        if (exam is null) return null;
        var suggestion = await LoadPendingSuggestionAsync(examId, suggestionId, cancellationToken);
        suggestion.Status = CancellationSuggestionStatus.Rejected;
        suggestion.DecidedAt = DateTime.UtcNow;
        suggestion.DecidedByUserId = currentUser.Get().Id;
        await dbContext.SaveChangesAsync(cancellationToken);
        return await BuildResponseAsync(examId, cancellationToken);
    }

    private async Task<QuestionCancellationSuggestion> LoadPendingSuggestionAsync(Guid examId, Guid suggestionId, CancellationToken cancellationToken)
    {
        var suggestion = await dbContext.QuestionCancellationSuggestions.SingleOrDefaultAsync(item => item.Id == suggestionId && item.ExamId == examId, cancellationToken);
        if (suggestion is null) throw new KeyNotFoundException("İptal önerisi bulunamadı.");
        if (suggestion.Status != CancellationSuggestionStatus.Pending) throw new InvalidOperationException("Bu iptal önerisi için zaten karar verildi.");
        return suggestion;
    }

    private async Task<CancellationSuggestionsResponse> BuildResponseAsync(Guid examId, CancellationToken cancellationToken)
    {
        var items = await dbContext.QuestionCancellationSuggestions.AsNoTracking()
            .Where(item => item.ExamId == examId)
            .OrderBy(item => item.TargetBookletCode)
            .ThenBy(item => item.TargetQuestionNumber)
            .ToListAsync(cancellationToken);
        var responses = items.Select(item => new CancellationSuggestionResponse(item.Id, item.SourceBookletCode, item.SourceQuestionNumber, item.TargetBookletCode, item.TargetQuestionNumber, item.Policy, item.Status, item.CreatedAt, item.DecidedAt)).ToList();
        return new CancellationSuggestionsResponse(responses, items.Count(item => item.Status == CancellationSuggestionStatus.Pending));
    }

    private async Task<Exam?> GetAccessibleExamAsync(Guid examId, CancellationToken cancellationToken)
    {
        var exam = await dbContext.Exams.AsNoTracking().SingleOrDefaultAsync(item => item.Id == examId, cancellationToken);
        if (exam is null) return null;
        var user = currentUser.Get();
        var hasAccess = user.IsAdministrator || user.Id.HasValue && await dbContext.CourseInstructors.AnyAsync(item => item.CourseId == exam.CourseId && item.UserId == user.Id.Value, cancellationToken);
        return hasAccess ? exam : null;
    }
}
