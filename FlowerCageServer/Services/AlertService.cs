using Microsoft.EntityFrameworkCore;
using FlowerCageServer.Common.Enums;
using FlowerCageServer.DTOs.Alerts;
using FlowerCageServer.Entities;
using FlowerCageServer.Mapping;
using FlowerCageServer.Repositories;

namespace FlowerCageServer.Services;

public class AlertService : IAlertService
{
    private readonly IRepository<AlertEvent> _alertRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISystemEventLogService _systemEventLogService;

    public AlertService(IRepository<AlertEvent> alertRepository, IUnitOfWork unitOfWork, ISystemEventLogService systemEventLogService)
    {
        _alertRepository = alertRepository;
        _unitOfWork = unitOfWork;
        _systemEventLogService = systemEventLogService;
    }

    public async Task<AlertEvent> CreateAsync(AlertLevel level, string title, string message, string source, Guid? cageDeviceId, Guid? plantProfileId, CancellationToken cancellationToken = default)
    {
        var entity = new AlertEvent
        {
            Level = level,
            Title = title,
            Message = message,
            Source = source,
            CageDeviceId = cageDeviceId,
            PlantProfileId = plantProfileId,
            OccurredAt = DateTime.UtcNow
        };

        await _alertRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _systemEventLogService.LogAsync("AlertCreated", nameof(AlertService), title, new { entity.Id, entity.Level }, cancellationToken);
        return entity;
    }

    public async Task<IReadOnlyCollection<AlertEventResponse>> GetAsync(Guid? cageDeviceId, Guid? plantProfileId, CancellationToken cancellationToken = default)
    {
        var query = _alertRepository.Query();
        if (cageDeviceId.HasValue)
        {
            query = query.Where(x => x.CageDeviceId == cageDeviceId.Value);
        }

        if (plantProfileId.HasValue)
        {
            query = query.Where(x => x.PlantProfileId == plantProfileId.Value);
        }

        var alerts = await query.OrderByDescending(x => x.OccurredAt).ToListAsync(cancellationToken);
        return alerts.Select(x => x.ToResponse()).ToList();
    }
}
