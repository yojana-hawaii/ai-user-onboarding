namespace DaHub.Core.Entities;

public sealed class RequestAccessTemplate
{
    public int RequestId { get; set; }
    public int TemplateId { get; set; }
    public required string MatchedOn { get; set; }
    public bool ITApproved { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
