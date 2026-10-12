namespace Datavello.Api.Infrastructure.Security;

/// <summary>
/// CSRF defense in depth (on top of SameSite=Strict cookies): state-changing API calls must
/// carry a custom header. Cross-site HTML forms cannot set it, and cross-origin scripts can
/// only send it after a CORS preflight, which is accepted only for allowed origins.
/// </summary>
public sealed class RequireClientHeaderMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Requested-With";
    public const string HeaderValue = "datavello";

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;
        var isUnsafe = !(HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method));

        if (isUnsafe
            && request.Path.StartsWithSegments("/api")
            && !string.Equals(request.Headers[HeaderName], HeaderValue, StringComparison.Ordinal))
        {
            await Results.Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Requisição inválida",
                detail: $"Cabeçalho {HeaderName} ausente ou inválido.").ExecuteAsync(context);
            return;
        }

        await next(context);
    }
}
