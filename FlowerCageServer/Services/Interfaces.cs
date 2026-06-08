using FlowerCageServer.Common.Enums;
using FlowerCageServer.DTOs.Alerts;
using FlowerCageServer.DTOs.AutoControl;
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

namespace FlowerCageServer.Services;

public interface ISystemEventLogService
{
    Task LogAsync(string eventType, string eventSource, string message, object? payload = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<SystemEventLogResponse>> GetLogsAsync(CancellationToken cancellationToken = default);
}

public interface IUserService
{
    Task<UserResponse> CreateAsync(UserCreateRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<UserResponse>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<UserResponse> UpdateAsync(Guid id, UserUpdateRequest request, CancellationToken cancellationToken = default);
}

public interface ICageDeviceService
{
    Task<CageDeviceResponse> CreateAsync(CageDeviceCreateRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<CageDeviceResponse>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<CageDeviceResponse> UpdateAsync(Guid id, CageDeviceUpdateRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IPlantProfileService
{
    Task<PlantProfileResponse> CreateAsync(PlantProfileCreateRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<PlantProfileResponse>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<PlantProfileResponse> UpdateAsync(Guid id, PlantProfileUpdateRequest request, CancellationToken cancellationToken = default);
}

public interface IPlantEnvironmentSettingService
{
    Task<PlantEnvironmentSettingResponse> UpsertAsync(UpsertPlantEnvironmentSettingRequest request, CancellationToken cancellationToken = default);
    Task<PlantEnvironmentSettingResponse> GetByPlantProfileAsync(Guid plantProfileId, CancellationToken cancellationToken = default);
}

public interface IAlertService
{
    Task<AlertEvent> CreateAsync(AlertLevel level, string title, string message, string source, Guid? cageDeviceId, Guid? plantProfileId, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<AlertEventResponse>> GetAsync(Guid? cageDeviceId, Guid? plantProfileId, CancellationToken cancellationToken = default);
}

public interface ISensorReadingService
{
    Task<SensorReadingCreateResponse> CreateAsync(SensorReadingCreateRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<SensorReadingResponse>> GetAsync(Guid? cageDeviceId, Guid? plantProfileId, int take, CancellationToken cancellationToken = default);
}

public interface IGrowthLogService
{
    Task<GrowthLogResponse> CreateAsync(GrowthLogCreateRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<GrowthLogResponse>> GetAsync(Guid plantProfileId, CancellationToken cancellationToken = default);
}

public interface IControlCommandService
{
    Task<ControlCommandResponse> CreateAsync(ControlCommandCreateRequest request, CancellationToken cancellationToken = default);
    Task<ControlCommandResponse> UpdateStatusAsync(Guid id, ControlCommandStatusUpdateRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<ControlCommandResponse>> GetAsync(Guid cageDeviceId, CommandStatus? status = null, int take = 50, CancellationToken cancellationToken = default);
}

public interface IControlScheduleService
{
    Task<IReadOnlyCollection<ControlScheduleResponse>> GetAsync(Guid cageDeviceId, CancellationToken cancellationToken = default);
    Task<ControlScheduleResponse> CreateAsync(ControlScheduleCreateRequest request, CancellationToken cancellationToken = default);
    Task<ControlScheduleResponse> SetEnabledAsync(Guid id, bool enabled, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IGptAnalysisService
{
    Task<GptAnalysisResponse> AnalyzeAsync(GptAnalysisRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyCollection<GptAnalysisResponse>> GetLogsAsync(Guid? plantProfileId, CancellationToken cancellationToken = default);
}

public interface IAutoControlService
{
    Task<AutoControlEvaluationResponse> EvaluateAsync(AutoControlEvaluationRequest request, CancellationToken cancellationToken = default);
}
