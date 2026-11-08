namespace DaHub.Core.Entities;

public sealed class AccessTemplateGroup
{
    public int Id { get; set; }
    public int TemplateId { get; set; }
    public required string GroupType { get; set; }
    public required string GroupName { get; set; }
    public required string GroupIdentifier { get; set; }
    public bool IsRequired { get; set; } = false;
    public DateTime CreatedAt { get; set; }
}
