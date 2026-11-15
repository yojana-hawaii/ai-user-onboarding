namespace DaHub.Core.Entities;

public sealed class UrgentExecution
{
    public int Id { get; set; }
    public int RequestId { get; set; }
    public int Revision { get; set; }
    public required string ActorUPN { get; set; }
    public required string Justification { get; set; }
    public required string ActionsJson { get; set; }
    public DateTime ExecutedAt { get; set; }
}
