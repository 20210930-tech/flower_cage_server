namespace FlowerCageServer.Entities;

public class SensorReading : BaseEntity
{
    public Guid CageDeviceId { get; set; }
    public Guid? PlantProfileId { get; set; }

    // ── 평균값 (알림·자동제어 로직에서 사용) ──────────────────
    public decimal SoilMoisture { get; set; }
    public decimal Temperature { get; set; }
    public decimal Humidity { get; set; }

    // ── 개별 센서 원시값 배열 (확장 가능, PostgreSQL numeric[]) ──
    // 센서 개수에 관계없이 모두 저장. 개수는 배열 길이로 확인 가능.
    public decimal[] SoilMoistures { get; set; } = [];
    public decimal[] Temperatures  { get; set; } = [];
    public decimal[] Humidities    { get; set; } = [];

    // ── AS7341 스펙트럼 센서 (10채널) ────────────────────────
    public int Nm415 { get; set; }
    public int Nm445 { get; set; }
    public int Nm480 { get; set; }
    public int Nm515 { get; set; }
    public int Nm555 { get; set; }
    public int Nm590 { get; set; }
    public int Nm630 { get; set; }
    public int Nm680 { get; set; }
    public int Clear { get; set; }
    public int Nir   { get; set; }

    public decimal LightLux { get; set; }

    public DateTime RecordedAt { get; set; }

    public CageDevice?   CageDevice   { get; set; }
    public PlantProfile? PlantProfile { get; set; }
}
