// Öğrenme çıktısı, kitapçık eşleştirmesi ve sınav tamamlama işlemlerini yürütür.
using Microsoft.EntityFrameworkCore;
using SDS.Application.LearningOutcomes;
using SDS.Application.Security;
using SDS.Domain.Entities;
using SDS.Infrastructure.Persistence;

namespace SDS.Infrastructure.Services;

public sealed class LearningOutcomeService(SdsDbContext dbContext, ICurrentUser currentUser) : ILearningOutcomeService
{
    public async Task<IReadOnlyCollection<LearningOutcomeResponse>?> GetCourseOutcomesAsync(Guid courseId, CancellationToken cancellationToken)
    {
        if (!await CanAccessCourseAsync(courseId, cancellationToken)) return null;
        return await dbContext.LearningOutcomes.AsNoTracking().Where(item => item.CourseId == courseId).OrderBy(item => item.DisplayOrder).ThenBy(item => item.CreatedAt).Select(item => new LearningOutcomeResponse(item.Id, item.CourseId, item.Description, item.DisplayOrder, item.UpdatedAt)).ToListAsync(cancellationToken);
    }

    public async Task<LearningOutcomeResponse?> CreateOutcomeAsync(Guid courseId, CreateLearningOutcomeRequest request, CancellationToken cancellationToken)
    {
        if (!await CanAccessCourseAsync(courseId, cancellationToken)) return null;
        var description = request.Description.Trim();
        if (description.Length == 0) throw new ArgumentException("Öğrenme çıktısı açıklaması boş bırakılamaz.");
        if (await dbContext.LearningOutcomes.AnyAsync(item => item.CourseId == courseId && item.Description == description, cancellationToken)) throw new InvalidOperationException("Bu öğrenme çıktısı aynı ders için zaten kayıtlı.");
        var now = DateTime.UtcNow;
        var outcome = new LearningOutcome { Id = Guid.NewGuid(), CourseId = courseId, Description = description, DisplayOrder = await dbContext.LearningOutcomes.CountAsync(item => item.CourseId == courseId, cancellationToken) + 1, CreatedAt = now, UpdatedAt = now };
        dbContext.LearningOutcomes.Add(outcome);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new LearningOutcomeResponse(outcome.Id, outcome.CourseId, outcome.Description, outcome.DisplayOrder, outcome.UpdatedAt);
    }

    public async Task<LearningOutcomePageResponse?> GetExamPageAsync(Guid examId, CancellationToken cancellationToken)
    {
        var exam = await LoadExamAsync(examId, cancellationToken);
        return exam is null ? null : await BuildPageAsync(exam, cancellationToken);
    }

    public async Task<LearningOutcomePageResponse?> SaveBookletMappingsAsync(Guid examId, SaveBookletQuestionMappingsRequest request, CancellationToken cancellationToken)
    {
        var exam = await LoadExamAsync(examId, cancellationToken);
        if (exam is null) return null;
        ValidateBookletMappings(exam, request);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var existing = await dbContext.BookletQuestionMappings.Where(item => item.ExamId == examId).ToListAsync(cancellationToken);
        dbContext.BookletQuestionMappings.RemoveRange(existing);
        dbContext.BookletQuestionMappings.AddRange(request.Mappings.SelectMany(item => item.Items.Select(mapping => new BookletQuestionMapping { Id = Guid.NewGuid(), ExamId = examId, ReferenceBookletId = exam.AnswerKey!.Booklets.Single(booklet => booklet.BookletCode == item.ReferenceBookletCode).Id, BookletId = exam.AnswerKey.Booklets.Single(booklet => booklet.BookletCode == item.BookletCode).Id, ReferenceQuestionNumber = mapping.ReferenceQuestionNumber, BookletQuestionNumber = mapping.BookletQuestionNumber })));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await BuildPageAsync(exam, cancellationToken);
    }

    public async Task<LearningOutcomePageResponse?> SaveOutcomeMappingsAsync(Guid examId, SaveLearningOutcomeMappingsRequest request, CancellationToken cancellationToken)
    {
        var exam = await LoadExamAsync(examId, cancellationToken);
        if (exam is null) return null;
        await ValidateOutcomeMappingsAsync(exam, request, cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var existing = await dbContext.QuestionLearningOutcomes.Where(item => item.ExamId == examId).ToListAsync(cancellationToken);
        dbContext.QuestionLearningOutcomes.RemoveRange(existing);
        dbContext.QuestionLearningOutcomes.AddRange(request.Mappings.SelectMany(item => item.QuestionNumbers.Distinct().Select(number => new QuestionLearningOutcome { Id = Guid.NewGuid(), ExamId = examId, ExamQuestionId = exam.AnswerKey!.Questions.Single(question => question.CanonicalQuestionNumber == number).Id, LearningOutcomeId = item.LearningOutcomeId })));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await BuildPageAsync(exam, cancellationToken);
    }

    public async Task<LearningOutcomePageResponse?> CompleteExamAsync(Guid examId, CompleteExamRequest request, CancellationToken cancellationToken)
    {
        var exam = await LoadExamAsync(examId, cancellationToken);
        if (exam is null) return null;
        if (exam.Status == ExamStatus.Completed) return await BuildPageAsync(exam, cancellationToken);
        await ValidatePreviousStepsAsync(exam, cancellationToken);
        if (exam.EvaluationType == EvaluationType.MultipleChoice) ValidateBookletMappings(exam, request.BookletMappings);
        await ValidateOutcomeMappingsAsync(exam, request.LearningOutcomeMappings, cancellationToken);
        var outcomes = await dbContext.LearningOutcomes.Where(item => item.CourseId == exam.CourseId).ToListAsync(cancellationToken);
        if (outcomes.Count == 0) throw new InvalidOperationException("Sınavı tamamlamak için en az bir öğrenme çıktısı eklemelisiniz.");
        if (request.LearningOutcomeMappings.Mappings.All(item => item.QuestionNumbers.Count == 0)) throw new InvalidOperationException("Sınavı tamamlamak için en az bir soru-öğrenme çıktısı ilişkisi kaydetmelisiniz.");
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var oldBookletMappings = await dbContext.BookletQuestionMappings.Where(item => item.ExamId == examId).ToListAsync(cancellationToken);
        var oldOutcomeMappings = await dbContext.QuestionLearningOutcomes.Where(item => item.ExamId == examId).ToListAsync(cancellationToken);
        dbContext.BookletQuestionMappings.RemoveRange(oldBookletMappings);
        dbContext.QuestionLearningOutcomes.RemoveRange(oldOutcomeMappings);
        dbContext.BookletQuestionMappings.AddRange(request.BookletMappings.Mappings.SelectMany(item => item.Items.Select(mapping => new BookletQuestionMapping { Id = Guid.NewGuid(), ExamId = examId, ReferenceBookletId = exam.AnswerKey!.Booklets.Single(booklet => booklet.BookletCode == item.ReferenceBookletCode).Id, BookletId = exam.AnswerKey.Booklets.Single(booklet => booklet.BookletCode == item.BookletCode).Id, ReferenceQuestionNumber = mapping.ReferenceQuestionNumber, BookletQuestionNumber = mapping.BookletQuestionNumber })));
        dbContext.QuestionLearningOutcomes.AddRange(request.LearningOutcomeMappings.Mappings.SelectMany(item => item.QuestionNumbers.Distinct().Select(number => new QuestionLearningOutcome { Id = Guid.NewGuid(), ExamId = examId, ExamQuestionId = exam.AnswerKey!.Questions.Single(question => question.CanonicalQuestionNumber == number).Id, LearningOutcomeId = item.LearningOutcomeId })));
        exam.Status = ExamStatus.Completed;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return await BuildPageAsync(exam, cancellationToken);
    }

    private async Task<Exam?> LoadExamAsync(Guid examId, CancellationToken cancellationToken)
    {
        var exam = await dbContext.Exams.Include(item => item.Course).Include(item => item.AnswerKey!).ThenInclude(item => item.Booklets).ThenInclude(item => item.Questions).Include(item => item.AnswerKey!).ThenInclude(item => item.Questions).SingleOrDefaultAsync(item => item.Id == examId, cancellationToken);
        return exam is null || !await CanAccessCourseAsync(exam.CourseId, cancellationToken) ? null : exam;
    }

    private async Task<LearningOutcomePageResponse> BuildPageAsync(Exam exam, CancellationToken cancellationToken)
    {
        var outcomes = await dbContext.LearningOutcomes.AsNoTracking().Where(item => item.CourseId == exam.CourseId).OrderBy(item => item.DisplayOrder).ThenBy(item => item.CreatedAt).ToListAsync(cancellationToken);
        var questionCount = exam.AnswerKey?.QuestionCount ?? 0;
        var mappings = await dbContext.BookletQuestionMappings.AsNoTracking().Where(item => item.ExamId == exam.Id).Include(item => item.ReferenceBooklet).Include(item => item.Booklet).ToListAsync(cancellationToken);
        var outcomeMappings = await dbContext.QuestionLearningOutcomes.AsNoTracking().Where(item => item.ExamId == exam.Id).ToListAsync(cancellationToken);
        var referenceCode = exam.AnswerKey?.Booklets.OrderBy(item => item.BookletCode).FirstOrDefault()?.BookletCode ?? "A";
        var bookletResponses = exam.AnswerKey?.Booklets.Where(item => item.BookletCode != referenceCode).OrderBy(item => item.BookletCode).Select(booklet => new BookletQuestionMappingResponse(referenceCode, booklet.BookletCode, mappings.Where(item => item.BookletId == booklet.Id).OrderBy(item => item.ReferenceQuestionNumber).Select(item => new BookletQuestionMappingItem(item.ReferenceQuestionNumber, item.BookletQuestionNumber)).ToList())).ToList() ?? [];
        var outcomeResponses = outcomeMappings.GroupBy(item => item.LearningOutcomeId).Select(group => new LearningOutcomeQuestionMapResponse(group.Key, group.Select(item => exam.AnswerKey!.Questions.Single(question => question.Id == item.ExamQuestionId).CanonicalQuestionNumber).OrderBy(number => number).ToList())).ToList();
        return new LearningOutcomePageResponse(outcomes.Select(item => new LearningOutcomeResponse(item.Id, item.CourseId, item.Description, item.DisplayOrder, item.UpdatedAt)).ToList(), bookletResponses, outcomeResponses, questionCount, exam.EvaluationType == EvaluationType.MultipleChoice, exam.Status == ExamStatus.Completed);
    }

    private void ValidateBookletMappings(Exam exam, SaveBookletQuestionMappingsRequest request)
    {
        if (exam.EvaluationType != EvaluationType.MultipleChoice || exam.AnswerKey is null) throw new InvalidOperationException("Kitapçık eşleştirmesi için çoktan seçmeli sınavın cevap anahtarı hazırlanmalıdır.");
        var booklets = exam.AnswerKey.Booklets.ToList();
        if (booklets.Count <= 1) return;
        var reference = booklets.FirstOrDefault(item => item.BookletCode == "A")?.BookletCode ?? booklets.OrderBy(item => item.BookletCode).First().BookletCode;
        if (request.Mappings.Count != booklets.Count - 1 || request.Mappings.Select(item => item.BookletCode).Distinct().Count() != request.Mappings.Count) throw new ArgumentException("Her referans dışı kitapçık için eşleştirme yapılmalıdır.");
        foreach (var mapping in request.Mappings)
        {
            if (mapping.ReferenceBookletCode != reference || mapping.BookletCode == reference || !booklets.Any(item => item.BookletCode == mapping.BookletCode)) throw new ArgumentException("Kitapçık soru eşleştirmesi geçersiz.");
            if (mapping.Items.Count != exam.AnswerKey.QuestionCount) throw new ArgumentException("Kitapçık soru eşleştirmesi tüm sorular için yapılmalıdır.");
            var values = mapping.Items.Select(item => item.BookletQuestionNumber).ToList();
            if (mapping.Items.Select(item => item.ReferenceQuestionNumber).Distinct().Count() != exam.AnswerKey.QuestionCount || values.Distinct().Count() != exam.AnswerKey.QuestionCount || values.Any(item => item < 1 || item > exam.AnswerKey.QuestionCount) || mapping.Items.Any(item => item.ReferenceQuestionNumber < 1 || item.ReferenceQuestionNumber > exam.AnswerKey.QuestionCount)) throw new ArgumentException("Kitapçık soruları tekrar edemez ve geçerli soru aralığında olmalıdır.");
        }
    }

    private async Task ValidateOutcomeMappingsAsync(Exam exam, SaveLearningOutcomeMappingsRequest request, CancellationToken cancellationToken)
    {
        var outcomeIds = await dbContext.LearningOutcomes.Where(item => item.CourseId == exam.CourseId).Select(item => item.Id).ToListAsync(cancellationToken);
        if (exam.AnswerKey is null) throw new InvalidOperationException("Öğrenme çıktısı ilişkileri için sınav soruları tanımlanmalıdır.");
        if (request.Mappings.Any(item => !outcomeIds.Contains(item.LearningOutcomeId) || item.QuestionNumbers.Any(number => number < 1 || number > exam.AnswerKey.QuestionCount))) throw new ArgumentException("Öğrenme çıktısı-soru ilişkisi geçersiz.");
    }

    private async Task ValidatePreviousStepsAsync(Exam exam, CancellationToken cancellationToken)
    {
        if (exam.EvaluationType == EvaluationType.OpenEnded) return;
        if (!await dbContext.ExamImports.AnyAsync(item => item.ExamId == exam.Id, cancellationToken)) throw new InvalidOperationException("Sınavı tamamlamak için dosya yükleme adımını tamamlamalısınız.");
        if (exam.AnswerKey is null || exam.AnswerKey.Status != AnswerKeyStatus.Validated) throw new InvalidOperationException("Sınavı tamamlamak için cevap anahtarını doğrulamalısınız.");
        if (!exam.MatchingConfirmed) throw new InvalidOperationException("Sınavı tamamlamak için veri eşleştirmesini onaylamalısınız.");
        if (!await dbContext.ExamResults.AnyAsync(item => item.ExamId == exam.Id, cancellationToken)) throw new InvalidOperationException("Sınavı tamamlamak için not hesaplamasını tamamlamalısınız.");
    }

    private async Task<bool> CanAccessCourseAsync(Guid courseId, CancellationToken cancellationToken)
    {
        var user = currentUser.Get();
        return user.IsAdministrator || user.Id.HasValue && await dbContext.CourseInstructors.AnyAsync(item => item.CourseId == courseId && item.UserId == user.Id.Value, cancellationToken);
    }
}
