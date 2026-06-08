namespace FlowerCageServer.Entities;

public class PlantProfile : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid CageDeviceId { get; set; }
    public string PlantName { get; set; } = string.Empty;
    public string? Species { get; set; }
    public DateTime RegisteredAt { get; set; }
    public string? Memo { get; set; }

    public User? User { get; set; }
    public CageDevice? CageDevice { get; set; }
    public PlantEnvironmentSetting? EnvironmentSetting { get; set; }
    public ICollection<SensorReading> SensorReadings { get; set; } = new List<SensorReading>();
    public ICollection<GrowthLog> GrowthLogs { get; set; } = new List<GrowthLog>();
    public ICollection<GptAnalysisLog> GptAnalysisLogs { get; set; } = new List<GptAnalysisLog>();
}
