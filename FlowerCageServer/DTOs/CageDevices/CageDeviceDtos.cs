namespace FlowerCageServer.DTOs.CageDevices;

// Id: 앱이 BLE로 기기에 미리 보낸 DeviceId를 그대로 등록할 때 사용 (없으면 서버가 생성)
public record CageDeviceCreateRequest(Guid UserId, string DeviceName, string DeviceSerial, string WifiStatus, bool IsOnline, DateTime? LastSeenAt, Guid? Id = null);

public record CageDeviceUpdateRequest(string DeviceName, string WifiStatus, bool IsOnline, DateTime? LastSeenAt);

public record CageDeviceResponse(
    Guid Id,
    Guid UserId,
    string DeviceName,
    string DeviceSerial,
    string WifiStatus,
    bool IsOnline,
    DateTime? LastSeenAt,
    DateTime CreatedAt,
    DateTime UpdatedAt);
