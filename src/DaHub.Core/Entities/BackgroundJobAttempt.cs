namespace DaHub.Core.Entities;

public sealed class BackgroundJobAttempt
{
    public long JobId { get; set; }
    public int Attempt { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? RetryAt { get; set; }
    public required string Outcome { get; set; }
    public string? ErrorCode { get; set; }
}
