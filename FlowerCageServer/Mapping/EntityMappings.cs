using FlowerCageServer.DTOs.Alerts;
using FlowerCageServer.DTOs.CageDevices;
using FlowerCageServer.DTOs.Controls;
using FlowerCageServer.DTOs.Gpt;
using FlowerCageServer.DTOs.GrowthLogs;
using FlowerCageServer.DTOs.PlantEnvironmentSettings;
using FlowerCageServer.DTOs.Plants;
using FlowerCageServer.DTOs.Sensors;
using FlowerCageServer.DTOs.SystemEvents;
using FlowerCageServer.DTOs.Users;
using FlowerCageServer.Entities;

namespace FlowerCageServer.Mapping;

public static class EntityMappings
{
    public static UserResponse ToResponse(this User entity) =>
        new(entity.Id, entity.Name, entity.CreatedAt, entity.UpdatedAt);

    public static CageDeviceResponse ToResponse(this CageDevice entity) =>
        new(entity.Id, entity.UserId, entity.DeviceName, entity.DeviceSerial, entity.WifiStatus, entity.IsOnline, entity.LastSeenAt, entity.CreatedAt, entity.UpdatedAt);

    public static PlantProfileResponse ToResponse(this PlantProfile entity) =>
        new(entity.Id, entity.UserId, entity.CageDeviceId, entity.PlantName, entity.Species, entity.RegisteredAt, entity.Memo, entity.CreatedAt, entity.UpdatedAt);

    public static PlantEnvironmentSettingResponse ToResponse(this PlantEnvironmentSetting entity) =>
        new(entity.Id, entity.PlantProfileId, entity.SoilMoistureMin, entity.SoilMoistureMax, entity.TemperatureMin, entity.TemperatureMax, entity.HumidityMin, entity.HumidityMax, entity.LightLuxMin, entity.LightLuxMax, entity.AutoModeEnabled, entity.AlertEnabled, entity.Mode, entity.CreatedAt, entity.UpdatedAt);

    public static SensorReadingResponse ToResponse(this SensorReading entity) =>
        new(entity.Id, entity.CageDeviceId, entity.PlantProfileId,
            entity.SoilMoisture, entity.Temperature, entity.Humidity, entity.LightLux,
            entity.SoilMoistures, entity.Temperatures, entity.Humidities,
            entity.Nm415, entity.Nm445, entity.Nm480, entity.Nm515,
            entity.Nm555, entity.Nm590, entity.Nm630, entity.Nm680,
            entity.Clear, entity.Nir,
            entity.RecordedAt, entity.CreatedAt, entity.UpdatedAt);

    public static ControlCommandResponse ToResponse(this ControlCommand entity) =>
        new(entity.Id, entity.CageDeviceId, entity.RequestedByUserId, entity.CommandType, entity.WaterPumpOn, entity.WaterAmountMl, entity.LedOn, entity.LedBrightness, entity.AutoModeOn, entity.AlertEnabled, entity.Reason, entity.SourceType, entity.Status, entity.RequestedAt, entity.AppliedAt, entity.CreatedAt, entity.UpdatedAt, entity.WaterDurationSec, entity.LedR, entity.LedG, entity.LedB);

    public static ControlScheduleResponse ToResponse(this ControlSchedule entity) =>
        new(entity.Id, entity.CageDeviceId, entity.PlantProfileId, entity.Type, entity.Repeat,
            entity.Enabled, entity.Label, entity.Hour, entity.Minute,
            entity.DayOfWeek, entity.DayOfMonth, entity.Month, entity.Date,
            entity.WaterDurationSec, entity.LedR, entity.LedG, entity.LedB,
            entity.LastFiredAt, entity.CreatedAt, entity.UpdatedAt);

    public static AlertEventResponse ToResponse(this AlertEvent entity) =>
        new(entity.Id, entity.CageDeviceId, entity.PlantProfileId, entity.Level, entity.Title, entity.Message, entity.Source, entity.IsAcknowledged, entity.OccurredAt, entity.CreatedAt, entity.UpdatedAt);

    public static GrowthLogResponse ToResponse(this GrowthLog entity) =>
        new(entity.Id, entity.PlantProfileId, entity.Title, entity.Content, entity.HeightCm, entity.CameraImagePlaceholderPath, entity.CameraImagePublicUrl, entity.LoggedAt, entity.CreatedAt, entity.UpdatedAt);

    public static GptAnalysisResponse ToResponse(this GptAnalysisLog entity) =>
        new(entity.Id, entity.PlantProfileId, entity.SensorReadingId, entity.AnalysisType, entity.PromptSummary, entity.ResponseContent, entity.Recommendation, entity.Status, entity.RequestedAt, entity.CompletedAt, entity.CreatedAt, entity.UpdatedAt);

    public static SystemEventLogResponse ToResponse(this SystemEventLog entity) =>
        new(entity.Id, entity.EventType, entity.EventSource, entity.Message, entity.PayloadJson, entity.OccurredAt, entity.CreatedAt, entity.UpdatedAt);
}
