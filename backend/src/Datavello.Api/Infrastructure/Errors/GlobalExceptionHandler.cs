using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Datavello.Api.Infrastructure.Errors;

/// <summary>
/// Converts unhandled exceptions into RFC 9457 ProblemDetails responses.
/// Known application errors keep their message; anything else returns a generic
/// message and the details go only to the log.
/// </summary>
public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, title, detail) = exception switch
        {
            AppException app => (app.StatusCode, app.Title, app.Message),
            BadHttpRequestException bad => (bad.StatusCode, "Requisição inválida", "Não foi possível ler a requisição."),
            _ => (StatusCodes.Status500InternalServerError, "Erro inesperado", "Ocorreu um erro inesperado. Tente novamente em instantes.")
        };

        if (status >= 500)
        {
            logger.LogError(exception, "Unhandled exception on {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail }
        });
    }
}
