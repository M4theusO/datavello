using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace Datavello.Api.Infrastructure.Http;

public class PageQuery
{
    public const int MaxPageSize = 100;

    /// <summary>Página (começa em 1).</summary>
    [Range(1, int.MaxValue, ErrorMessage = "A página deve ser maior ou igual a 1.")]
    public int Page { get; init; } = 1;

    /// <summary>Itens por página (1 a 100).</summary>
    [Range(1, MaxPageSize, ErrorMessage = "O tamanho da página deve estar entre 1 e 100.")]
    public int PageSize { get; init; } = 20;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public static class PagingExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(this IQueryable<T> query, PageQuery page, CancellationToken ct)
    {
        var total = await query.CountAsync(ct);
        var items = await query.Skip((page.Page - 1) * page.PageSize).Take(page.PageSize).ToListAsync(ct);
        return new PagedResult<T>(items, page.Page, page.PageSize, total);
    }
}

public static class LikePattern
{
    /// <summary>Escapes LIKE wildcards so user input is matched literally ("50%" finds "50%", not "50…").</summary>
    public static string Contains(string term) =>
        "%" + term.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_") + "%";
}
