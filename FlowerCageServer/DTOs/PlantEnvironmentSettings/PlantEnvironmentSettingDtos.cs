using FlowerCageServer.Common.Enums;

namespace FlowerCageServer.DTOs.PlantEnvironmentSettings;

public record UpsertPlantEnvironmentSettingRequest(
    Guid PlantProfileId,
    decimal SoilMoistureMin,
    decimal SoilMoistureMax,
    decimal TemperatureMin,
    decimal TemperatureMax,
    decimal HumidityMin,
    decimal HumidityMax,
    decimal LightLuxMin,
    decimal LightLuxMax,
    bool AutoModeEnabled,
    bool AlertEnabled,
    ControlMode Mode = ControlMode.Manual);

public record PlantEnvironmentSettingResponse(
    Guid Id,
    Guid PlantProfileId,
    decimal SoilMoistureMin,
    decimal SoilMoistureMax,
    decimal TemperatureMin,
    decimal TemperatureMax,
    decimal HumidityMin,
    decimal HumidityMax,
    decimal LightLuxMin,
    decimal LightLuxMax,
    bool AutoModeEnabled,
    bool AlertEnabled,
    ControlMode Mode,
    DateTime CreatedAt,
    DateTime UpdatedAt);
