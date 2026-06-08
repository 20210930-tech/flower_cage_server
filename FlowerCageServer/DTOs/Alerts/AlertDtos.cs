using FlowerCageServer.Common.Enums;

namespace FlowerCageServer.DTOs.Alerts;

public record AlertEventResponse(
    Guid Id,
    Guid? CageDeviceId,
    Guid? PlantProfileId,
    AlertLevel Level,
    string Title,
    string Message,
    string Source,
    bool IsAcknowledged,
    DateTime OccurredAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);
