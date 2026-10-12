namespace Datavello.Api.Domain;

public enum ImportStatus
{
    /// <summary>File parsed and staged; waiting for mapping/confirmation.</summary>
    Pending,
    Completed,
    CompletedWithErrors,
    Failed
}

public class ImportJob : ITenantOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CompanyId { get; set; }
    public Guid UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public string FileName { get; set; } = string.Empty;

    /// <summary>"csv" or "xlsx".</summary>
    public string FileType { get; set; } = string.Empty;

    public ImportStatus Status { get; set; } = ImportStatus.Pending;
    public int TotalRows { get; set; }
    public int ImportedRows { get; set; }
    public int RejectedRows { get; set; }

    /// <summary>Header row and raw cell text (jsonb). Cleared after the import is confirmed.</summary>
    public string? StagingData { get; set; }

    /// <summary>Column mapping and parse options used on confirmation (jsonb).</summary>
    public string? Options { get; set; }

    public string? Message { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }

    public ICollection<ImportRowError> Errors { get; set; } = [];
}

public class ImportRowError : ITenantOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();
    public Guid CompanyId { get; set; }
    public Guid ImportJobId { get; set; }

    /// <summary>1-based row number as seen in the spreadsheet (header is row 1).</summary>
    public int RowNumber { get; set; }

    public string? Field { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? RawValue { get; set; }
}
