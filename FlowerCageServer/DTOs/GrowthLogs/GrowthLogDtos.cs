namespace FlowerCageServer.DTOs.GrowthLogs;

public record GrowthLogCreateRequest(
    Guid PlantProfileId,
    string Title,
    string Content,
    decimal? HeightCm,
    DateTime LoggedAt,
    string? CameraImagePlaceholderName);

public record GrowthLogResponse(
    Guid Id,
    Guid PlantProfileId,
    string Title,
    string Content,
    decimal? HeightCm,
    string? CameraImagePlaceholderPath,
    string? CameraImagePublicUrl,
    DateTime LoggedAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);
