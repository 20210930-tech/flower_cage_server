using System.Net;
using Microsoft.EntityFrameworkCore;
using FlowerCageServer.Common.Enums;
using FlowerCageServer.Common.Exceptions;
using FlowerCageServer.DTOs.AutoControl;
using FlowerCageServer.DTOs.Sensors;
using FlowerCageServer.Entities;
using FlowerCageServer.Mapping;
using FlowerCageServer.Repositories;

namespace FlowerCageServer.Services;

public class SensorReadingService : ISensorReadingService
{
    private readonly IRepository<SensorReading>           _sensorRepository;
    private readonly IRepository<CageDevice>              _deviceRepository;
    private readonly IRepository<PlantProfile>            _plantRepository;
    private readonly IRepository<PlantEnvironmentSetting> _settingRepository;
    private readonly IRepository<User>                    _userRepository;
    private readonly IUnitOfWork                          _unitOfWork;
    private readonly IAlertService                        _alertService;
    private readonly IAutoControlService                  _autoControlService;
    private readonly IControlCommandService               _controlCommandService;
    private readonly ISystemEventLogService               _systemEventLogService;

    public SensorReadingService(
        IRepository<SensorReading> sensorRepository,
        IRepository<CageDevice> deviceRepository,
        IRepository<PlantProfile> plantRepository,
        IRepository<PlantEnvironmentSetting> settingRepository,
        IRepository<User> userRepository,
        IUnitOfWork unitOfWork,
        IAlertService alertService,
        IAutoControlService autoControlService,
        IControlCommandService controlCommandService,
        ISystemEventLogService systemEventLogService)
    {
        _sensorRepository      = sensorRepository;
        _deviceRepository      = deviceRepository;
        _plantRepository       = plantRepository;
        _settingRepository     = settingRepository;
        _userRepository        = userRepository;
        _unitOfWork            = unitOfWork;
        _alertService          = alertService;
        _autoControlService    = autoControlService;
        _controlCommandService = controlCommandService;
        _systemEventLogService = systemEventLogService;
    }

    public async Task<SensorReadingCreateResponse> CreateAsync(
        SensorReadingCreateRequest request, CancellationToken cancellationToken = default)
    {
        // 자동 등록 부분이다. 첫 센서 전송이 곧 연결 절차 역할을 한다.
        // 펌웨어가 스스로 만든 식별자로 처음 접속하거나, 서버 데이터가 초기화나 재시작으로 비어 있어
        // 서버가 모르는 식별자가 들어와도 찾을 수 없다는 오류로 막지 않는다.
        // 그 식별자 그대로 기기를 만들어 주어, 식별자 불일치로 인한 데이터 유실이나 무한 대기를 없애고
        // 전원 차단이나 서버 재시작에도 스스로 복구되게 한다.
        var device = await _deviceRepository.GetByIdAsync(request.CageDeviceId, cancellationToken);
        var deviceAutoCreated = device is null;
        if (device is null)
        {
            var ownerId = await EnsureDefaultUserIdAsync(cancellationToken);
            var shortId = request.CageDeviceId.ToString("N")[..8];
            device = new CageDevice
            {
                Id           = request.CageDeviceId,
                UserId       = ownerId,
                DeviceName   = $"자동등록 기기 {shortId}",
                DeviceSerial = $"AUTO-{shortId}",
                WifiStatus   = "Connected",
                IsOnline     = true,
                LastSeenAt   = DateTime.UtcNow,
            };
            await _deviceRepository.AddAsync(device, cancellationToken);
            await _systemEventLogService.LogAsync(
                "DeviceAutoRegistered", nameof(SensorReadingService),
                "Unknown device auto-registered from sensor reading.",
                new { device.Id }, cancellationToken);
        }

        // 식물 ID가 함께 왔는데 서버에 없으면 식물 + 기본 환경설정까지 자동 생성한다.
        if (request.PlantProfileId is Guid plantId &&
            await _plantRepository.GetByIdAsync(plantId, cancellationToken) is null)
        {
            await _plantRepository.AddAsync(new PlantProfile
            {
                Id           = plantId,
                UserId       = device.UserId,
                CageDeviceId = device.Id,
                PlantName    = "자동등록 식물",
                Species      = "미분류",
                RegisteredAt = DateTime.UtcNow,
                Memo         = "센서 데이터 수신 시 자동 생성됨",
            }, cancellationToken);

            // 기본 환경설정을 함께 만든다. 의도치 않은 자동 급수를 막으려고 수동 모드로 시작한다.
            // 사용자가 앱에서 자동 모드로 바꾸면 그때부터 자동 제어가 동작한다.
            await _settingRepository.AddAsync(new PlantEnvironmentSetting
            {
                PlantProfileId  = plantId,
                SoilMoistureMin = 300,   SoilMoistureMax = 700,
                TemperatureMin  = 22,    TemperatureMax  = 26,
                HumidityMin     = 55,    HumidityMax     = 70,
                LightLuxMin     = 10000, LightLuxMax     = 25000,
                AutoModeEnabled = false, AlertEnabled    = true,
                Mode            = Common.Enums.ControlMode.Manual,
            }, cancellationToken);

            await _systemEventLogService.LogAsync(
                "PlantAutoRegistered", nameof(SensorReadingService),
                "Unknown plant auto-registered from sensor reading.",
                new { PlantProfileId = plantId, device.Id }, cancellationToken);
        }

        // 배열이 비어있으면 예외
        if (request.SoilMoistures.Length == 0 || request.Temperatures.Length == 0 || request.Humidities.Length == 0)
            throw new ApiException("센서 배열은 비어 있을 수 없습니다.");

        // 서버에서 평균 계산 (알림·자동제어 로직에서 사용)
        var avgSoil  = request.SoilMoistures.Average();
        var avgTemp  = request.Temperatures.Average();
        var avgHumid = request.Humidities.Average();

        var entity = new SensorReading
        {
            CageDeviceId   = request.CageDeviceId,
            PlantProfileId = request.PlantProfileId,
            // 평균값
            SoilMoisture   = avgSoil,
            Temperature    = avgTemp,
            Humidity       = avgHumid,
            // 개별 원시값 배열 (전체 저장)
            SoilMoistures  = request.SoilMoistures,
            Temperatures   = request.Temperatures,
            Humidities     = request.Humidities,
            LightLux       = request.LightLux,
            Nm415 = request.Nm415, Nm445 = request.Nm445,
            Nm480 = request.Nm480, Nm515 = request.Nm515,
            Nm555 = request.Nm555, Nm590 = request.Nm590,
            Nm630 = request.Nm630, Nm680 = request.Nm680,
            Clear = request.Clear, Nir   = request.Nir,
            RecordedAt = request.RecordedAt ?? DateTime.UtcNow,
        };

        await _sensorRepository.AddAsync(entity, cancellationToken);

        // 하트비트 갱신이다. 센서 데이터가 들어왔다는 것은 기기가 살아 있다는 뜻이므로
        // 마지막 통신 시각을 새로 적는다. 온라인 여부는 조회할 때 이 시각의 신선도로 판정한다.
        // 방금 자동 생성한 기기는 이미 추가 대기 상태라서, 여기서 갱신을 또 호출하면
        // 새로 넣기 대신 수정으로 처리되어 실패한다. 그래서 자동 생성한 경우에는 갱신을 건너뛴다.
        // 갱신할 값은 기기를 만들 때 이미 채워 두었다.
        device.LastSeenAt = DateTime.UtcNow;
        device.IsOnline = true;
        device.WifiStatus = "Connected";
        if (!deviceAutoCreated) _deviceRepository.Update(device);

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _systemEventLogService.LogAsync(
            "SensorReadingCreated", nameof(SensorReadingService),
            "Sensor reading stored.",
            new { entity.Id, entity.CageDeviceId }, cancellationToken);

        // 식물이 연결된 센서 데이터이면 알림을 평가하고 자동 제어를 수행한다.
        if (request.PlantProfileId.HasValue)
        {
            var setting = await _settingRepository.FirstOrDefaultAsync(
                x => x.PlantProfileId == request.PlantProfileId.Value, cancellationToken);

            if (setting is not null)
            {
                if (setting.AlertEnabled)
                    await EvaluateAlertsAsync(entity, setting, cancellationToken);

                // 자동 모드일 때만 센서 저장 직후 규칙 기반 자동 평가를 한다.
                // 인공지능 모드는 별도 백그라운드 서비스가 주기적으로 처리하고, 수동 모드는 자동화가 없다.
                if (setting.Mode == Common.Enums.ControlMode.Auto)
                {
                    try
                    {
                        await _autoControlService.EvaluateAsync(
                            new AutoControlEvaluationRequest(request.PlantProfileId.Value, entity.Id),
                            cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        await _systemEventLogService.LogAsync(
                            "AutoControlError", nameof(SensorReadingService),
                            "Auto-control evaluation failed after sensor save.",
                            new { Error = ex.Message, entity.Id }, cancellationToken);
                    }
                }
            }
        }

        // 연결 왕복을 줄이는 부분이다. 펌웨어가 제어 명령을 따로 조회하는 요청을 한 번 덜 열도록,
        // 이 기기의 대기 중 명령을 센서 응답에 함께 실어 보낸다. 방금 자동 제어가 만든 명령도 포함된다.
        var pendingCommands = await _controlCommandService.GetAsync(
            entity.CageDeviceId, CommandStatus.Pending, 10, cancellationToken);

        return new SensorReadingCreateResponse(entity.ToResponse(), pendingCommands);
    }

    public async Task<IReadOnlyCollection<SensorReadingResponse>> GetAsync(
        Guid? cageDeviceId, Guid? plantProfileId, int take, CancellationToken cancellationToken = default)
    {
        var query = _sensorRepository.Query();

        if (cageDeviceId.HasValue)
            query = query.Where(x => x.CageDeviceId == cageDeviceId.Value);

        if (plantProfileId.HasValue)
            query = query.Where(x => x.PlantProfileId == plantProfileId.Value);

        var readings = await query
            .OrderByDescending(x => x.RecordedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        return readings.Select(x => x.ToResponse()).ToList();
    }

    private async Task EvaluateAlertsAsync(
        SensorReading reading, PlantEnvironmentSetting setting, CancellationToken cancellationToken)
    {
        var violations = new List<string>();
        var level      = AlertLevel.Normal;

        EvaluateRange("SoilMoisture", reading.SoilMoisture,
            setting.SoilMoistureMin, setting.SoilMoistureMax, violations, ref level);
        EvaluateRange("Temperature", reading.Temperature,
            setting.TemperatureMin, setting.TemperatureMax, violations, ref level);
        EvaluateRange("Humidity", reading.Humidity,
            setting.HumidityMin, setting.HumidityMax, violations, ref level);
        EvaluateRange("LightLux", reading.LightLux,
            setting.LightLuxMin, setting.LightLuxMax, violations, ref level);

        if (violations.Count == 0) return;

        await _alertService.CreateAsync(
            level,
            "Environment threshold exceeded",
            string.Join("; ", violations),
            "SensorMonitor",
            reading.CageDeviceId,
            reading.PlantProfileId,
            cancellationToken);
    }

    // 자동 등록 기기를 소유시킬 사용자. 기존 사용자가 있으면 가장 먼저 만든 사용자를,
    // 없으면 기본 사용자를 새로 만든다. (앱도 첫 사용자를 기본으로 쓰므로 서로 일치)
    private async Task<Guid> EnsureDefaultUserIdAsync(CancellationToken cancellationToken)
    {
        var existing = await _userRepository.Query()
            .OrderBy(u => u.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is not null) return existing.Id;

        var user = new User { Name = "Flower Cage 사용자" };
        await _userRepository.AddAsync(user, cancellationToken);
        return user.Id;
    }

    private static void EvaluateRange(
        string fieldName, decimal value, decimal min, decimal max,
        List<string> violations, ref AlertLevel currentLevel)
    {
        if (value < min)
        {
            violations.Add($"{fieldName} below minimum: {value} < {min}");
            currentLevel = AlertLevel.Danger;
        }
        else if (value > max)
        {
            violations.Add($"{fieldName} above maximum: {value} > {max}");
            if (currentLevel < AlertLevel.Warning) currentLevel = AlertLevel.Warning;
        }
    }
}
