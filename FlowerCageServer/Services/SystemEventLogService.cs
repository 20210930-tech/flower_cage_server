using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using FlowerCageServer.DTOs.SystemEvents;
using FlowerCageServer.Entities;
using FlowerCageServer.Mapping;
using FlowerCageServer.Repositories;

namespace FlowerCageServer.Services;

public class SystemEventLogService : ISystemEventLogService
{
    private readonly IRepository<SystemEventLog> _eventRepository;
    private readonly IUnitOfWork _unitOfWork;

    public SystemEventLogService(IRepository<SystemEventLog> eventRepository, IUnitOfWork unitOfWork)
    {
        _eventRepository = eventRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task LogAsync(string eventType, string eventSource, string message, object? payload = null, CancellationToken cancellationToken = default)
    {
        var entity = new SystemEventLog
        {
            EventType = eventType,
            EventSource = eventSource,
            Message = message,
            PayloadJson = payload is null ? null : JsonSerializer.Serialize(payload),
            OccurredAt = DateTime.UtcNow
        };

        await _eventRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<SystemEventLogResponse>> GetLogsAsync(CancellationToken cancellationToken = default)
    {
        var logs = await _eventRepository.Query()
            .OrderByDescending(x => x.OccurredAt)
            .ToListAsync(cancellationToken);

        return logs.Select(x => x.ToResponse()).ToList();
    }
}
