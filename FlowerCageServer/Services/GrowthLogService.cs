using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using FlowerCageServer.Common.Exceptions;
using FlowerCageServer.Common.Options;
using FlowerCageServer.DTOs.GrowthLogs;
using FlowerCageServer.Entities;
using FlowerCageServer.Mapping;
using FlowerCageServer.Repositories;

namespace FlowerCageServer.Services;

public class GrowthLogService : IGrowthLogService
{
    private readonly IRepository<GrowthLog> _growthLogRepository;
    private readonly IRepository<PlantProfile> _plantRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISystemEventLogService _systemEventLogService;
    private readonly StorageOptions _storageOptions;

    public GrowthLogService(IRepository<GrowthLog> growthLogRepository, IRepository<PlantProfile> plantRepository, IUnitOfWork unitOfWork, ISystemEventLogService systemEventLogService, IOptions<StorageOptions> storageOptions)
    {
        _growthLogRepository = growthLogRepository;
        _plantRepository = plantRepository;
        _unitOfWork = unitOfWork;
        _systemEventLogService = systemEventLogService;
        _storageOptions = storageOptions.Value;
    }

    public async Task<GrowthLogResponse> CreateAsync(GrowthLogCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (await _plantRepository.GetByIdAsync(request.PlantProfileId, cancellationToken) is null)
        {
            throw new ApiException("Plant profile not found.", HttpStatusCode.NotFound);
        }

        var placeholderFileName = string.IsNullOrWhiteSpace(request.CameraImagePlaceholderName)
            ? null
            : request.CameraImagePlaceholderName.Trim();

        var placeholderPath = placeholderFileName is null
            ? null
            : Path.Combine(_storageOptions.CameraImageBasePath, placeholderFileName).Replace("\\", "/");

        var publicUrl = string.IsNullOrWhiteSpace(_storageOptions.PublicBaseUrl) || placeholderPath is null
            ? null
            : $"{_storageOptions.PublicBaseUrl.TrimEnd('/')}/{placeholderPath}";

        var entity = new GrowthLog
        {
            PlantProfileId = request.PlantProfileId,
            Title = request.Title,
            Content = request.Content,
            HeightCm = request.HeightCm,
            LoggedAt = request.LoggedAt,
            CameraImagePlaceholderPath = placeholderPath,
            CameraImagePublicUrl = publicUrl
        };

        await _growthLogRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _systemEventLogService.LogAsync("GrowthLogCreated", nameof(GrowthLogService), "Growth log has been created.", new { entity.Id, entity.PlantProfileId }, cancellationToken);
        return entity.ToResponse();
    }

    public async Task<IReadOnlyCollection<GrowthLogResponse>> GetAsync(Guid plantProfileId, CancellationToken cancellationToken = default)
    {
        var logs = await _growthLogRepository.Query()
            .Where(x => x.PlantProfileId == plantProfileId)
            .OrderByDescending(x => x.LoggedAt)
            .ToListAsync(cancellationToken);

        return logs.Select(x => x.ToResponse()).ToList();
    }
}
