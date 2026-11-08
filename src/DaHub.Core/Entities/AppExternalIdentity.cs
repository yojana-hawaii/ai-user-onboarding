namespace DaHub.Core.Entities;

public sealed class AppExternalIdentity
{
    public int Id { get; set; }
    public int AppUserId { get; set; }
    public required string ProviderKey { get; set; }
    public required string Issuer { get; set; }
    public required string Subject { get; set; }
    public int? LinkedByUserId { get; set; }
    public required string LinkMethod { get; set; }
    public DateTime LinkedAt { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? RevokedAt { get; set; }
}
