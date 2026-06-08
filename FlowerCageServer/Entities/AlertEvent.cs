using FlowerCageServer.Common.Enums;

namespace FlowerCageServer.Entities;

public class AlertEvent : BaseEntity
{
    public Guid? CageDeviceId { get; set; }
    public Guid? PlantProfileId { get; set; }
    public AlertLevel Level { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Source { get; set; } = "System";
    public bool IsAcknowledged { get; set; }
    public DateTime OccurredAt { get; set; }

    public CageDevice? CageDevice { get; set; }
    public PlantProfile? PlantProfile { get; set; }
}
