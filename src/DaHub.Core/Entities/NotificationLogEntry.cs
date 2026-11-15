namespace DaHub.Core.Entities;

public sealed class NotificationLogEntry
{
    public long Id { get; set; }
    public int? RequestId { get; set; }
    public required string Channel { get; set; }
    public required string Recipient { get; set; }
    public string? Subject { get; set; }
    public required string NotificationType { get; set; }
    public string Status { get; set; } = "Sent";
    public int AttemptCount { get; set; } = 1;
    public string? ErrorDetail { get; set; }
    public DateTime SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
}
