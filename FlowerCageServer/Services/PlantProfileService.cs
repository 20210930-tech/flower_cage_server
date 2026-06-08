using System.Net;
using Microsoft.EntityFrameworkCore;
using FlowerCageServer.Common.Exceptions;
using FlowerCageServer.DTOs.Plants;
using FlowerCageServer.Entities;
using FlowerCageServer.Mapping;
using FlowerCageServer.Repositories;

namespace FlowerCageServer.Services;

public class PlantProfileService : IPlantProfileService
{
    private readonly IRepository<PlantProfile> _plantRepository;
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<CageDevice> _deviceRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISystemEventLogService _systemEventLogService;

    public PlantProfileService(IRepository<PlantProfile> plantRepository, IRepository<User> userRepository, IRepository<CageDevice> deviceRepository, IUnitOfWork unitOfWork, ISystemEventLogService systemEventLogService)
    {
        _plantRepository = plantRepository;
        _userRepository = userRepository;
        _deviceRepository = deviceRepository;
        _unitOfWork = unitOfWork;
        _systemEventLogService = systemEventLogService;
    }

    public async Task<PlantProfileResponse> CreateAsync(PlantProfileCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (await _userRepository.GetByIdAsync(request.UserId, cancellationToken) is null)
        {
            throw new ApiException("User not found.", HttpStatusCode.NotFound);
        }

        if (await _deviceRepository.GetByIdAsync(request.CageDeviceId, cancellationToken) is null)
        {
            throw new ApiException("Cage device not found.", HttpStatusCode.NotFound);
        }

        var entity = new PlantProfile
        {
            UserId = request.UserId,
            CageDeviceId = request.CageDeviceId,
            PlantName = request.PlantName,
            Species = request.Species,
            RegisteredAt = request.RegisteredAt,
            Memo = request.Memo
        };

        // 앱이 BLE로 기기에 미리 보낸 ID가 있으면 그대로 사용
        if (request.Id is Guid providedId && providedId != Guid.Empty)
        {
            entity.Id = providedId;
        }

        await _plantRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _systemEventLogService.LogAsync("PlantProfileCreated", nameof(PlantProfileService), "Plant profile has been created.", new { entity.Id, entity.PlantName }, cancellationToken);
        return entity.ToResponse();
    }

    public async Task<IReadOnlyCollection<PlantProfileResponse>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var plants = await _plantRepository.Query()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.RegisteredAt)
            .ToListAsync(cancellationToken);

        return plants.Select(x => x.ToResponse()).ToList();
    }

    public async Task<PlantProfileResponse> UpdateAsync(Guid id, PlantProfileUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _plantRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new ApiException("Plant profile not found.", HttpStatusCode.NotFound);

        entity.PlantName = request.PlantName;
        entity.Species = request.Species;
        entity.Memo = request.Memo;

        _plantRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _systemEventLogService.LogAsync("PlantProfileUpdated", nameof(PlantProfileService), "Plant profile has been updated.", new { entity.Id }, cancellationToken);
        return entity.ToResponse();
    }
}
