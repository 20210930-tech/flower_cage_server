using FlowerCageServer.Common.Enums;

namespace FlowerCageServer.DTOs.Gpt;

public record GptAnalysisRequest(Guid? PlantProfileId, Guid? SensorReadingId, string AnalysisType, string PromptSummary);

public record GptAnalysisResponse(
    Guid Id,
    Guid? PlantProfileId,
    Guid? SensorReadingId,
    string AnalysisType,
    string? PromptSummary,
    string? ResponseContent,
    string? Recommendation,
    GptAnalysisStatus Status,
    DateTime RequestedAt,
    DateTime? CompletedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);
