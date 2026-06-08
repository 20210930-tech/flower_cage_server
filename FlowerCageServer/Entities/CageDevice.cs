namespace FlowerCageServer.Entities;

public class CageDevice : BaseEntity
{
    public Guid UserId { get; set; }
    public string DeviceName { get; set; } = string.Empty;
    public string DeviceSerial { get; set; } = string.Empty;
    public string WifiStatus { get; set; } = "Unknown";
    public bool IsOnline { get; set; }
    public DateTime? LastSeenAt { get; set; }

    public User? User { get; set; }
    public ICollection<PlantProfile> PlantProfiles { get; set; } = new List<PlantProfile>();
    public ICollection<SensorReading> SensorReadings { get; set; } = new List<SensorReading>();
    public ICollection<ControlCommand> ControlCommands { get; set; } = new List<ControlCommand>();
    public ICollection<AlertEvent> AlertEvents { get; set; } = new List<AlertEvent>();
}
