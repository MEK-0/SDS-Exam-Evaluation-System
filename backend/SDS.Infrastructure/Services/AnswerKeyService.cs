// Cevap anahtarını doğrular, kaydeder ve kitapçık soru eşleşmelerini yönetir.
using Microsoft.EntityFrameworkCore;
using SDS.Application.AnswerKeys;
using SDS.Application.Security;
using SDS.Domain.Entities;
using SDS.Infrastructure.Persistence;

namespace SDS.Infrastructure.Services;

public sealed class AnswerKeyService(SdsDbContext dbContext, ICurrentUser currentUser) : IAnswerKeyService
{
    public async Task<AnswerKeyResponse?> GetAsync(Guid examId, CancellationToken cancellationToken)
    {
        var exam = await GetAccessibleExamAsync(examId, cancellationToken);
        if (exam is null) return null;
        EnsureMultipleChoice(exam);
        var key = await dbContext.ExamAnswerKeys.AsNoTracking().Include(item => item.Questions).Include(item => item.Booklets).ThenInclude(item => item.Questions).ThenInclude(item => item.ExamQuestion).SingleOrDefaultAsync(item => item.ExamId == examId, cancellationToken);
        return key is null ? EmptyResponse(examId) : ToResponse(key, await LoadSuggestionsAsync(examId, cancellationToken));
    }

    public async Task<AnswerKeyResponse?> UpdateAsync(Guid examId, UpdateAnswerKeyRequest request, CancellationToken cancellationToken)
    {
        var exam = await GetAccessibleExamAsync(examId, cancellationToken);
        if (exam is null) return null;
        EnsureMultipleChoice(exam);
        var validationIssues = Validate(request);
        if (validationIssues.Count > 0) throw new ArgumentException(string.Join(" ", validationIssues));
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var existing = await dbContext.ExamAnswerKeys.Include(item => item.Booklets).ThenInclude(item => item.Questions).SingleOrDefaultAsync(item => item.ExamId == examId, cancellationToken);
        var snapshot = await TakeSnapshotAsync(examId, existing, cancellationToken);
        if (existing is not null)
        {
            var oldMappings = await dbContext.BookletQuestionMappings.Where(item => item.ExamId == examId).ToListAsync(cancellationToken);
            dbContext.BookletQuestionMappings.RemoveRange(oldMappings);
            dbContext.ExamAnswerKeys.Remove(existing);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        var answerKey = CreateAnswerKey(examId, request, snapshot);
        dbContext.ExamAnswerKeys.Add(answerKey);
        await dbContext.SaveChangesAsync(cancellationToken);
        var restoredMappings = RestoreMappings(examId, answerKey, snapshot);
        RestoreOutcomeLinks(examId, answerKey, snapshot);
        await SyncCancellationSuggestionsAsync(examId, answerKey, snapshot, restoredMappings, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(answerKey, await LoadSuggestionsAsync(examId, cancellationToken));
    }

    private async Task<KeySnapshot> TakeSnapshotAsync(Guid examId, ExamAnswerKey? existing, CancellationToken cancellationToken)
    {
        var states = new Dictionary<(string Code, int Display), (ExamQuestionStatus Status, QuestionCancellationPolicy Policy)>();
        if (existing is not null)
        {
            foreach (var booklet in existing.Booklets)
                foreach (var question in booklet.Questions)
                    states[(booklet.BookletCode, question.DisplayQuestionNumber)] = (question.Status, question.CancellationPolicy);
        }
        var mappings = await dbContext.BookletQuestionMappings.AsNoTracking().Where(item => item.ExamId == examId).Select(item => new MappingSnapshot(item.ReferenceBooklet.BookletCode, item.Booklet.BookletCode, item.ReferenceQuestionNumber, item.BookletQuestionNumber)).ToListAsync(cancellationToken);
        var links = await dbContext.QuestionLearningOutcomes.AsNoTracking().Where(item => item.ExamId == examId).Select(item => new OutcomeLinkSnapshot(item.ExamQuestion.CanonicalQuestionNumber, item.LearningOutcomeId)).ToListAsync(cancellationToken);
        return new KeySnapshot(states, mappings, links, existing?.QuestionCount ?? 0);
    }

    private IReadOnlyCollection<BookletQuestionMapping> RestoreMappings(Guid examId, ExamAnswerKey key, KeySnapshot snapshot)
    {
        var referenceCode = BookletMappingResolver.ReferenceBookletCode(key.Booklets.ToList());
        var reference = key.Booklets.Single(item => item.BookletCode == referenceCode);
        var restored = new List<BookletQuestionMapping>();
        if (snapshot.QuestionCount != key.QuestionCount) return restored;
        foreach (var group in snapshot.Mappings.Where(item => item.ReferenceCode == referenceCode && item.BookletCode != referenceCode).GroupBy(item => item.BookletCode))
        {
            var booklet = key.Booklets.FirstOrDefault(item => item.BookletCode == group.Key);
            if (booklet is null || group.Count() != key.QuestionCount) continue;
            restored.AddRange(group.Select(item => new BookletQuestionMapping { Id = Guid.NewGuid(), ExamId = examId, ReferenceBookletId = reference.Id, BookletId = booklet.Id, ReferenceQuestionNumber = item.ReferenceQuestionNumber, BookletQuestionNumber = item.BookletQuestionNumber }));
        }
        dbContext.BookletQuestionMappings.AddRange(restored);
        return restored;
    }

    private void RestoreOutcomeLinks(Guid examId, ExamAnswerKey key, KeySnapshot snapshot)
    {
        var questions = key.Questions.ToDictionary(item => item.CanonicalQuestionNumber);
        foreach (var link in snapshot.OutcomeLinks.Distinct())
        {
            if (!questions.TryGetValue(link.CanonicalQuestionNumber, out var question)) continue;
            dbContext.QuestionLearningOutcomes.Add(new QuestionLearningOutcome { Id = Guid.NewGuid(), ExamId = examId, ExamQuestionId = question.Id, LearningOutcomeId = link.LearningOutcomeId });
        }
    }

    private async Task SyncCancellationSuggestionsAsync(Guid examId, ExamAnswerKey key, KeySnapshot snapshot, IReadOnlyCollection<BookletQuestionMapping> mappings, CancellationToken cancellationToken)
    {
        var lookup = mappings.ToLookup(item => item.BookletId);
        var suggestions = await dbContext.QuestionCancellationSuggestions.Where(item => item.ExamId == examId).ToListAsync(cancellationToken);
        var current = key.Booklets.SelectMany(booklet => booklet.Questions.Select(question => (Code: booklet.BookletCode, Question: question))).ToDictionary(item => (item.Code, item.Question.DisplayQuestionNumber), item => item.Question);
        foreach (var suggestion in suggestions.Where(item => item.Status != CancellationSuggestionStatus.Approved).ToList())
        {
            var sourceCancelled = current.TryGetValue((suggestion.SourceBookletCode, suggestion.SourceQuestionNumber), out var source) && source.Status == ExamQuestionStatus.Cancelled;
            var targetActive = current.TryGetValue((suggestion.TargetBookletCode, suggestion.TargetQuestionNumber), out var target) && target.Status == ExamQuestionStatus.Active;
            if (sourceCancelled && (targetActive || suggestion.Status == CancellationSuggestionStatus.Rejected)) continue;
            dbContext.QuestionCancellationSuggestions.Remove(suggestion);
            suggestions.Remove(suggestion);
        }
        foreach (var booklet in key.Booklets)
        {
            foreach (var question in booklet.Questions.Where(item => item.Status == ExamQuestionStatus.Cancelled))
            {
                var wasCancelled = snapshot.States.TryGetValue((booklet.BookletCode, question.DisplayQuestionNumber), out var old) && old.Status == ExamQuestionStatus.Cancelled;
                if (wasCancelled) continue;
                var referenceNumber = BookletMappingResolver.ReferenceNumber(booklet, question, lookup);
                foreach (var other in key.Booklets.Where(item => item.Id != booklet.Id))
                {
                    var counterpart = other.Questions.FirstOrDefault(item => item.Status == ExamQuestionStatus.Active && BookletMappingResolver.ReferenceNumber(other, item, lookup) == referenceNumber);
                    if (counterpart is null) continue;
                    var exists = suggestions.Any(item => item.SourceBookletCode == booklet.BookletCode && item.SourceQuestionNumber == question.DisplayQuestionNumber && item.TargetBookletCode == other.BookletCode && item.TargetQuestionNumber == counterpart.DisplayQuestionNumber);
                    if (exists) continue;
                    var suggestion = new QuestionCancellationSuggestion { Id = Guid.NewGuid(), ExamId = examId, SourceBookletCode = booklet.BookletCode, SourceQuestionNumber = question.DisplayQuestionNumber, TargetBookletCode = other.BookletCode, TargetQuestionNumber = counterpart.DisplayQuestionNumber, Policy = question.CancellationPolicy, Status = CancellationSuggestionStatus.Pending, CreatedAt = DateTime.UtcNow };
                    dbContext.QuestionCancellationSuggestions.Add(suggestion);
                    suggestions.Add(suggestion);
                }
            }
        }
    }

    private async Task<IReadOnlyCollection<QuestionCancellationSuggestion>> LoadSuggestionsAsync(Guid examId, CancellationToken cancellationToken)
    {
        return await dbContext.QuestionCancellationSuggestions.AsNoTracking().Where(item => item.ExamId == examId).OrderBy(item => item.TargetBookletCode).ThenBy(item => item.TargetQuestionNumber).ToListAsync(cancellationToken);
    }

    private async Task<Exam?> GetAccessibleExamAsync(Guid examId, CancellationToken cancellationToken)
    {
        var exam = await dbContext.Exams.AsNoTracking().SingleOrDefaultAsync(item => item.Id == examId, cancellationToken);
        if (exam is null) return null;
        var user = currentUser.Get();
        return user.IsAdministrator || user.Id.HasValue && await dbContext.CourseInstructors.AnyAsync(item => item.CourseId == exam.CourseId && item.UserId == user.Id.Value, cancellationToken) ? exam : null;
    }

    private static void EnsureMultipleChoice(Exam exam)
    {
        if (exam.EvaluationType != EvaluationType.MultipleChoice) throw new InvalidOperationException("Cevap anahtarı yalnızca çoktan seçmeli sınavlarda kullanılabilir.");
    }

    private static List<string> Validate(UpdateAnswerKeyRequest request)
    {
        var issues = new List<string>();
        if (request.MaximumScore <= 0) issues.Add("Maksimum puan pozitif olmalıdır.");
        if (request.QuestionCount <= 0) issues.Add("Soru sayısı pozitif olmalıdır.");
        if (request.Questions.Count > request.QuestionCount) issues.Add("Soru kayıtları tanımlanan soru sayısını aşamaz.");
        if (request.Booklets.Count == 0) issues.Add("En az bir kitapçık tanımlanmalıdır.");
        if (request.Booklets.Select(item => item.BookletCode.Trim().ToUpperInvariant()).Where(item => item.Length > 0).Distinct().Count() != request.Booklets.Count) issues.Add("Kitapçık kodları tekrar edemez ve boş olamaz.");
        var questions = request.Questions.GroupBy(item => item.CanonicalQuestionNumber).ToList();
        if (questions.Any(item => item.Key <= 0 || item.Key > request.QuestionCount || item.Count() > 1)) issues.Add("Mantıksal soru numaraları geçersiz veya tekrarlı.");
        if (request.Questions.Any(item => item.Status == ExamQuestionStatus.Active && item.CanonicalQuestionNumber <= 0)) issues.Add("Aktif soru numarası geçersiz.");
        foreach (var booklet in request.Booklets)
        {
            var displayNumbers = booklet.Questions.GroupBy(item => item.DisplayQuestionNumber).ToList();
            var logicalNumbers = booklet.Questions.GroupBy(item => item.ExamQuestionNumber).ToList();
            if (displayNumbers.Any(item => item.Key <= 0 || item.Count() > 1)) issues.Add($"{booklet.BookletCode} kitapçığında görüntülenen soru numaraları tekrar edemez.");
            if (logicalNumbers.Any(item => item.Key <= 0 || item.Count() > 1)) issues.Add($"{booklet.BookletCode} kitapçığında aynı mantıksal soru tekrar edemez.");
            foreach (var question in booklet.Questions)
            {
                var accepted = question.AcceptedAnswers.Select(item => item.Trim().ToUpperInvariant()).Where(item => item.Length > 0).ToList();
                if (accepted.Any(item => item is not ("A" or "B" or "C" or "D" or "E")) || accepted.Count != accepted.Distinct().Count()) issues.Add($"{booklet.BookletCode} kitapçığında geçersiz veya tekrarlı cevap şıkkı bulunuyor.");
                if (accepted.Count > 1 && !request.AllowMultipleAnswers) issues.Add("Birden fazla doğru şık seçimi etkin değil.");
                var logicalQuestion = request.Questions.SingleOrDefault(item => item.CanonicalQuestionNumber == question.ExamQuestionNumber);
                if (logicalQuestion is null) issues.Add($"{booklet.BookletCode} kitapçığındaki {question.ExamQuestionNumber}. mantıksal soru tanımlı değil.");
                if (logicalQuestion?.Status == ExamQuestionStatus.Active && accepted.Count == 0) continue;
            }
        }
        return issues;
    }

    private static ExamAnswerKey CreateAnswerKey(Guid examId, UpdateAnswerKeyRequest request, KeySnapshot snapshot)
    {
        var points = AnswerKeyPointCalculator.Calculate(request.MaximumScore, request.QuestionCount);
        var key = new ExamAnswerKey { Id = Guid.NewGuid(), ExamId = examId, MaximumScore = request.MaximumScore, QuestionCount = request.QuestionCount, DynamicScoring = request.DynamicScoring, AllowMultipleAnswers = request.AllowMultipleAnswers, CancelledQuestionPolicy = request.CancelledQuestionPolicy, UpdatedAt = DateTime.UtcNow };
        key.Questions = request.Questions.Select(item => new ExamQuestion { Id = Guid.NewGuid(), ExamAnswerKeyId = key.Id, CanonicalQuestionNumber = item.CanonicalQuestionNumber, Points = points[item.CanonicalQuestionNumber - 1], Status = item.Status, Source = item.Source }).ToList();
        var questionMap = key.Questions.ToDictionary(item => item.CanonicalQuestionNumber);
        var canonicalRequests = request.Questions.ToDictionary(item => item.CanonicalQuestionNumber);
        var isComplete = request.Questions.Count == request.QuestionCount && request.Booklets.Count > 0;
        key.Booklets = request.Booklets.Select(booklet =>
        {
            var code = booklet.BookletCode.Trim().ToUpperInvariant();
            var entity = new ExamBooklet { Id = Guid.NewGuid(), ExamAnswerKeyId = key.Id, BookletCode = code, QuestionCount = booklet.Questions.Count };
            entity.Questions = booklet.Questions.Select(item =>
            {
                snapshot.States.TryGetValue((code, item.DisplayQuestionNumber), out var old);
                var status = ResolveStatus(item, canonicalRequests[item.ExamQuestionNumber], snapshot.States.ContainsKey((code, item.DisplayQuestionNumber)) ? old.Status : null);
                var policy = item.CancellationPolicy ?? (snapshot.States.ContainsKey((code, item.DisplayQuestionNumber)) ? old.Policy : QuestionCancellationPolicy.AwardFullPoints);
                var displayIndex = Math.Clamp(item.DisplayQuestionNumber, 1, request.QuestionCount) - 1;
                var accepted = string.Join(',', item.AcceptedAnswers.Select(answer => answer.Trim().ToUpperInvariant()).Distinct());
                return new BookletQuestion { Id = Guid.NewGuid(), ExamBookletId = entity.Id, ExamQuestionId = questionMap[item.ExamQuestionNumber].Id, DisplayQuestionNumber = item.DisplayQuestionNumber, AcceptedAnswers = accepted, Points = points[displayIndex], Status = status, CancellationPolicy = policy };
            }).ToList();
            return entity;
        }).ToList();
        foreach (var question in key.Questions)
        {
            var linked = key.Booklets.SelectMany(booklet => booklet.Questions).Where(item => item.ExamQuestionId == question.Id).ToList();
            question.Status = canonicalRequests[question.CanonicalQuestionNumber].Status == ExamQuestionStatus.Cancelled || linked.Count > 0 && linked.All(item => item.Status == ExamQuestionStatus.Cancelled) ? ExamQuestionStatus.Cancelled : ExamQuestionStatus.Active;
        }
        var allAnswered = key.Booklets.All(booklet => booklet.Questions.Count == request.QuestionCount && booklet.Questions.All(item => item.Status == ExamQuestionStatus.Cancelled || item.AcceptedAnswers.Length > 0));
        key.Status = isComplete && allAnswered ? AnswerKeyStatus.Validated : AnswerKeyStatus.Draft;
        return key;
    }

    private static ExamQuestionStatus ResolveStatus(BookletQuestionRequest item, AnswerKeyQuestionRequest canonical, ExamQuestionStatus? oldStatus)
    {
        if (item.Status.HasValue) return item.Status.Value;
        if (canonical.Status == ExamQuestionStatus.Cancelled) return ExamQuestionStatus.Cancelled;
        return oldStatus ?? ExamQuestionStatus.Active;
    }

    private static AnswerKeyResponse EmptyResponse(Guid examId) => new(Guid.Empty, examId, 100, 0, true, false, CancelledQuestionPolicy.ExcludeFromTotal, AnswerKeyStatus.Draft, DateTime.UtcNow, false, [], [], ["Cevap anahtarı henüz oluşturulmadı."], [], 0);

    private static AnswerKeyResponse ToResponse(ExamAnswerKey key, IReadOnlyCollection<QuestionCancellationSuggestion> suggestions)
    {
        var bookletQuestions = key.Booklets.SelectMany(item => item.Questions).ToList();
        var questions = key.Questions.OrderBy(item => item.CanonicalQuestionNumber).Select(item =>
        {
            var linked = bookletQuestions.Where(question => question.ExamQuestionId == item.Id).ToList();
            var status = item.Status == ExamQuestionStatus.Cancelled || linked.Count > 0 && linked.All(question => question.Status == ExamQuestionStatus.Cancelled) ? ExamQuestionStatus.Cancelled : ExamQuestionStatus.Active;
            return new AnswerKeyQuestionResponse(item.Id, item.CanonicalQuestionNumber, item.Points, status, item.Source);
        }).ToList();
        var booklets = key.Booklets.OrderBy(item => item.BookletCode).Select(item => new AnswerKeyBookletResponse(
            item.Id,
            item.BookletCode,
            item.QuestionCount,
            item.Questions.OrderBy(question => question.DisplayQuestionNumber).Select(question => new BookletQuestionResponse(
                question.Id,
                question.ExamQuestionId,
                question.ExamQuestion.CanonicalQuestionNumber,
                question.DisplayQuestionNumber,
                question.AcceptedAnswers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                question.Points ?? question.ExamQuestion.Points,
                question.Status,
                question.CancellationPolicy)).ToList())).ToList();
        var suggestionResponses = suggestions.Select(item => new CancellationSuggestionResponse(item.Id, item.SourceBookletCode, item.SourceQuestionNumber, item.TargetBookletCode, item.TargetQuestionNumber, item.Policy, item.Status, item.CreatedAt, item.DecidedAt)).ToList();
        return new AnswerKeyResponse(key.Id, key.ExamId, key.MaximumScore, key.QuestionCount, key.DynamicScoring, key.AllowMultipleAnswers, key.CancelledQuestionPolicy, key.Status, key.UpdatedAt, key.Status == AnswerKeyStatus.Validated, questions, booklets, [], suggestionResponses, suggestions.Count(item => item.Status == CancellationSuggestionStatus.Pending));
    }

    private sealed record MappingSnapshot(string ReferenceCode, string BookletCode, int ReferenceQuestionNumber, int BookletQuestionNumber);
    private sealed record OutcomeLinkSnapshot(int CanonicalQuestionNumber, Guid LearningOutcomeId);
    private sealed record KeySnapshot(Dictionary<(string Code, int Display), (ExamQuestionStatus Status, QuestionCancellationPolicy Policy)> States, IReadOnlyCollection<MappingSnapshot> Mappings, IReadOnlyCollection<OutcomeLinkSnapshot> OutcomeLinks, int QuestionCount);
}
