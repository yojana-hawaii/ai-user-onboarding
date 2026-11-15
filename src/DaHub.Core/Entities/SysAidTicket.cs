namespace DaHub.Core.Entities;

public sealed class SysAidTicket
{
    public int Id { get; set; }
    public int RequestId { get; set; }
    public int SysAidSrId { get; set; }
    public required string TicketType { get; set; }
    public required string AssignedToUPN { get; set; }
    public string Status { get; set; } = "Open";
    public string? SysAidUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
}
