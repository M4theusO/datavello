namespace Datavello.Api.Features.Imports;

/// <summary>Limits for spreadsheet imports (configuration section "Import").</summary>
public sealed class ImportSettings
{
    public const string Section = "Import";

    /// <summary>Hard ceiling for the upload request, regardless of configuration.</summary>
    public const long RequestSizeCeiling = 20 * 1024 * 1024;

    public long MaxFileSizeBytes { get; set; } = 5 * 1024 * 1024;
    public int MaxRows { get; set; } = 5000;
    public int PreviewRows { get; set; } = 20;

    /// <summary>Pending (unconfirmed) imports older than this are discarded.</summary>
    public TimeSpan PendingExpiration { get; set; } = TimeSpan.FromHours(24);
}
