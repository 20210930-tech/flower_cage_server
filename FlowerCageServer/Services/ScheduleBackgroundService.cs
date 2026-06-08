using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FlowerCageServer.Common.Enums;
using FlowerCageServer.DTOs.Controls;
using FlowerCageServer.Entities;
using FlowerCageServer.Repositories;

namespace FlowerCageServer.Services;

// 시간 예약 실행기: 1분마다 예약을 확인해 발화 시각에 ControlCommand를 생성한다.
// 수동(Manual) 모드인 식물에서만 발화한다. (시간대: KST = UTC+9, DST 없음)
public class ScheduleBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScheduleBackgroundService> _logger;

    public ScheduleBackgroundService(
        IServiceScopeFactory scopeFactory,
        ILogger<ScheduleBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("예약 실행 백그라운드 서비스 시작 — 1분 주기 (KST 기준)");
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        do
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "예약 처리 중 오류");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task TickAsync(CancellationToken ct)
    {
        var nowKst = DateTime.UtcNow.AddHours(9);

        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        var scheduleRepo = sp.GetRequiredService<IRepository<ControlSchedule>>();
        var settingRepo = sp.GetRequiredService<IRepository<PlantEnvironmentSetting>>();
        var commandService = sp.GetRequiredService<IControlCommandService>();
        var unitOfWork = sp.GetRequiredService<IUnitOfWork>();
        var eventLog = sp.GetRequiredService<ISystemEventLogService>();

        var schedules = await scheduleRepo.Query()
            .Where(s => s.Enabled)
            .ToListAsync(ct);

        foreach (var sch in schedules)
        {
            if (sch.Hour != nowKst.Hour || sch.Minute != nowKst.Minute) continue;
            if (!RepeatMatches(sch, nowKst)) continue;
            if (AlreadyFiredThisMinute(sch, nowKst)) continue;

            // 수동 모드 가드: 설정이 있고 Manual이 아니면 발화 안 함
            if (sch.PlantProfileId.HasValue)
            {
                var setting = await settingRepo.FirstOrDefaultAsync(
                    x => x.PlantProfileId == sch.PlantProfileId.Value, ct);
                if (setting is not null && setting.Mode != ControlMode.Manual)
                {
                    _logger.LogInformation(
                        "예약 {Id} 시각 일치하나 모드가 {Mode}라 스킵 (예약은 수동 모드에서만 발화)",
                        sch.Id, setting.Mode);
                    continue;
                }
            }

            try
            {
                await FireAsync(commandService, sch, ct);
                _logger.LogInformation("예약 발화: {Type} {Hour}:{Minute} (예약 {Id})",
                    sch.Type, sch.Hour, sch.Minute, sch.Id);
                sch.LastFiredAt = DateTime.UtcNow;
                if (sch.Repeat == ScheduleRepeat.Once) sch.Enabled = false;
                scheduleRepo.Update(sch);
                await unitOfWork.SaveChangesAsync(ct);
            }
            catch (Exception ex)
            {
                await eventLog.LogAsync("ScheduleFireError", nameof(ScheduleBackgroundService),
                    "예약 실행 실패.", new { sch.Id, sch.Type, Error = ex.Message }, ct);
            }
        }
    }

    private static bool RepeatMatches(ControlSchedule s, DateTime nowKst) => s.Repeat switch
    {
        ScheduleRepeat.Once => s.Date.HasValue && s.Date.Value.Date == nowKst.Date,
        ScheduleRepeat.Daily => true,
        ScheduleRepeat.Weekly => s.DayOfWeek == (int)nowKst.DayOfWeek,
        ScheduleRepeat.Monthly => s.DayOfMonth == nowKst.Day,
        ScheduleRepeat.Yearly => s.Month == nowKst.Month && s.DayOfMonth == nowKst.Day,
        _ => false
    };

    private static bool AlreadyFiredThisMinute(ControlSchedule s, DateTime nowKst)
    {
        if (!s.LastFiredAt.HasValue) return false;
        var lastKst = s.LastFiredAt.Value.AddHours(9);
        return lastKst.Date == nowKst.Date && lastKst.Hour == nowKst.Hour && lastKst.Minute == nowKst.Minute;
    }

    private static async Task FireAsync(IControlCommandService commandService, ControlSchedule s, CancellationToken ct)
    {
        if (s.Type == ScheduleType.Water)
        {
            await commandService.CreateAsync(new ControlCommandCreateRequest(
                s.CageDeviceId, null, CommandType.Water,
                WaterPumpOn: true, WaterAmountMl: null,
                LedOn: null, LedBrightness: null, AutoModeOn: null, AlertEnabled: null,
                Reason: $"예약 급수{(s.Label is null ? "" : $" ({s.Label})")}",
                SourceType: SourceType.Manual,
                WaterDurationSec: s.WaterDurationSec ?? 3), ct);
        }
        else // Led
        {
            var r = s.LedR ?? 0;
            var g = s.LedG ?? 0;
            var b = s.LedB ?? 0;
            await commandService.CreateAsync(new ControlCommandCreateRequest(
                s.CageDeviceId, null, CommandType.Led,
                WaterPumpOn: null, WaterAmountMl: null,
                LedOn: (r + g + b) > 0, LedBrightness: null, AutoModeOn: null, AlertEnabled: null,
                Reason: $"예약 LED{(s.Label is null ? "" : $" ({s.Label})")}",
                SourceType: SourceType.Manual,
                LedR: r, LedG: g, LedB: b), ct);
        }
    }
}
