// API endpointleri için ortak başarılı ve hatalı yanıt modelini tanımlar.
namespace SDS.Application.Common;

public sealed record ApiResponse<T>(bool Success, T? Data, string? Message, IReadOnlyCollection<string>? Errors)
{
    public static ApiResponse<T> Ok(T data, string? message = null) => new(true, data, message, null);
    public static ApiResponse<T> Fail(string message, IReadOnlyCollection<string>? errors = null) => new(false, default, message, errors);
}
