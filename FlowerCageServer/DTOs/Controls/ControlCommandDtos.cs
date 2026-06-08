using FlowerCageServer.Common.Enums;

namespace FlowerCageServer.DTOs.Controls;

public record ControlCommandCreateRequest(
    Guid CageDeviceId,
    Guid? RequestedByUserId,
    CommandType CommandType,
    bool? WaterPumpOn,
    decimal? WaterAmountMl,
    bool? LedOn,
    int? LedBrightness,
    bool? AutoModeOn,
    bool? AlertEnabled,
    string? Reason,
    SourceType SourceType,
    int? WaterDurationSec = null,   // 급수 시간(초)
    int? LedR = null,               // LED RGB (0~255)
    int? LedG = null,
    int? LedB = null);

public record ControlCommandStatusUpdateRequest(CommandStatus Status, DateTime? AppliedAt);

public record ControlCommandResponse(
    Guid Id,
    Guid CageDeviceId,
    Guid? RequestedByUserId,
    CommandType CommandType,
    bool? WaterPumpOn,
    decimal? WaterAmountMl,
    bool? LedOn,
    int? LedBrightness,
    bool? AutoModeOn,
    bool? AlertEnabled,
    string? Reason,
    SourceType SourceType,
    CommandStatus Status,
    DateTime RequestedAt,
    DateTime? AppliedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int? WaterDurationSec = null,
    int? LedR = null,
    int? LedG = null,
    int? LedB = null);
