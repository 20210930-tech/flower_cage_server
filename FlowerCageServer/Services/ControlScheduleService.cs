using System.Net;
using Microsoft.EntityFrameworkCore;
using FlowerCageServer.Common.Exceptions;
using FlowerCageServer.DTOs.Controls;
using FlowerCageServer.Entities;
using FlowerCageServer.Mapping;
using FlowerCageServer.Repositories;

namespace FlowerCageServer.Services;

public class ControlScheduleService : IControlScheduleService
{
    private readonly IRepository<ControlSchedule> _scheduleRepository;
    private readonly IRepository<CageDevice> _deviceRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISystemEventLogService _systemEventLogService;

    public ControlScheduleService(
        IRepository<ControlSchedule> scheduleRepository,
        IRepository<CageDevice> deviceRepository,
        IUnitOfWork unitOfWork,
        ISystemEventLogService systemEventLogService)
    {
        _scheduleRepository = scheduleRepository;
        _deviceRepository = deviceRepository;
        _unitOfWork = unitOfWork;
        _systemEventLogService = systemEventLogService;
    }

    public async Task<IReadOnlyCollection<ControlScheduleResponse>> GetAsync(
        Guid cageDeviceId, CancellationToken cancellationToken = default)
    {
        var schedules = await _scheduleRepository.Query()
            .Where(x => x.CageDeviceId == cageDeviceId)
            .OrderBy(x => x.Hour).ThenBy(x => x.Minute)
            .ToListAsync(cancellationToken);

        return schedules.Select(x => x.ToResponse()).ToList();
    }

    public async Task<ControlScheduleResponse> CreateAsync(
        ControlScheduleCreateRequest request, CancellationToken cancellationToken = default)
    {
        if (await _deviceRepository.GetByIdAsync(request.CageDeviceId, cancellationToken) is null)
            throw new ApiException("Cage device not found.", HttpStatusCode.NotFound);

        Validate(request);

        var entity = new ControlSchedule
        {
            CageDeviceId = request.CageDeviceId,
            PlantProfileId = request.PlantProfileId,
            Type = request.Type,
            Repeat = request.Repeat,
            Enabled = request.Enabled,
            Label = request.Label,
            Hour = request.Hour,
            Minute = request.Minute,
            DayOfWeek = request.DayOfWeek,
            DayOfMonth = request.DayOfMonth,
            Month = request.Month,
            Date = request.Date,
            WaterDurationSec = request.WaterDurationSec,
            LedR = request.LedR,
            LedG = request.LedG,
            LedB = request.LedB,
        };

        await _scheduleRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _systemEventLogService.LogAsync("ControlScheduleCreated", nameof(ControlScheduleService),
            "Control schedule created.", new { entity.Id, entity.Type, entity.Repeat }, cancellationToken);
        return entity.ToResponse();
    }

    public async Task<ControlScheduleResponse> SetEnabledAsync(
        Guid id, bool enabled, CancellationToken cancellationToken = default)
    {
        var entity = await _scheduleRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new ApiException("Control schedule not found.", HttpStatusCode.NotFound);

        entity.Enabled = enabled;
        _scheduleRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return entity.ToResponse();
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _scheduleRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new ApiException("Control schedule not found.", HttpStatusCode.NotFound);

        _scheduleRepository.Remove(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static void Validate(ControlScheduleCreateRequest r)
    {
        if (r.Hour is < 0 or > 23 || r.Minute is < 0 or > 59)
            throw new ApiException("Hour/Minute가 유효하지 않습니다.");

        var invalid = r.Repeat switch
        {
            Common.Enums.ScheduleRepeat.Once => r.Date is null,
            Common.Enums.ScheduleRepeat.Weekly => r.DayOfWeek is null or < 0 or > 6,
            Common.Enums.ScheduleRepeat.Monthly => r.DayOfMonth is null or < 1 or > 31,
            Common.Enums.ScheduleRepeat.Yearly => r.Month is null or < 1 or > 12 || r.DayOfMonth is null or < 1 or > 31,
            _ => false
        };
        if (invalid)
            throw new ApiException("반복 주기에 필요한 정보가 누락되었습니다.");

        var valueMissing = r.Type switch
        {
            Common.Enums.ScheduleType.Water => r.WaterDurationSec is null or <= 0,
            Common.Enums.ScheduleType.Led => r.LedR is null || r.LedG is null || r.LedB is null,
            _ => true
        };
        if (valueMissing)
            throw new ApiException("예약 동작 값(급수 시간 또는 LED RGB)이 누락되었습니다.");
    }
}
