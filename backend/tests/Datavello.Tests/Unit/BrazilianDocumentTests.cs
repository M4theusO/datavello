using Datavello.Api.Features.Companies;

namespace Datavello.Tests.Unit;

public sealed class BrazilianDocumentTests
{
    [Theory]
    [InlineData("529.982.247-25", true)]
    [InlineData("52998224725", true)]
    [InlineData("52998224724", false)]
    [InlineData("11.222.333/0001-81", true)]
    [InlineData("11222333000180", false)]
    [InlineData("00000000000000", false)]
    [InlineData("1234567890", false)]
    public void Validates_cpf_and_cnpj_check_digits(string input, bool expected)
    {
        Assert.Equal(expected, BrazilianDocument.IsValid(BrazilianDocument.Digits(input)));
    }

    [Theory]
    [InlineData("52998224725", "529.982.247-25")]
    [InlineData("11222333000181", "11.222.333/0001-81")]
    public void Formats_documents(string digits, string expected)
    {
        Assert.Equal(expected, BrazilianDocument.Format(digits));
    }
}
