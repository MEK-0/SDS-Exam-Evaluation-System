// Kitapçık sorusunu referans soru numarası uzayına eşler; yalnızca soru ve öğrenme çıktısı analizinde kullanılır.
using SDS.Domain.Entities;

namespace SDS.Infrastructure.Services;

internal static class BookletMappingResolver
{
    public static int ReferenceNumber(ExamBooklet booklet, BookletQuestion question, ILookup<Guid, BookletQuestionMapping> mappingsByBooklet)
    {
        var match = mappingsByBooklet[booklet.Id].FirstOrDefault(item => item.BookletQuestionNumber == question.DisplayQuestionNumber);
        return match?.ReferenceQuestionNumber ?? question.ExamQuestion.CanonicalQuestionNumber;
    }

    public static string ReferenceBookletCode(IReadOnlyCollection<ExamBooklet> booklets)
    {
        return booklets.FirstOrDefault(item => item.BookletCode == "A")?.BookletCode ?? booklets.OrderBy(item => item.BookletCode).First().BookletCode;
    }
}
