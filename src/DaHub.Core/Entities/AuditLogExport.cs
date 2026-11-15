namespace DaHub.Core.Entities;

public sealed class AuditLogExport
{
    public int Id { get; set; }
    public required string ExportedByUPN { get; set; }
    public required string ExporterRole { get; set; }
    public required string Format { get; set; }
    public DateTime? FilterDateFrom { get; set; }
    public DateTime? FilterDateTo { get; set; }
    public string? FilterActorUPN { get; set; }
    public string? FilterTargetUPN { get; set; }
    public string? FilterActionType { get; set; }
    public int RowCount { get; set; } = 0;
    public string? IPAddress { get; set; }
    public DateTime ExportedAt { get; set; }
}
