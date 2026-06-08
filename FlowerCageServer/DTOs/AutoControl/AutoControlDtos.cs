using FlowerCageServer.DTOs.Alerts;
using FlowerCageServer.DTOs.Controls;

namespace FlowerCageServer.DTOs.AutoControl;

public record AutoControlEvaluationRequest(Guid PlantProfileId, Guid? SensorReadingId);

public record AutoControlEvaluationResponse(
    Guid PlantProfileId,
    Guid SensorReadingId,
    bool AutoModeEnabled,
    List<string> Decisions,
    List<ControlCommandResponse> Commands,
    List<AlertEventResponse> Alerts);
