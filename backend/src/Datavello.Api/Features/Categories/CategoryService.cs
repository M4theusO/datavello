using Datavello.Api.Domain;
using Datavello.Api.Infrastructure.Data;
using Datavello.Api.Infrastructure.Errors;
using Datavello.Api.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Datavello.Api.Features.Categories;

/// <summary>Categories of the active company. Tenant isolation comes from the DbContext query filter.</summary>
public sealed class CategoryService(AppDbContext db, ICurrentTenant tenant)
{
    public async Task<IReadOnlyList<CategoryDto>> ListAsync(CategoryKind? kind, CancellationToken ct)
    {
        tenant.RequireCompanyId();

        var query = db.Categories.AsNoTracking();
        if (kind is { } k)
        {
            // "Both" categories are usable for either type, so they are always included.
            query = query.Where(c => c.Kind == k || c.Kind == CategoryKind.Both);
        }

        return await query
            .OrderBy(c => c.Name)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Kind, c.IsDefault, db.Transactions.Count(t => t.CategoryId == c.Id)))
            .ToListAsync(ct);
    }

    public async Task<CategoryDto> GetAsync(Guid id, CancellationToken ct)
    {
        tenant.RequireCompanyId();
        return await db.Categories.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new CategoryDto(c.Id, c.Name, c.Kind, c.IsDefault, db.Transactions.Count(t => t.CategoryId == c.Id)))
            .SingleOrDefaultAsync(ct) ?? throw AppException.NotFound("Categoria não encontrada.");
    }

    public async Task<CategoryDto> CreateAsync(CategoryRequest request, CancellationToken ct)
    {
        tenant.RequireCompanyId();
        var name = request.Name.Trim();
        await EnsureUniqueNameAsync(name, exceptId: null, ct);

        var category = new Category { Name = name, Kind = request.Kind!.Value };
        db.Categories.Add(category);
        await SaveAsync(ct);

        return new CategoryDto(category.Id, category.Name, category.Kind, category.IsDefault, 0);
    }

    public async Task<CategoryDto> UpdateAsync(Guid id, CategoryRequest request, CancellationToken ct)
    {
        tenant.RequireCompanyId();
        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id, ct)
            ?? throw AppException.NotFound("Categoria não encontrada.");

        var name = request.Name.Trim();
        var kind = request.Kind!.Value;
        await EnsureUniqueNameAsync(name, exceptId: id, ct);

        if (kind != category.Kind && kind != CategoryKind.Both)
        {
            // Narrowing the kind must not leave transactions in a category that no longer accepts them.
            var conflictingType = kind == CategoryKind.Income ? TransactionType.Expense : TransactionType.Income;
            var conflicts = await db.Transactions.CountAsync(t => t.CategoryId == id && t.Type == conflictingType, ct);
            if (conflicts > 0)
            {
                var label = conflictingType == TransactionType.Expense ? "despesas" : "receitas";
                throw AppException.Conflict(
                    $"Esta categoria possui {conflicts} {label} vinculadas. Reclassifique-as antes de alterar o tipo.");
            }
        }

        category.Name = name;
        category.Kind = kind;
        await SaveAsync(ct);

        return await GetAsync(id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        tenant.RequireCompanyId();
        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id, ct)
            ?? throw AppException.NotFound("Categoria não encontrada.");

        var inUse = await db.Transactions.CountAsync(t => t.CategoryId == id, ct);
        if (inUse > 0)
        {
            throw AppException.Conflict(
                $"Esta categoria possui {inUse} transação(ões) vinculada(s). Reclassifique-as antes de excluir.");
        }

        db.Categories.Remove(category);
        await SaveAsync(ct);
    }

    private async Task EnsureUniqueNameAsync(string name, Guid? exceptId, CancellationToken ct)
    {
        var lower = name.ToLower();
        var exists = await db.Categories.AnyAsync(c => c.Name.ToLower() == lower && c.Id != exceptId, ct);
        if (exists)
        {
            throw AppException.Conflict($"Já existe uma categoria chamada \"{name}\".");
        }
    }

    private async Task SaveAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (ex.IsUniqueViolation())
        {
            throw AppException.Conflict("Já existe uma categoria com este nome.");
        }
        catch (DbUpdateException ex) when (ex.IsForeignKeyViolation())
        {
            throw AppException.Conflict("Esta categoria está em uso e não pode ser excluída.");
        }
    }
}
