namespace DaHub.Core.Entities;

public sealed class RequestRevision
{
    public int RequestId { get; set; }
    public int Revision { get; set; }
    public required string SnapshotJson { get; set; }
    public required string CreatedByUPN { get; set; }
    public DateTime CreatedAt { get; set; }
    public required string ChangeReason { get; set; }
}
