using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FlowerCageServer.Common.Enums;
using FlowerCageServer.Common.Options;
using FlowerCageServer.DTOs.Gpt;
using FlowerCageServer.Entities;
using FlowerCageServer.Repositories;

namespace FlowerCageServer.Services;

// AI 모드: Mode == Ai 인 식물을, 일정 주기마다 최신 센서/목표값으로 프롬프트를 만들어
// 기존 GptAnalysisService("AutoControl")로 보낸다. 응답은 GptAnalysisService 내부에서
// ControlCommand(Water/LED, SourceType.Gpt)로 자동 변환된다.
public class AiControlBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly GptOptions _options;
    private readonly ILogger<AiControlBackgroundService> _logger;

    public AiControlBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<GptOptions> options,
        ILogger<AiControlBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var minutes = _options.AiPollIntervalMinutes <= 0 ? 30 : _options.AiPollIntervalMinutes;
        var interval = TimeSpan.FromMinutes(minutes);
        _logger.LogInformation(
            "AI 제어 백그라운드 서비스 시작 — 주기 {Minutes}분, GPT Enabled={Enabled}",
            minutes, _options.Enabled);

        using var timer = new PeriodicTimer(interval);
        // 기동 직후 1회 실행 후 주기 반복
        do
        {
            try
            {
                await ProcessAiPlantsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI 제어 주기 처리 중 오류");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task ProcessAiPlantsAsync(CancellationToken ct)
    {
        if (!_options.Enabled)
        {
            _logger.LogDebug("GPT 비활성화 상태 — AI 모드 호출 스킵");
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var sp = scope.ServiceProvider;
        var settingRepo = sp.GetRequiredService<IRepository<PlantEnvironmentSetting>>();
        var sensorRepo = sp.GetRequiredService<IRepository<SensorReading>>();
        var plantRepo = sp.GetRequiredService<IRepository<PlantProfile>>();
        var gpt = sp.GetRequiredService<IGptAnalysisService>();
        var eventLog = sp.GetRequiredService<ISystemEventLogService>();

        var aiSettings = await settingRepo.Query()
            .Where(s => s.Mode == ControlMode.Ai)
            .ToListAsync(ct);

        if (aiSettings.Count == 0) return;

        _logger.LogInformation("AI 모드 식물 {Count}개 처리 시작", aiSettings.Count);

        foreach (var setting in aiSettings)
        {
            try
            {
                var plant = await plantRepo.GetByIdAsync(setting.PlantProfileId, ct);
                if (plant is null) continue;

                var latest = await sensorRepo.Query()
                    .Where(r => r.PlantProfileId == setting.PlantProfileId)
                    .OrderByDescending(r => r.RecordedAt)
                    .FirstOrDefaultAsync(ct);
                if (latest is null) continue;

                var prompt = BuildPrompt(plant, setting, latest);
                await gpt.AnalyzeAsync(
                    new GptAnalysisRequest(setting.PlantProfileId, latest.Id, "AutoControl", prompt), ct);
            }
            catch (Exception ex)
            {
                await eventLog.LogAsync(
                    "AiControlError", nameof(AiControlBackgroundService),
                    "AI 모드 주기 제어 실패.",
                    new { setting.PlantProfileId, Error = ex.Message }, ct);
            }
        }
    }

    private static string BuildPrompt(PlantProfile plant, PlantEnvironmentSetting s, SensorReading r) =>
        $"""
        [현재 상태 요약]
        식물: {plant.Species} ({plant.PlantName})
        토양수분(raw 0~1023): {r.SoilMoisture:F0} (목표 {s.SoilMoistureMin:F0}~{s.SoilMoistureMax:F0})
        온도: {r.Temperature:F1}°C (목표 {s.TemperatureMin:F1}~{s.TemperatureMax:F1})
        습도: {r.Humidity:F0}% (목표 {s.HumidityMin:F0}~{s.HumidityMax:F0})
        광량: {r.LightLux:F0} lux (목표 {s.LightLuxMin:F0}~{s.LightLuxMax:F0})
        위 상태를 분석해 즉시 적용할 제어값(급수/LED)을 계산하라.
        """;
}
