using FlowerCageServer.Common.Enums;

namespace FlowerCageServer.DTOs.Controls;

public record ControlScheduleCreateRequest(
    Guid CageDeviceId,
    Guid? PlantProfileId,
    ScheduleType Type,
    ScheduleRepeat Repeat,
    int Hour,
    int Minute,
    int? DayOfWeek = null,
    int? DayOfMonth = null,
    int? Month = null,
    DateTime? Date = null,
    int? WaterDurationSec = null,
    int? LedR = null,
    int? LedG = null,
    int? LedB = null,
    string? Label = null,
    bool Enabled = true);

public record ControlScheduleEnabledUpdateRequest(bool Enabled);

public record ControlScheduleResponse(
    Guid Id,
    Guid CageDeviceId,
    Guid? PlantProfileId,
    ScheduleType Type,
    ScheduleRepeat Repeat,
    bool Enabled,
    string? Label,
    int Hour,
    int Minute,
    int? DayOfWeek,
    int? DayOfMonth,
    int? Month,
    DateTime? Date,
    int? WaterDurationSec,
    int? LedR,
    int? LedG,
    int? LedB,
    DateTime? LastFiredAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);
