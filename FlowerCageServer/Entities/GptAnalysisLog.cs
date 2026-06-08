using FlowerCageServer.Common.Enums;

namespace FlowerCageServer.Entities;

public class GptAnalysisLog : BaseEntity
{
    public Guid? PlantProfileId { get; set; }
    public Guid? SensorReadingId { get; set; }
    public string AnalysisType { get; set; } = string.Empty;
    public string? PromptSummary { get; set; }
    public string? ResponseContent { get; set; }
    public string? Recommendation { get; set; }
    public GptAnalysisStatus Status { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public PlantProfile? PlantProfile { get; set; }
    public SensorReading? SensorReading { get; set; }
}
