namespace FlowerCageServer.DTOs.Plants;

// Id: 앱이 BLE로 기기에 미리 보낸 PlantProfileId를 그대로 등록할 때 사용 (없으면 서버가 생성)
public record PlantProfileCreateRequest(Guid UserId, Guid CageDeviceId, string PlantName, string? Species, DateTime RegisteredAt, string? Memo, Guid? Id = null);

public record PlantProfileUpdateRequest(string PlantName, string? Species, string? Memo);

public record PlantProfileResponse(
    Guid Id,
    Guid UserId,
    Guid CageDeviceId,
    string PlantName,
    string? Species,
    DateTime RegisteredAt,
    string? Memo,
    DateTime CreatedAt,
    DateTime UpdatedAt);
