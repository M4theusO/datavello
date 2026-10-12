namespace Datavello.Api.Features.Companies;

/// <summary>CPF (11 digits) and CNPJ (14 digits) normalization and check-digit validation.</summary>
public static class BrazilianDocument
{
    public static string Digits(string value) => new(value.Where(char.IsAsciiDigit).ToArray());

    public static bool IsValid(string digits) => digits.Length switch
    {
        11 => IsValidCpf(digits),
        14 => IsValidCnpj(digits),
        _ => false
    };

    /// <summary>"12345678000195" → "12.345.678/0001-95"; "52998224725" → "529.982.247-25".</summary>
    public static string Format(string digits) => digits.Length switch
    {
        11 => $"{digits[..3]}.{digits[3..6]}.{digits[6..9]}-{digits[9..]}",
        14 => $"{digits[..2]}.{digits[2..5]}.{digits[5..8]}/{digits[8..12]}-{digits[12..]}",
        _ => digits
    };

    private static bool IsValidCpf(string cpf)
    {
        if (cpf.Distinct().Count() == 1) return false;
        return CheckDigit(cpf[..9], [10, 9, 8, 7, 6, 5, 4, 3, 2]) == cpf[9] - '0'
            && CheckDigit(cpf[..10], [11, 10, 9, 8, 7, 6, 5, 4, 3, 2]) == cpf[10] - '0';
    }

    private static bool IsValidCnpj(string cnpj)
    {
        if (cnpj.Distinct().Count() == 1) return false;
        return CheckDigit(cnpj[..12], [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]) == cnpj[12] - '0'
            && CheckDigit(cnpj[..13], [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2]) == cnpj[13] - '0';
    }

    private static int CheckDigit(string digits, int[] weights)
    {
        var sum = digits.Select((d, i) => (d - '0') * weights[i]).Sum();
        var rest = sum % 11;
        return rest < 2 ? 0 : 11 - rest;
    }
}
