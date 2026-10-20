// Uygulama hatalarını ortak Türkçe API yanıtına dönüştürür.
using System.Text.Json;
using SDS.Application.Common;

namespace SDS.Api.Middleware;

public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try { await next(context); }
        catch (Exception exception)
        {
            logger.LogError(exception, "İstek işlenirken hata oluştu.");
            context.Response.StatusCode = exception switch { KeyNotFoundException => StatusCodes.Status404NotFound, InvalidOperationException => StatusCodes.Status409Conflict, ArgumentException => StatusCodes.Status400BadRequest, _ => StatusCodes.Status500InternalServerError };
            context.Response.ContentType = "application/json";
            var message = context.Response.StatusCode == StatusCodes.Status500InternalServerError ? "Beklenmeyen bir sunucu hatası oluştu." : exception.Message;
            await context.Response.WriteAsync(JsonSerializer.Serialize(ApiResponse<object>.Fail(message)));
        }
    }
}
