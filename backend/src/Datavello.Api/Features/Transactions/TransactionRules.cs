using Datavello.Api.Infrastructure.Errors;

namespace Datavello.Api.Features.Transactions;

/// <summary>Business validation shared by manual entry and spreadsheet import.</summary>
public static class TransactionRules
{
    /// <summary>numeric(18,2): at most 16 integer digits.</summary>
    public const decimal MaxAmount = 9_999_999_999_999_999.99m;

    public static readonly DateOnly MinDate = new(1900, 1, 1);
    public static readonly DateOnly MaxDate = new(2100, 12, 31);

    /// <summary>Returns a pt-BR error message, or null when the amount is valid.</summary>
    public static string? ValidateAmount(decimal amount)
    {
        if (amount <= 0) return "O valor deve ser maior que zero.";
        if (amount > MaxAmount) return "O valor é muito alto.";
        if (decimal.Round(amount, 2) != amount) return "O valor deve ter no máximo 2 casas decimais.";
        return null;
    }

    public static string? ValidateDate(DateOnly date) =>
        date < MinDate || date > MaxDate ? "Data fora do intervalo permitido (1900 a 2100)." : null;

    public static void ValidateRange(DateOnly? from, DateOnly? to)
    {
        if (from is { } f && to is { } t && f > t)
        {
            throw AppException.BadRequest("A data inicial deve ser anterior ou igual à data final.");
        }
    }
}
