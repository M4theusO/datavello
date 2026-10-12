using Datavello.Api.Infrastructure.Http;
using Microsoft.AspNetCore.Mvc;

namespace Datavello.Api.Features.Imports;

[ApiController]
[Route("api/imports")]
[Produces("application/json")]
public sealed class ImportsController(ImportService imports) : ControllerBase
{
    /// <summary>Envia um arquivo .csv ou .xlsx e devolve cabeçalhos, prévia e sugestão de mapeamento.</summary>
    /// <remarks>Requisição multipart com o campo <c>file</c>. Nada é importado nesta etapa.</remarks>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(ImportSettings.RequestSizeCeiling)]
    [RequestFormLimits(MultipartBodyLengthLimit = ImportSettings.RequestSizeCeiling)]
    [ProducesResponseType<ImportPreviewDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImportPreviewDto>> Upload(IFormFile? file, CancellationToken ct)
    {
        var preview = await imports.UploadAsync(file, ct);
        return CreatedAtAction(nameof(Get), new { id = preview.Id }, preview);
    }

    /// <summary>Valida todas as linhas com o mapeamento e as opções informadas, sem gravar nada.</summary>
    [HttpPost("{id:guid}/validate")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<ImportValidationDto> Validate(Guid id, ImportOptions options, CancellationToken ct) =>
        imports.ValidateAsync(id, options, ct);

    /// <summary>Revalida e grava as linhas válidas em uma única transação. Linhas inválidas ficam registradas no histórico.</summary>
    [HttpPost("{id:guid}/commit")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public Task<ImportJobDetailDto> Commit(Guid id, ImportOptions options, CancellationToken ct) =>
        imports.CommitAsync(id, options, ct);

    /// <summary>Histórico de importações concluídas da empresa ativa.</summary>
    [HttpGet]
    public Task<PagedResult<ImportJobDto>> List([FromQuery] PageQuery page, CancellationToken ct) =>
        imports.ListAsync(page, ct);

    /// <summary>Detalhes de uma importação, com os erros por linha (até 500).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public Task<ImportJobDetailDto> Get(Guid id, CancellationToken ct) => imports.GetAsync(id, ct);

    /// <summary>Descarta uma importação ainda não confirmada.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken ct)
    {
        await imports.CancelAsync(id, ct);
        return NoContent();
    }
}
