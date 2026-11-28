// Sınav türü ile optik formdaki sınav türü alanı değeri arasındaki eşlemeyi tanımlar.
using SDS.Domain.Entities;

namespace SDS.Application.Imports;

public static class ExamTypeCodes
{
    public static string GetCode(ExamType examType)
    {
        return examType switch
        {
            ExamType.Midterm => "1",
            ExamType.Final => "2",
            _ => "3"
        };
    }

    public static string GetDisplayName(ExamType examType)
    {
        return examType switch
        {
            ExamType.Midterm => "Vize",
            ExamType.Final => "Final",
            _ => "Bütünleme"
        };
    }
}
