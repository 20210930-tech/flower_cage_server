using System.Net;
using Microsoft.EntityFrameworkCore;
using FlowerCageServer.Common.Exceptions;
using FlowerCageServer.DTOs.CageDevices;
using FlowerCageServer.Entities;
using FlowerCageServer.Mapping;
using FlowerCageServer.Repositories;

namespace FlowerCageServer.Services;

public class CageDeviceService : ICageDeviceService
{
    // 마지막 센서 수신 후 이 시간이 지나면 오프라인으로 본다. (센서 주기 5초 → 여유 있게 90초)
    private static readonly TimeSpan OnlineThreshold = TimeSpan.FromSeconds(90);

    private readonly IRepository<CageDevice> _deviceRepository;
    private readonly IRepository<User> _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISystemEventLogService _systemEventLogService;

    public CageDeviceService(IRepository<CageDevice> deviceRepository, IRepository<User> userRepository, IUnitOfWork unitOfWork, ISystemEventLogService systemEventLogService)
    {
        _deviceRepository = deviceRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _systemEventLogService = systemEventLogService;
    }

    public async Task<CageDeviceResponse> CreateAsync(CageDeviceCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (await _userRepository.GetByIdAsync(request.UserId, cancellationToken) is null)
        {
            throw new ApiException("User not found.", HttpStatusCode.NotFound);
        }

        var entity = new CageDevice
        {
            UserId = request.UserId,
            DeviceName = request.DeviceName,
            DeviceSerial = request.DeviceSerial,
            WifiStatus = request.WifiStatus,
            IsOnline = request.IsOnline,
            LastSeenAt = request.LastSeenAt
        };

        // 앱이 BLE로 기기에 미리 보낸 ID가 있으면 그대로 사용 (아두이노와 ID 일치)
        if (request.Id is Guid providedId && providedId != Guid.Empty)
        {
            entity.Id = providedId;
        }

        await _deviceRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _systemEventLogService.LogAsync("DeviceCreated", nameof(CageDeviceService), "Cage device has been registered.", new { entity.Id, entity.DeviceSerial }, cancellationToken);
        return entity.ToResponse();
    }

    public async Task<IReadOnlyCollection<CageDeviceResponse>> GetByUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var devices = await _deviceRepository.Query()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync(cancellationToken);

        // 저장된 IsOnline 플래그 대신 마지막 통신 시각으로 실시간 판정.
        var now = DateTime.UtcNow;
        return devices
            .Select(x => x.ToResponse() with
            {
                IsOnline = x.LastSeenAt.HasValue && now - x.LastSeenAt.Value <= OnlineThreshold
            })
            .ToList();
    }

    public async Task<CageDeviceResponse> UpdateAsync(Guid id, CageDeviceUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _deviceRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new ApiException("Cage device not found.", HttpStatusCode.NotFound);

        entity.DeviceName = request.DeviceName;
        entity.WifiStatus = request.WifiStatus;
        entity.IsOnline = request.IsOnline;
        entity.LastSeenAt = request.LastSeenAt;

        _deviceRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _systemEventLogService.LogAsync("DeviceUpdated", nameof(CageDeviceService), "Cage device has been updated.", new { entity.Id }, cancellationToken);
        return entity.ToResponse();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _deviceRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new ApiException("Cage device not found.", HttpStatusCode.NotFound);

        _deviceRepository.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _systemEventLogService.LogAsync("DeviceDeleted", nameof(CageDeviceService), "Cage device has been deleted.", new { id }, cancellationToken);
    }
}
