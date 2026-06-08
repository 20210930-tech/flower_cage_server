using System.Collections.Concurrent;
using System.Text.Json;

namespace FlowerCageServer.Services;

// 기기별 최신 카메라 프레임(JPEG)을 메모리에 보관하는 릴레이 저장소.
// 카메라가 POST로 올리고, 앱이 GET으로 받아간다. (프레임은 서버 재시작 시 사라짐 — 정상)
//
// 단, "카메라 재설정" 요청 플래그는 파일에 영속화한다.
//  - 카메라가 잠깐 오프라인이거나(다음 프레임 업로드 때 205로 전달되므로)
//  - 서버가 재시작돼도
//    재설정 요청이 사라지지 않고, 카메라가 다시 켜져 첫 프레임을 올릴 때 전달된다.
// 프레임 업로드(핫패스)는 인메모리만 읽으므로 디스크 비용이 없다.
// 파일 쓰기는 요청/소비 시점(드묾)에만 발생한다.
public class CameraFrameStore
{
    private readonly ConcurrentDictionary<Guid, (byte[] Jpeg, DateTime At)> _frames = new();
    // 앱이 요청한 카메라 재설정 플래그. 다음 프레임 업로드 응답(205)으로 카메라에 전달.
    private readonly ConcurrentDictionary<Guid, bool> _resetRequests = new();
    private readonly string _resetFilePath;
    private readonly object _fileLock = new();

    public CameraFrameStore()
    {
        _resetFilePath = Path.Combine(AppContext.BaseDirectory, "camera-reset-requests.json");
        LoadResetRequests();
    }

    public void RequestReset(Guid deviceId)
    {
        _resetRequests[deviceId] = true;
        PersistResetRequests();
    }

    // 재설정 요청이 있으면 true 반환 후 소비(1회성)
    public bool ConsumeReset(Guid deviceId)
    {
        if (_resetRequests.TryRemove(deviceId, out _))
        {
            PersistResetRequests();
            return true;
        }
        return false;
    }

    public void Set(Guid deviceId, byte[] jpeg) => _frames[deviceId] = (jpeg, DateTime.UtcNow);

    public bool TryGet(Guid deviceId, out byte[] jpeg, out DateTime at)
    {
        if (_frames.TryGetValue(deviceId, out var f))
        {
            jpeg = f.Jpeg;
            at = f.At;
            return true;
        }
        jpeg = Array.Empty<byte>();
        at = default;
        return false;
    }

    // 시작 시 파일에서 미처리 재설정 요청을 복원
    private void LoadResetRequests()
    {
        try
        {
            if (!File.Exists(_resetFilePath)) return;
            var ids = JsonSerializer.Deserialize<List<Guid>>(File.ReadAllText(_resetFilePath));
            if (ids is null) return;
            foreach (var id in ids) _resetRequests[id] = true;
        }
        catch
        {
            // 파일 손상/읽기 실패는 무시 — 재설정 플래그가 없을 뿐 정상 동작엔 지장 없음
        }
    }

    private void PersistResetRequests()
    {
        lock (_fileLock)
        {
            try
            {
                File.WriteAllText(_resetFilePath, JsonSerializer.Serialize(_resetRequests.Keys.ToList()));
            }
            catch
            {
                // 디스크 쓰기 실패는 무시 — 인메모리 플래그는 여전히 유효
            }
        }
    }
}
