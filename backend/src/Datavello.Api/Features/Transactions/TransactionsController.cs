using Datavello.Api.Infrastructure.Http;
using Microsoft.AspNetCore.Mvc;

namespace Datavello.Api.Features.Transactions;

[ApiController]
[Route("api/transactions")]
[Produces("application/json")]
public sealed class TransactionsController(TransactionService transactions) : ControllerBase
{
    /// <summary>Lista paginada de transações com filtros por período, categoria, tipo e busca na descrição.</summary>
    /// <remarks>Exemplo: <c>GET /api/transactions?from=2026-01-01&amp;to=2026-03-31&amp;type=Expense&amp;sortBy=Amount&amp;sortDir=Desc&amp;page=1&amp;pageSize=20</c></remarks>
    [HttpGet]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<PagedResult<TransactionDto>> List([FromQuery] TransactionQuery query, CancellationToken ct) =>
        transactions.ListAsync(query, ct);

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<TransactionDto> Get(Guid id, CancellationToken ct) => transactions.GetAsync(id, ct);

    [HttpPost]
    [ProducesResponseType<TransactionDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<TransactionDto>> Create(TransactionRequest request, CancellationToken ct)
    {
        var created = await transactions.CreateAsync(request, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<TransactionDto> Update(Guid id, TransactionRequest request, CancellationToken ct) =>
        transactions.UpdateAsync(id, request, ct);

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await transactions.DeleteAsync(id, ct);
        return NoContent();
    }
}
