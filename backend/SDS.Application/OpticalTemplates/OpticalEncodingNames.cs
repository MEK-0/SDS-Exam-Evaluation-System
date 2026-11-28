// Optik şablon kodlama modunu ayar/ayrıştırıcı adlarına çevirir ve adları doğrular.
using SDS.Domain.Entities;

namespace SDS.Application.OpticalTemplates;

public static class OpticalEncodingNames
{
    public const string Auto = "auto";
    public const string Utf8 = "utf-8";
    public const string Windows1254 = "windows-1254";

    public static OpticalEncodingMode Parse(string? name)
    {
        return (name ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            Auto => OpticalEncodingMode.Auto,
            Utf8 => OpticalEncodingMode.Utf8,
            Windows1254 => OpticalEncodingMode.Windows1254,
            _ => throw new ArgumentException("Geçersiz dosya kodlaması. Otomatik, utf-8 veya windows-1254 seçilebilir.")
        };
    }

    public static string ToName(OpticalEncodingMode mode)
    {
        return mode switch
        {
            OpticalEncodingMode.Utf8 => Utf8,
            OpticalEncodingMode.Windows1254 => Windows1254,
            _ => Auto
        };
    }

    public static string ToParserEncodingName(OpticalEncodingMode mode)
    {
        return mode switch
        {
            OpticalEncodingMode.Windows1254 => Windows1254,
            OpticalEncodingMode.Utf8 => Utf8,
            _ => Auto
        };
    }
}
