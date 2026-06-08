using System.Net;
using Microsoft.EntityFrameworkCore;
using FlowerCageServer.Common.Enums;
using FlowerCageServer.Common.Exceptions;
using FlowerCageServer.DTOs.Controls;
using FlowerCageServer.Entities;
using FlowerCageServer.Mapping;
using FlowerCageServer.Repositories;

namespace FlowerCageServer.Services;

public class ControlCommandService : IControlCommandService
{
    private readonly IRepository<ControlCommand> _commandRepository;
    private readonly IRepository<CageDevice> _deviceRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISystemEventLogService _systemEventLogService;

    public ControlCommandService(IRepository<ControlCommand> commandRepository, IRepository<CageDevice> deviceRepository, IUnitOfWork unitOfWork, ISystemEventLogService systemEventLogService)
    {
        _commandRepository = commandRepository;
        _deviceRepository = deviceRepository;
        _unitOfWork = unitOfWork;
        _systemEventLogService = systemEventLogService;
    }

    public async Task<ControlCommandResponse> CreateAsync(ControlCommandCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (await _deviceRepository.GetByIdAsync(request.CageDeviceId, cancellationToken) is null)
        {
            throw new ApiException("Cage device not found.", HttpStatusCode.NotFound);
        }

        ValidateByType(request);

        var entity = new ControlCommand
        {
            CageDeviceId = request.CageDeviceId,
            RequestedByUserId = request.RequestedByUserId,
            CommandType = request.CommandType,
            WaterPumpOn = request.WaterPumpOn,
            WaterAmountMl = request.WaterAmountMl,
            WaterDurationSec = request.WaterDurationSec,
            LedOn = request.LedOn,
            LedBrightness = request.LedBrightness,
            LedR = request.LedR,
            LedG = request.LedG,
            LedB = request.LedB,
            AutoModeOn = request.AutoModeOn,
            AlertEnabled = request.AlertEnabled,
            Reason = request.Reason,
            SourceType = request.SourceType,
            Status = CommandStatus.Pending,
            RequestedAt = DateTime.UtcNow
        };

        await _commandRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _systemEventLogService.LogAsync("ControlCommandCreated", nameof(ControlCommandService), "Control command has been queued.", new { entity.Id, entity.CommandType, entity.SourceType }, cancellationToken);
        return entity.ToResponse();
    }

    public async Task<ControlCommandResponse> UpdateStatusAsync(Guid id, ControlCommandStatusUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _commandRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new ApiException("Control command not found.", HttpStatusCode.NotFound);

        entity.Status = request.Status;
        entity.AppliedAt = request.AppliedAt;

        _commandRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _systemEventLogService.LogAsync("ControlCommandUpdated", nameof(ControlCommandService), "Control command status updated.", new { entity.Id, entity.Status }, cancellationToken);
        return entity.ToResponse();
    }

    public async Task<IReadOnlyCollection<ControlCommandResponse>> GetAsync(Guid cageDeviceId, CommandStatus? status = null, int take = 50, CancellationToken cancellationToken = default)
    {
        if (take <= 0) take = 50;
        if (take > 200) take = 200;

        var query = _commandRepository.Query()
            .Where(x => x.CageDeviceId == cageDeviceId);

        if (status is not null)
        {
            query = query.Where(x => x.Status == status);
        }

        // status 필터가 있으면(펌웨어 폴링) 오래된 명령부터, 없으면(앱 이력) 최신순.
        query = status is not null
            ? query.OrderBy(x => x.RequestedAt)
            : query.OrderByDescending(x => x.RequestedAt);

        var commands = await query.Take(take).ToListAsync(cancellationToken);

        return commands.Select(x => x.ToResponse()).ToList();
    }

    private static void ValidateByType(ControlCommandCreateRequest request)
    {
        var invalid = request.CommandType switch
        {
            CommandType.Water => request.WaterDurationSec is null && request.WaterAmountMl is null && request.WaterPumpOn is null,
            CommandType.Led => request.LedR is null && request.LedG is null && request.LedB is null && request.LedBrightness is null && request.LedOn is null,
            CommandType.AutoMode => request.AutoModeOn is null,
            CommandType.Alert => request.AlertEnabled is null,
            CommandType.ResetConfig => false, // 추가 파라미터 불필요 (항상 유효)
            _ => true
        };

        if (invalid)
        {
            throw new ApiException("Control command payload does not match the requested command type.");
        }
    }
}
