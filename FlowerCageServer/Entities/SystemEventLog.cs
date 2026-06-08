namespace FlowerCageServer.Entities;

public class SystemEventLog : BaseEntity
{
    public string EventType { get; set; } = string.Empty;
    public string EventSource { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? PayloadJson { get; set; }
    public DateTime OccurredAt { get; set; }
}
