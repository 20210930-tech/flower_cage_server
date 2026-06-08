using System.Net;
using FlowerCageServer.Common.Exceptions;
using FlowerCageServer.DTOs.PlantEnvironmentSettings;
using FlowerCageServer.Entities;
using FlowerCageServer.Mapping;
using FlowerCageServer.Repositories;

namespace FlowerCageServer.Services;

public class PlantEnvironmentSettingService : IPlantEnvironmentSettingService
{
    private readonly IRepository<PlantEnvironmentSetting> _settingRepository;
    private readonly IRepository<PlantProfile> _plantRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISystemEventLogService _systemEventLogService;

    public PlantEnvironmentSettingService(IRepository<PlantEnvironmentSetting> settingRepository, IRepository<PlantProfile> plantRepository, IUnitOfWork unitOfWork, ISystemEventLogService systemEventLogService)
    {
        _settingRepository = settingRepository;
        _plantRepository = plantRepository;
        _unitOfWork = unitOfWork;
        _systemEventLogService = systemEventLogService;
    }

    public async Task<PlantEnvironmentSettingResponse> UpsertAsync(UpsertPlantEnvironmentSettingRequest request, CancellationToken cancellationToken = default)
    {
        if (await _plantRepository.GetByIdAsync(request.PlantProfileId, cancellationToken) is null)
        {
            throw new ApiException("Plant profile not found.", HttpStatusCode.NotFound);
        }

        var entity = await _settingRepository.FirstOrDefaultAsync(x => x.PlantProfileId == request.PlantProfileId, cancellationToken);
        if (entity is null)
        {
            entity = new PlantEnvironmentSetting { PlantProfileId = request.PlantProfileId };
            await _settingRepository.AddAsync(entity, cancellationToken);
        }

        entity.SoilMoistureMin = request.SoilMoistureMin;
        entity.SoilMoistureMax = request.SoilMoistureMax;
        entity.TemperatureMin = request.TemperatureMin;
        entity.TemperatureMax = request.TemperatureMax;
        entity.HumidityMin = request.HumidityMin;
        entity.HumidityMax = request.HumidityMax;
        entity.LightLuxMin = request.LightLuxMin;
        entity.LightLuxMax = request.LightLuxMax;
        entity.Mode = request.Mode;
        // 호환: 기존 AutoModeEnabled 소비자를 위해 Mode에서 파생 (Manual이 아니면 자동화 on)
        entity.AutoModeEnabled = request.Mode != Common.Enums.ControlMode.Manual;
        entity.AlertEnabled = request.AlertEnabled;

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _systemEventLogService.LogAsync("EnvironmentSettingUpserted", nameof(PlantEnvironmentSettingService), "Plant environment setting has been saved.", new { entity.PlantProfileId }, cancellationToken);
        return entity.ToResponse();
    }

    public async Task<PlantEnvironmentSettingResponse> GetByPlantProfileAsync(Guid plantProfileId, CancellationToken cancellationToken = default)
    {
        var entity = await _settingRepository.FirstOrDefaultAsync(x => x.PlantProfileId == plantProfileId, cancellationToken)
            ?? throw new ApiException("Plant environment setting not found.", HttpStatusCode.NotFound);

        return entity.ToResponse();
    }
}
