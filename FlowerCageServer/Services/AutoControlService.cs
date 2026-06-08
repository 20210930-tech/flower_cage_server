using System.Net;
using Microsoft.EntityFrameworkCore;
using FlowerCageServer.Common.Enums;
using FlowerCageServer.Common.Exceptions;
using FlowerCageServer.DTOs.AutoControl;
using FlowerCageServer.DTOs.Controls;
using FlowerCageServer.Entities;
using FlowerCageServer.Mapping;
using FlowerCageServer.Repositories;
using FlowerCageServer.Services.Fuzzy;

namespace FlowerCageServer.Services;

// 자동 모드의 환경 평가와 제어 명령 생성을 담당한다.
// 센서 한 건과 식물 환경설정을 받아 토양 수분과 조도와 온도와 습도를 차례로 살핀다.
// 토양 수분은 목표까지 일 초씩 급수하는 피드백 방식으로 다루고, 조도는 퍼지 추론으로 LED 밝기를 정한다.
// 온도와 습도는 제어 수단이 없어 범위를 벗어나면 경고만 만든다.
// 판단 과정을 사람이 읽을 수 있는 문장으로 모아 응답에 함께 돌려준다.
public class AutoControlService : IAutoControlService
{
    private readonly IRepository<PlantProfile>           _plantRepository;
    private readonly IRepository<PlantEnvironmentSetting> _settingRepository;
    private readonly IRepository<SensorReading>          _sensorRepository;
    private readonly IRepository<ControlCommand>         _commandRepository;
    private readonly IControlCommandService              _controlCommandService;
    private readonly IAlertService                       _alertService;
    private readonly ISystemEventLogService              _systemEventLogService;

    public AutoControlService(
        IRepository<PlantProfile> plantRepository,
        IRepository<PlantEnvironmentSetting> settingRepository,
        IRepository<SensorReading> sensorRepository,
        IRepository<ControlCommand> commandRepository,
        IControlCommandService controlCommandService,
        IAlertService alertService,
        ISystemEventLogService systemEventLogService)
    {
        _plantRepository       = plantRepository;
        _settingRepository     = settingRepository;
        _sensorRepository      = sensorRepository;
        _commandRepository     = commandRepository;
        _controlCommandService = controlCommandService;
        _alertService          = alertService;
        _systemEventLogService = systemEventLogService;
    }

    public async Task<AutoControlEvaluationResponse> EvaluateAsync(
        AutoControlEvaluationRequest request, CancellationToken cancellationToken = default)
    {
        var plant = await _plantRepository.GetByIdAsync(request.PlantProfileId, cancellationToken)
            ?? throw new ApiException("Plant profile not found.", HttpStatusCode.NotFound);

        var setting = await _settingRepository.FirstOrDefaultAsync(
            x => x.PlantProfileId == request.PlantProfileId, cancellationToken)
            ?? throw new ApiException("Plant environment setting not found.", HttpStatusCode.NotFound);

        var sensorReading = request.SensorReadingId.HasValue
            ? await _sensorRepository.GetByIdAsync(request.SensorReadingId.Value, cancellationToken)
            : await _sensorRepository.Query()
                .Where(x => x.PlantProfileId == request.PlantProfileId)
                .OrderByDescending(x => x.RecordedAt)
                .FirstOrDefaultAsync(cancellationToken);

        if (sensorReading is null)
            throw new ApiException("Sensor reading not found.", HttpStatusCode.NotFound);

        var decisions = new List<string>();
        var commands  = new List<ControlCommandResponse>();
        var alerts    = new List<DTOs.Alerts.AlertEventResponse>();

        if (!setting.AutoModeEnabled)
        {
            decisions.Add("Auto mode is disabled. No command generated.");
            return new AutoControlEvaluationResponse(
                plant.Id, sensorReading.Id, false, decisions, commands, alerts);
        }

        // 토양 수분 제어 부분이다. 목표 수분까지 한 번에 일 초씩만 급수하는 피드백 방식을 쓴다.
        // 펌프의 실제 유량을 몰라도 되는 폐루프 제어이다. 현재 수분이 목표보다 낮으면 일 초만 급수하고
        // 다음 센서 측정에서 다시 판단한다. 수분이 목표 중앙값에 닿으면 급수를 멈춘다.
        // 토양 센서가 피드백을 주므로 펌프가 빠르든 느리든 알아서 목표로 수렴한다.
        var soilTarget = (setting.SoilMoistureMin + setting.SoilMoistureMax) / 2m;
        if (sensorReading.SoilMoisture < soilTarget)
        {
            decisions.Add($"토양 수분 {sensorReading.SoilMoisture:F1} < 목표 {soilTarget:F1} " +
                          $"(범위 {setting.SoilMoistureMin:F1}~{setting.SoilMoistureMax:F1}). 1초 급수 펄스 생성.");
            commands.Add(await _controlCommandService.CreateAsync(new ControlCommandCreateRequest(
                plant.CageDeviceId, null, CommandType.Water,
                WaterPumpOn: true, WaterAmountMl: null,
                LedOn: null, LedBrightness: null, AutoModeOn: null, AlertEnabled: null,
                Reason: $"Auto: 토양 수분 {sensorReading.SoilMoisture:F1} → 목표 {soilTarget:F1}까지 1초 급수",
                SourceType: SourceType.Auto,
                WaterDurationSec: 1), cancellationToken));
        }
        else if (sensorReading.SoilMoisture > setting.SoilMoistureMax)
        {
            decisions.Add($"토양 수분 {sensorReading.SoilMoisture:F1} > 최댓값 {setting.SoilMoistureMax:F1}. 과습 경고.");
            alerts.Add((await _alertService.CreateAsync(AlertLevel.Warning,
                "과습 감지",
                $"토양 수분 {sensorReading.SoilMoisture:F1}이 최대 허용값 {setting.SoilMoistureMax:F1}을 초과합니다.",
                "AutoController", plant.CageDeviceId, plant.Id, cancellationToken)).ToResponse());
        }
        else
        {
            decisions.Add($"토양 수분 {sensorReading.SoilMoisture:F1}이 목표 {soilTarget:F1} 이상. 급수 불필요.");
        }

        // 조도 제어 부분이다. 퍼지 추론으로 조도에 맞는 LED 밝기를 정한다.
        // 수동 우선 정책을 둔다. 가장 최근의 LED 명령이 수동이면 자동 제어가 LED를 건드리지 않는다.
        // 그래서 사용자가 직접 켜거나 끈 LED는, 다음에 어떤 주체가 LED 명령을 다시 낼 때까지 유지된다.
        // 시간이 지났다고 자동이 슬그머니 덮어쓰지 않는다. 자동 LED를 다시 쓰려면 자동이나 인공지능이
        // LED 명령을 새로 내거나, 사용자가 LED 설정을 바꾸면 된다.
        var latestLed = await _commandRepository.Query()
            .Where(x => x.CageDeviceId == plant.CageDeviceId
                     && x.CommandType == CommandType.Led)
            .OrderByDescending(x => x.RequestedAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (latestLed is not null && latestLed.SourceType == SourceType.Manual)
        {
            decisions.Add("가장 최근 LED 명령이 수동이라 자동 LED 제어를 건너뜁니다. (수동 우선)");
        }
        else
        {
            var light = FuzzyControlEngine.EvaluateLighting(
                sensorReading.LightLux, setting.LightLuxMin, setting.LightLuxMax);
            decisions.AddRange(light.Trace);

            if (light.Act)
            {
                var brightness = (int)light.Value;
                // 밝기 퍼센트를 영부터 이백오십오까지의 값으로 바꿔 흰색 RGB로 보낸다.
                var ledVal = (int)Math.Round(brightness / 100.0 * 255);
                commands.Add(await _controlCommandService.CreateAsync(new ControlCommandCreateRequest(
                    plant.CageDeviceId, null, CommandType.Led,
                    WaterPumpOn: null, WaterAmountMl: null,
                    LedOn: brightness > 0, LedBrightness: brightness,
                    AutoModeOn: null, AlertEnabled: null,
                    Reason: $"Auto(퍼지): 조도 {sensorReading.LightLux:F0}lux (목표 {setting.LightLuxMin:F0}~{setting.LightLuxMax:F0}) → LED {brightness}%",
                    SourceType: SourceType.Auto,
                    LedR: ledVal, LedG: ledVal, LedB: ledVal), cancellationToken));
            }
        }

        // 온도 점검 부분이다. 온도는 직접 제어할 수단이 없으므로 범위를 벗어나면 경고만 남긴다.
        if (sensorReading.Temperature < setting.TemperatureMin)
        {
            decisions.Add($"온도 {sensorReading.Temperature:F1}°C < 최솟값 {setting.TemperatureMin:F1}°C. 저온 경고.");
            alerts.Add((await _alertService.CreateAsync(AlertLevel.Danger,
                "저온 감지",
                $"온도 {sensorReading.Temperature:F1}°C가 최저 허용값 {setting.TemperatureMin:F1}°C 미만입니다.",
                "AutoController", plant.CageDeviceId, plant.Id, cancellationToken)).ToResponse());
        }
        else if (sensorReading.Temperature > setting.TemperatureMax)
        {
            decisions.Add($"온도 {sensorReading.Temperature:F1}°C > 최댓값 {setting.TemperatureMax:F1}°C. 고온 경고.");
            alerts.Add((await _alertService.CreateAsync(AlertLevel.Danger,
                "고온 감지",
                $"온도 {sensorReading.Temperature:F1}°C가 최대 허용값 {setting.TemperatureMax:F1}°C를 초과합니다.",
                "AutoController", plant.CageDeviceId, plant.Id, cancellationToken)).ToResponse());
        }

        // 습도 점검 부분이다. 온도와 마찬가지로 제어 수단이 없어 범위를 벗어나면 경고만 남긴다.
        if (sensorReading.Humidity < setting.HumidityMin)
        {
            decisions.Add($"습도 {sensorReading.Humidity:F1}% < 최솟값 {setting.HumidityMin:F1}%. 저습 경고.");
            alerts.Add((await _alertService.CreateAsync(AlertLevel.Warning,
                "저습 감지",
                $"습도 {sensorReading.Humidity:F1}%가 최저 허용값 {setting.HumidityMin:F1}% 미만입니다.",
                "AutoController", plant.CageDeviceId, plant.Id, cancellationToken)).ToResponse());
        }
        else if (sensorReading.Humidity > setting.HumidityMax)
        {
            decisions.Add($"습도 {sensorReading.Humidity:F1}% > 최댓값 {setting.HumidityMax:F1}%. 고습 경고.");
            alerts.Add((await _alertService.CreateAsync(AlertLevel.Warning,
                "고습 감지",
                $"습도 {sensorReading.Humidity:F1}%가 최대 허용값 {setting.HumidityMax:F1}%를 초과합니다.",
                "AutoController", plant.CageDeviceId, plant.Id, cancellationToken)).ToResponse());
        }

        // 스펙트럼 광질 분석 부분이다. 적색과 녹색과 청색 비율을 계산해 참고용으로 기록만 한다.
        // 제어에는 쓰지 않고, 어떤 파장이 우세한지 사람이 확인하는 용도이다.
        var specTotal = sensorReading.Nm415 + sensorReading.Nm445 + sensorReading.Nm480
                      + sensorReading.Nm515 + sensorReading.Nm555 + sensorReading.Nm590
                      + sensorReading.Nm630 + sensorReading.Nm680;
        if (specTotal > 0)
        {
            var red   = sensorReading.Nm630 + sensorReading.Nm680;
            var blue  = sensorReading.Nm415 + sensorReading.Nm445 + sensorReading.Nm480;
            var green = sensorReading.Nm515 + sensorReading.Nm555 + sensorReading.Nm590;
            var rPct  = red   * 100.0 / specTotal;
            var bPct  = blue  * 100.0 / specTotal;
            var gPct  = green * 100.0 / specTotal;
            decisions.Add($"광질 분석 — Red {rPct:F1}% / Green {gPct:F1}% / Blue {bPct:F1}% / NIR {sensorReading.Nir}");
        }

        if (decisions.Count == 0 || decisions.All(d => d.Contains("광질 분석")))
            decisions.Add("모든 환경값이 목표 범위 내에 있습니다. 자동 제어 불필요.");

        await _systemEventLogService.LogAsync(
            "AutoControlEvaluated", nameof(AutoControlService),
            "Auto control evaluation completed.",
            new { PlantProfileId = plant.Id, SensorReadingId = sensorReading.Id, DecisionCount = decisions.Count },
            cancellationToken);

        return new AutoControlEvaluationResponse(
            plant.Id, sensorReading.Id, true, decisions, commands, alerts);
    }
}
