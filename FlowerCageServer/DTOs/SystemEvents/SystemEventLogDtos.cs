namespace FlowerCageServer.DTOs.SystemEvents;

public record SystemEventLogResponse(
    Guid Id,
    string EventType,
    string EventSource,
    string Message,
    string? PayloadJson,
    DateTime OccurredAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);
