namespace Datavello.Api.Infrastructure.Errors;

/// <summary>Expected application error whose message is safe to show to the user (pt-BR).</summary>
public class AppException(int statusCode, string title, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
    public string Title { get; } = title;

    public static AppException NotFound(string message = "Recurso não encontrado.") =>
        new(StatusCodes.Status404NotFound, "Não encontrado", message);

    public static AppException Conflict(string message) =>
        new(StatusCodes.Status409Conflict, "Conflito", message);

    public static AppException BadRequest(string message) =>
        new(StatusCodes.Status400BadRequest, "Requisição inválida", message);

    public static AppException Forbidden(string message = "Você não tem acesso a este recurso.") =>
        new(StatusCodes.Status403Forbidden, "Acesso negado", message);
}
