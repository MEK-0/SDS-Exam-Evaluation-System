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
        return key is null ? EmptyResponse(examId) : ToResponse(key);
    }

    public async Task<AnswerKeyResponse?> UpdateAsync(Guid examId, UpdateAnswerKeyRequest request, CancellationToken cancellationToken)
    {
        var exam = await GetAccessibleExamAsync(examId, cancellationToken);
        if (exam is null) return null;
        EnsureMultipleChoice(exam);
        var validationIssues = Validate(request);
        if (validationIssues.Count > 0) throw new ArgumentException(string.Join(" ", validationIssues));
        var isComplete = IsComplete(request);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var existing = await dbContext.ExamAnswerKeys.SingleOrDefaultAsync(item => item.ExamId == examId, cancellationToken);
        if (existing is not null)
        {
            dbContext.ExamAnswerKeys.Remove(existing);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        var answerKey = CreateAnswerKey(examId, request, isComplete);
        dbContext.ExamAnswerKeys.Add(answerKey);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToResponse(answerKey);
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

    private static bool IsComplete(UpdateAnswerKeyRequest request)
    {
        return request.Questions.Count == request.QuestionCount && request.Booklets.Count > 0 && request.Booklets.All(booklet => booklet.Questions.Count == request.QuestionCount) && request.Questions.All(question => question.Status == ExamQuestionStatus.Cancelled || request.Booklets.All(booklet => booklet.Questions.Any(item => item.ExamQuestionNumber == question.CanonicalQuestionNumber && item.AcceptedAnswers.Count > 0)));
    }

    private static ExamAnswerKey CreateAnswerKey(Guid examId, UpdateAnswerKeyRequest request, bool isComplete)
    {
        var key = new ExamAnswerKey { Id = Guid.NewGuid(), ExamId = examId, MaximumScore = request.MaximumScore, QuestionCount = request.QuestionCount, DynamicScoring = request.DynamicScoring, AllowMultipleAnswers = request.AllowMultipleAnswers, CancelledQuestionPolicy = request.CancelledQuestionPolicy, Status = isComplete ? AnswerKeyStatus.Validated : AnswerKeyStatus.Draft, UpdatedAt = DateTime.UtcNow };
        var points = AnswerKeyPointCalculator.Calculate(request.MaximumScore, request.QuestionCount);
        key.Questions = request.Questions.Select(item => new ExamQuestion { Id = Guid.NewGuid(), ExamAnswerKeyId = key.Id, CanonicalQuestionNumber = item.CanonicalQuestionNumber, Points = points[item.CanonicalQuestionNumber - 1], Status = item.Status }).ToList();
        var questionMap = key.Questions.ToDictionary(item => item.CanonicalQuestionNumber);
        key.Booklets = request.Booklets.Select(booklet => new ExamBooklet { Id = Guid.NewGuid(), ExamAnswerKeyId = key.Id, BookletCode = booklet.BookletCode.Trim().ToUpperInvariant(), QuestionCount = booklet.Questions.Count, Questions = booklet.Questions.Select(item => new BookletQuestion { Id = Guid.NewGuid(), ExamBookletId = Guid.Empty, ExamQuestionId = questionMap[item.ExamQuestionNumber].Id, DisplayQuestionNumber = item.DisplayQuestionNumber, AcceptedAnswers = string.Join(',', item.AcceptedAnswers.Select(answer => answer.Trim().ToUpperInvariant()).Distinct()) }).ToList() }).ToList();
        foreach (var booklet in key.Booklets) foreach (var question in booklet.Questions) question.ExamBookletId = booklet.Id;
        return key;
    }

    private static AnswerKeyResponse EmptyResponse(Guid examId) => new(Guid.Empty, examId, 100, 0, true, false, CancelledQuestionPolicy.ExcludeFromTotal, AnswerKeyStatus.Draft, DateTime.UtcNow, false, [], [], ["Cevap anahtarı henüz oluşturulmadı."]);
    private static AnswerKeyResponse ToResponse(ExamAnswerKey key) => new(
        key.Id,
        key.ExamId,
        key.MaximumScore,
        key.QuestionCount,
        key.DynamicScoring,
        key.AllowMultipleAnswers,
        key.CancelledQuestionPolicy,
        key.Status,
        key.UpdatedAt,
        key.Status == AnswerKeyStatus.Validated,
        key.Questions.OrderBy(item => item.CanonicalQuestionNumber).Select(item => new AnswerKeyQuestionResponse(item.Id, item.CanonicalQuestionNumber, item.Points, item.Status)).ToList(),
        key.Booklets.OrderBy(item => item.BookletCode).Select(item => new AnswerKeyBookletResponse(
            item.Id,
            item.BookletCode,
            item.QuestionCount,
            item.Questions.OrderBy(question => question.DisplayQuestionNumber).Select(question => new BookletQuestionResponse(
                question.Id,
                question.ExamQuestionId,
                question.ExamQuestion.CanonicalQuestionNumber,
                question.DisplayQuestionNumber,
                question.AcceptedAnswers.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))).ToList())).ToList(),
        []);
}
