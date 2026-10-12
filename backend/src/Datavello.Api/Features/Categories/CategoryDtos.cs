using System.ComponentModel.DataAnnotations;
using Datavello.Api.Domain;

namespace Datavello.Api.Features.Categories;

public sealed record CategoryDto(Guid Id, string Name, CategoryKind Kind, bool IsDefault, int TransactionCount);

public sealed class CategoryRequest
{
    /// <example>Fornecedores</example>
    [Required(ErrorMessage = "Informe o nome da categoria.")]
    [StringLength(80, MinimumLength = 2, ErrorMessage = "O nome deve ter entre 2 e 80 caracteres.")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Income (receitas), Expense (despesas) ou Both (ambos).</summary>
    [Required(ErrorMessage = "Informe o tipo da categoria.")]
    [EnumDataType(typeof(CategoryKind), ErrorMessage = "Tipo de categoria inválido.")]
    public CategoryKind? Kind { get; init; }
}
