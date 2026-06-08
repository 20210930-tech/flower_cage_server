using FlowerCageServer.DTOs.Controls;

namespace FlowerCageServer.DTOs.Sensors;

public record SensorReadingCreateRequest(
    Guid      CageDeviceId,
    Guid?     PlantProfileId,
    // 배열로 전송 (센서 개수 무관하게 확장 가능)
    decimal[] SoilMoistures,
    decimal[] Temperatures,
    decimal[] Humidities,
    decimal   LightLux,
    int Nm415, int Nm445, int Nm480, int Nm515,
    int Nm555, int Nm590, int Nm630, int Nm680,
    int Clear, int Nir,
    DateTime? RecordedAt = null);

public record SensorReadingResponse(
    Guid      Id,
    Guid      CageDeviceId,
    Guid?     PlantProfileId,
    // 평균값 (앱 표시·자동제어용)
    decimal   SoilMoisture,
    decimal   Temperature,
    decimal   Humidity,
    decimal   LightLux,
    // 원시값 배열 (전체 저장)
    decimal[] SoilMoistures,
    decimal[] Temperatures,
    decimal[] Humidities,
    int Nm415, int Nm445, int Nm480, int Nm515,
    int Nm555, int Nm590, int Nm630, int Nm680,
    int Clear, int Nir,
    DateTime RecordedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);

// 센서 전송 요청에 대한 응답이다.
// 센서 저장과 자동 제어 평가를 끝낸 뒤, 이 기기의 대기 중 명령을 함께 내려준다.
// 펌웨어가 제어 명령을 따로 조회하는 왕복을 한 번 덜 열게 해 연결 실패 지점을 줄이려는 것이다.
public record SensorReadingCreateResponse(
    SensorReadingResponse Reading,
    IReadOnlyCollection<ControlCommandResponse> PendingCommands);
