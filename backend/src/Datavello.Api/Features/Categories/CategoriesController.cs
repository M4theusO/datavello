using Datavello.Api.Domain;
using Microsoft.AspNetCore.Mvc;

namespace Datavello.Api.Features.Categories;

[ApiController]
[Route("api/categories")]
[Produces("application/json")]
public sealed class CategoriesController(CategoryService categories) : ControllerBase
{
    /// <summary>Lista as categorias da empresa ativa, com a quantidade de transações de cada uma.</summary>
    /// <param name="kind">Filtra pelo tipo de transação aceito (categorias "Both" sempre aparecem).</param>
    [HttpGet]
    public Task<IReadOnlyList<CategoryDto>> List([FromQuery] CategoryKind? kind, CancellationToken ct) =>
        categories.ListAsync(kind, ct);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<CategoryDto> Get(Guid id, CancellationToken ct) => categories.GetAsync(id, ct);

    [HttpPost]
    [ProducesResponseType<CategoryDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryDto>> Create(CategoryRequest request, CancellationToken ct)
    {
        var created = await categories.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>Atualiza nome e tipo. Não permite restringir o tipo se houver transações incompatíveis.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<CategoryDto> Update(Guid id, CategoryRequest request, CancellationToken ct) =>
        categories.UpdateAsync(id, request, ct);

    /// <summary>Exclui a categoria. Retorna 409 se houver transações vinculadas.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await categories.DeleteAsync(id, ct);
        return NoContent();
    }
}
