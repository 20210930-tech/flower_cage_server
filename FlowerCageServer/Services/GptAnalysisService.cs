using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using FlowerCageServer.Common.Enums;
using FlowerCageServer.Common.Options;
using FlowerCageServer.DTOs.Controls;
using FlowerCageServer.DTOs.Gpt;
using FlowerCageServer.Entities;
using FlowerCageServer.Mapping;
using FlowerCageServer.Repositories;

namespace FlowerCageServer.Services;

public class GptAnalysisService : IGptAnalysisService
{
    private readonly IRepository<GptAnalysisLog> _analysisRepository;
    private readonly IRepository<SensorReading>  _sensorRepository;
    private readonly IRepository<PlantProfile>   _plantRepository;
    private readonly IControlCommandService      _controlCommandService;
    private readonly IUnitOfWork                 _unitOfWork;
    private readonly ISystemEventLogService      _systemEventLogService;
    private readonly HttpClient                  _httpClient;
    private readonly GptOptions                  _options;

    public GptAnalysisService(
        IRepository<GptAnalysisLog> analysisRepository,
        IRepository<SensorReading>  sensorRepository,
        IRepository<PlantProfile>   plantRepository,
        IControlCommandService      controlCommandService,
        IUnitOfWork                 unitOfWork,
        ISystemEventLogService      systemEventLogService,
        HttpClient                  httpClient,
        IOptions<GptOptions>        options)
    {
        _analysisRepository    = analysisRepository;
        _sensorRepository      = sensorRepository;
        _plantRepository       = plantRepository;
        _controlCommandService = controlCommandService;
        _unitOfWork            = unitOfWork;
        _systemEventLogService = systemEventLogService;
        _httpClient            = httpClient;
        _options               = options.Value;
    }

    public async Task<GptAnalysisResponse> AnalyzeAsync(
        GptAnalysisRequest request, CancellationToken cancellationToken = default)
    {
        // SensorReading이 있으면 스펙트럼 데이터를 프롬프트에 추가
        var enrichedPrompt = await EnrichPromptWithSensorDataAsync(
            request.PromptSummary, request.SensorReadingId, cancellationToken);

        var entity = new GptAnalysisLog
        {
            PlantProfileId  = request.PlantProfileId,
            SensorReadingId = request.SensorReadingId,
            AnalysisType    = request.AnalysisType,
            PromptSummary   = enrichedPrompt,
            RequestedAt     = DateTime.UtcNow,
            Status          = GptAnalysisStatus.Requested,
        };

        if (!_options.Enabled)
        {
            entity.ResponseContent = BuildMockAnalysis(request.AnalysisType, enrichedPrompt);
            entity.Recommendation  = "GPT 비활성화 상태입니다. 플레이스홀더 분석 결과를 반환합니다.";
            entity.Status          = GptAnalysisStatus.Completed;
            entity.CompletedAt     = DateTime.UtcNow;
        }
        else
        {
            try
            {
                var systemPrompt = GetSystemPrompt(request.AnalysisType);
                var aiResponse   = _options.Model.Contains("gemini", StringComparison.OrdinalIgnoreCase)
                    ? await RequestGeminiAsync(systemPrompt, enrichedPrompt, cancellationToken)
                    : await RequestOpenAiAsync(systemPrompt, enrichedPrompt, cancellationToken);

                entity.ResponseContent = aiResponse;
                entity.Recommendation  = "AI 분석이 완료되었습니다.";
                entity.Status          = GptAnalysisStatus.Completed;
                entity.CompletedAt     = DateTime.UtcNow;
            }
            catch (Exception ex)
            {
                entity.ResponseContent = "AI 호출 중 오류가 발생했습니다.";
                entity.Recommendation  = ex.Message;
                entity.Status          = GptAnalysisStatus.Failed;
                entity.CompletedAt     = DateTime.UtcNow;
            }
        }

        await _analysisRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // AutoControl 분석이 성공한 경우 결과를 파싱해 ControlCommand 자동 생성
        if (entity.Status == GptAnalysisStatus.Completed &&
            request.AnalysisType == "AutoControl" &&
            request.PlantProfileId.HasValue)
        {
            await TryApplyAutoControlCommandsAsync(entity, request.PlantProfileId.Value, cancellationToken);
        }

        await _systemEventLogService.LogAsync(
            "GptAnalysisRequested", nameof(GptAnalysisService),
            "GPT analysis processed.",
            new { entity.Id, entity.AnalysisType, GptEnabled = _options.Enabled }, cancellationToken);

        return entity.ToResponse();
    }

    public async Task<IReadOnlyCollection<GptAnalysisResponse>> GetLogsAsync(
        Guid? plantProfileId, CancellationToken cancellationToken = default)
    {
        var query = _analysisRepository.Query();
        if (plantProfileId.HasValue)
            query = query.Where(x => x.PlantProfileId == plantProfileId.Value);

        var logs = await query.OrderByDescending(x => x.RequestedAt).ToListAsync(cancellationToken);
        return logs.Select(x => x.ToResponse()).ToList();
    }

    // ── 스펙트럼 데이터를 프롬프트에 추가 ────────────────────────────────
    private async Task<string> EnrichPromptWithSensorDataAsync(
        string basePrompt, Guid? sensorReadingId, CancellationToken cancellationToken)
    {
        if (!sensorReadingId.HasValue) return basePrompt;

        var sr = await _sensorRepository.GetByIdAsync(sensorReadingId.Value, cancellationToken);
        if (sr is null) return basePrompt;

        var specTotal = sr.Nm415 + sr.Nm445 + sr.Nm480 + sr.Nm515
                      + sr.Nm555 + sr.Nm590 + sr.Nm630 + sr.Nm680;
        var specSection = specTotal > 0
            ? $"""

              [AS7341 스펙트럼 센서 데이터]
              415nm Violet : {sr.Nm415}
              445nm Indigo : {sr.Nm445}
              480nm Blue   : {sr.Nm480}
              515nm Cyan   : {sr.Nm515}
              555nm Green  : {sr.Nm555}
              590nm Yellow : {sr.Nm590}
              630nm Orange : {sr.Nm630}
              680nm Red    : {sr.Nm680}
              Clear(광대역) : {sr.Clear}
              NIR(근적외선) : {sr.Nir}
              R:B 비율     : {(sr.Nm630 + sr.Nm680) * 100.0 / specTotal:F1}% : {(sr.Nm415 + sr.Nm445 + sr.Nm480) * 100.0 / specTotal:F1}%
              """
            : "";

        return basePrompt + specSection;
    }

    // ── AI AutoControl 응답 파싱 → ControlCommand 생성 ───────────────────
    private async Task TryApplyAutoControlCommandsAsync(
        GptAnalysisLog entity, Guid plantProfileId, CancellationToken cancellationToken)
    {
        var plant = await _plantRepository.GetByIdAsync(plantProfileId, cancellationToken);
        if (plant is null || string.IsNullOrWhiteSpace(entity.ResponseContent)) return;

        try
        {
            using var doc  = JsonDocument.Parse(entity.ResponseContent);
            var       root = doc.RootElement;

            // 급수
            if (root.TryGetProperty("water_needed", out var wn) && wn.GetBoolean())
            {
                var ml = root.TryGetProperty("water_amount_ml", out var mlProp)
                    ? (decimal)mlProp.GetDouble() : 150m;
                ml = Math.Max(10, Math.Min(300, ml));

                await _controlCommandService.CreateAsync(new ControlCommandCreateRequest(
                    plant.CageDeviceId, null, CommandType.Water,
                    WaterPumpOn: true, WaterAmountMl: ml,
                    LedOn: null, LedBrightness: null, AutoModeOn: null, AlertEnabled: null,
                    Reason: $"GPT AutoControl 분석 결과 급수 {ml}ml 권장",
                    SourceType: SourceType.Gpt), cancellationToken);
            }

            // LED
            if (root.TryGetProperty("led_adjustment", out var ledAdj))
            {
                var dir = ledAdj.GetString() ?? "";
                if (dir != "유지")
                {
                    int brightness;
                    if (root.TryGetProperty("led_target_rgb", out var rgb))
                    {
                        var r = rgb.TryGetProperty("red",   out var rv) ? rv.GetInt32() : 50;
                        var g = rgb.TryGetProperty("green", out var gv) ? gv.GetInt32() : 15;
                        var b = rgb.TryGetProperty("blue",  out var bv) ? bv.GetInt32() : 35;
                        brightness = Math.Max(r, Math.Max(g, b)); // 가장 높은 채널 기준
                    }
                    else
                    {
                        brightness = dir == "증가" ? 80 : 20;
                    }

                    await _controlCommandService.CreateAsync(new ControlCommandCreateRequest(
                        plant.CageDeviceId, null, CommandType.Led,
                        WaterPumpOn: null, WaterAmountMl: null,
                        LedOn: true, LedBrightness: Math.Clamp(brightness, 0, 100),
                        AutoModeOn: null, AlertEnabled: null,
                        Reason: $"GPT AutoControl 분석 결과 LED {dir} ({brightness}%)",
                        SourceType: SourceType.Gpt), cancellationToken);
                }
            }
        }
        catch
        {
            // JSON 파싱 실패 시 무시 (분석 결과 저장은 이미 완료)
        }
    }

    // ── 시스템 프롬프트 ───────────────────────────────────────────────────
    private static string GetSystemPrompt(string analysisType) => analysisType switch
    {
        "AlertPrediction" => """
            너는 식물 생육 환경을 분석하는 전문가이다.
            센서 데이터를 기반으로 현재 상태를 판단하고, 향후 환경 변화를 예측하라.
            스펙트럼 데이터(AS7341)가 있으면 광질(R:B 비율)도 분석하라.

            [출력 형식(JSON)]
            {
              "status": "정상/주의/위험",
              "alert_needed": true,
              "predicted_dry_time_hours": 0,
              "recommended_watering_time": "",
              "reason": {
                "soil_moisture": "",
                "temperature": "",
                "humidity": "",
                "light": "",
                "spectrum": "",
                "prediction": "",
                "summary": ""
              }
            }
            """,

        "AutoControl" => """
            너는 식물 생육 환경을 최적화하는 전문가이다.
            실시간 센서 데이터(스펙트럼 포함)를 분석하여 즉시 적용 가능한 제어 값을 계산하라.

            [제어 기준]
            - 권장 범위 중앙값에 가깝게 단계적으로 보정
            - 급수량은 최대 300ml 초과 금지
            - 스펙트럼 R:B 비율을 기반으로 LED RGB 방향 결정 (성장기: R:B ≈ 1:1, 개화기: R:B ≈ 2:1)

            [출력 형식(JSON)]
            {
              "status": "정상/주의/위험",
              "water_needed": true,
              "water_amount_ml": 0,
              "led_adjustment": "증가/감소/유지",
              "led_target_lux": 0,
              "led_rgb_adjustment": { "red": "증가/감소/유지", "green": "증가/감소/유지", "blue": "증가/감소/유지" },
              "led_target_rgb": { "red": 0, "green": 0, "blue": 0 },
              "temperature_adjustment": "증가/감소/유지",
              "temperature_target": 0,
              "humidity_adjustment": "증가/감소/유지",
              "humidity_target": 0,
              "reason": {
                "soil_moisture": "",
                "temperature": "",
                "humidity": "",
                "light": "",
                "spectrum": "",
                "summary": ""
              }
            }
            """,

        "InitialSettings" => """
            너는 식물 생육 환경을 설정하는 전문가이다.
            식물 정보를 기반으로 최적 생육 범위와 광환경(조도+RGB 비율)을 포함한 초기 환경 기준을 제안하라.

            [출력 형식(JSON)]
            {
              "plant_type": "",
              "growth_stage": "",
              "recommended_range": {
                "soil_moisture_min": 0, "soil_moisture_max": 0,
                "temperature_min": 0,   "temperature_max": 0,
                "humidity_min": 0,      "humidity_max": 0,
                "light": {
                  "lux_min": 0, "lux_max": 0,
                  "rgb_ratio": { "red_min": 0, "red_max": 0, "green_min": 0, "green_max": 0, "blue_min": 0, "blue_max": 0 }
                }
              },
              "control_policy": {
                "default_mode": "자동",
                "max_watering_ml": 0,
                "recommended_led_brightness_percent": 0,
                "recommended_led_rgb": { "red": 0, "green": 0, "blue": 0 }
              },
              "reason": { "plant_info": "", "growth_stage": "", "light": "", "summary": "" }
            }
            """,

        "RoutineRecommendation" => """
            너는 식물 생육 루틴을 설계하는 전문가이다.
            최근 센서 데이터(스펙트럼 포함)와 생육 기록을 기반으로 일일/주간 자동 제어 루틴을 추천하라.

            [출력 형식(JSON)]
            {
              "routine_summary": "",
              "daily_schedule": [
                { "time": "06:00", "action": "LED 켜기", "value": "", "reason": "" }
              ],
              "weekly_tasks": [
                { "day": "월요일", "action": "급수", "value": "", "reason": "" }
              ],
              "alert_thresholds": {
                "soil_moisture_critical": 0,
                "temperature_critical_high": 0,
                "temperature_critical_low": 0,
                "humidity_critical_high": 0
              },
              "optimization_tips": [""],
              "reason": { "spectrum_analysis": "", "trend_analysis": "", "summary": "" }
            }
            """,

        _ => "너는 식물 전문가이다. 사용자의 요청에 알맞은 분석을 JSON 형태로 제공하라."
    };

    // ── Gemini API 호출 ───────────────────────────────────────────────────
    private async Task<string> RequestGeminiAsync(
        string systemPrompt, string userPrompt, CancellationToken cancellationToken)
    {
        var body = new
        {
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            contents = new[] { new { role = "user", parts = new[] { new { text = userPrompt } } } },
            generationConfig = new { responseMimeType = "application/json" }
        };

        var endpoint = $"{_options.BaseUrl.TrimEnd('/')}/models/{_options.Model}:generateContent?key={_options.ApiKey}";
        var req      = new HttpRequestMessage(HttpMethod.Post, endpoint);
        req.Content  = JsonContent.Create(body);

        var res = await _httpClient.SendAsync(req, cancellationToken);
        res.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(cancellationToken));
        return doc.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? "{}";
    }

    // ── OpenAI API 호출 ───────────────────────────────────────────────────
    private async Task<string> RequestOpenAiAsync(
        string systemPrompt, string userPrompt, CancellationToken cancellationToken)
    {
        var body = new
        {
            model    = _options.Model,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user",   content = userPrompt }
            },
            response_format = new { type = "json_object" }
        };

        var endpoint = $"{_options.BaseUrl.TrimEnd('/')}/chat/completions";
        var req      = new HttpRequestMessage(HttpMethod.Post, endpoint);
        req.Headers.Add("Authorization", $"Bearer {_options.ApiKey}");
        req.Content = JsonContent.Create(body);

        var res = await _httpClient.SendAsync(req, cancellationToken);
        res.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(cancellationToken));
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "{}";
    }

    private static string BuildMockAnalysis(string analysisType, string promptSummary) =>
        $$$"""{"mock":true,"analysisType":"{{{analysisType}}}","note":"GPT disabled","promptLength":{{{promptSummary.Length}}}}""";
}
