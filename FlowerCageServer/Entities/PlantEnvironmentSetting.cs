using FlowerCageServer.Common.Enums;

namespace FlowerCageServer.Entities;

public class PlantEnvironmentSetting : BaseEntity
{
    public Guid PlantProfileId { get; set; }
    public decimal SoilMoistureMin { get; set; }
    public decimal SoilMoistureMax { get; set; }
    public decimal TemperatureMin { get; set; }
    public decimal TemperatureMax { get; set; }
    public decimal HumidityMin { get; set; }
    public decimal HumidityMax { get; set; }
    public decimal LightLuxMin { get; set; }
    public decimal LightLuxMax { get; set; }
    public bool AutoModeEnabled { get; set; }
    public bool AlertEnabled { get; set; }

    // 제어 모드 (Manual/Auto/Ai). AutoModeEnabled는 호환용으로 유지(Mode != Manual).
    public ControlMode Mode { get; set; } = ControlMode.Manual;

    public PlantProfile? PlantProfile { get; set; }
}
