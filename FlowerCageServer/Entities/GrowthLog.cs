namespace FlowerCageServer.Entities;

public class GrowthLog : BaseEntity
{
    public Guid PlantProfileId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public decimal? HeightCm { get; set; }
    public string? CameraImagePlaceholderPath { get; set; }
    public string? CameraImagePublicUrl { get; set; }
    public DateTime LoggedAt { get; set; }

    public PlantProfile? PlantProfile { get; set; }
}
