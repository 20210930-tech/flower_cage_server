using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using FlowerCageServer.Common.Exceptions;
using FlowerCageServer.Common.Options;
using FlowerCageServer.DTOs.GrowthLogs;
using FlowerCageServer.Entities;
using FlowerCageServer.Mapping;
using FlowerCageServer.Repositories;
using FlowerCageServer.Services;

namespace FlowerCageServer.Controllers;

[ApiController]
[Route("api/cameras")]
public class CamerasController : ControllerBase
{
    // 프레임이 이 시간보다 오래되면 카메라 오프라인으로 간주
    private static readonly TimeSpan FrameTtl = TimeSpan.FromSeconds(10);

    private readonly CameraFrameStore _store;
    private readonly IRepository<GrowthLog> _growthRepository;
    private readonly IRepository<PlantProfile> _plantRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly StorageOptions _storage;

    public CamerasController(
        CameraFrameStore store,
        IRepository<GrowthLog> growthRepository,
        IRepository<PlantProfile> plantRepository,
        IUnitOfWork unitOfWork,
        IOptions<StorageOptions> storage)
    {
        _store = store;
        _growthRepository = growthRepository;
        _plantRepository = plantRepository;
        _unitOfWork = unitOfWork;
        _storage = storage.Value;
    }

    // 카메라 → 서버: 최신 프레임 업로드 (body = JPEG 바이트)
    [HttpPost("{deviceId:guid}/frame")]
    public async Task<IActionResult> UploadFrame(Guid deviceId, CancellationToken cancellationToken)
    {
        using var ms = new MemoryStream();
        await Request.Body.CopyToAsync(ms, cancellationToken);
        var bytes = ms.ToArray();
        if (bytes.Length == 0) return BadRequest("빈 프레임입니다.");
        _store.Set(deviceId, bytes);

        // 앱에서 재설정 요청이 있었으면 205로 카메라에 알림 → 카메라가 NVS 비우고 BLE 모드로
        if (_store.ConsumeReset(deviceId))
        {
            return StatusCode(205); // Reset Content
        }
        return Ok();
    }

    // 앱 → 카메라 재설정 요청 (다음 프레임 업로드 응답으로 전달됨)
    [HttpPost("{deviceId:guid}/reset")]
    public IActionResult RequestReset(Guid deviceId)
    {
        _store.RequestReset(deviceId);
        return Ok();
    }

    // 앱 → 서버: 최신 프레임 받기
    [HttpGet("{deviceId:guid}/frame")]
    public IActionResult GetFrame(Guid deviceId)
    {
        if (_store.TryGet(deviceId, out var jpeg, out var at) &&
            DateTime.UtcNow - at <= FrameTtl)
        {
            Response.Headers.CacheControl = "no-store";
            return File(jpeg, "image/jpeg");
        }
        return NotFound();
    }

    // 카메라 온라인 여부 (최근 프레임 신선도)
    [HttpGet("{deviceId:guid}/status")]
    public IActionResult GetStatus(Guid deviceId)
    {
        if (_store.TryGet(deviceId, out _, out var at))
        {
            var ageMs = (DateTime.UtcNow - at).TotalMilliseconds;
            return Ok(new { online = ageMs <= FrameTtl.TotalMilliseconds, ageMs });
        }
        return Ok(new { online = false, ageMs = (double?)null });
    }

    // 스크린샷: 현재 최신 프레임을 디스크에 저장 + 생육 기록 생성
    [HttpPost("{deviceId:guid}/snapshot")]
    public async Task<ActionResult<GrowthLogResponse>> Snapshot(
        Guid deviceId, [FromQuery] Guid plantProfileId, CancellationToken cancellationToken)
    {
        if (!_store.TryGet(deviceId, out var jpeg, out _))
            throw new ApiException("최신 카메라 프레임이 없습니다.", HttpStatusCode.NotFound);

        if (await _plantRepository.GetByIdAsync(plantProfileId, cancellationToken) is null)
            throw new ApiException("Plant profile not found.", HttpStatusCode.NotFound);

        var id = Guid.NewGuid();
        var relPath = $"{_storage.CameraImageBasePath.TrimEnd('/')}/{id}.jpg";
        var absPath = Path.Combine(AppContext.BaseDirectory,
            relPath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(absPath)!);
        await System.IO.File.WriteAllBytesAsync(absPath, jpeg, cancellationToken);

        var entity = new GrowthLog
        {
            Id = id,
            PlantProfileId = plantProfileId,
            Title = "카메라 스냅샷",
            Content = "앱에서 저장한 카메라 스냅샷",
            LoggedAt = DateTime.UtcNow,
            CameraImagePlaceholderPath = relPath,
            CameraImagePublicUrl = $"/api/growth-logs/{id}/image",
        };
        await _growthRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Ok(entity.ToResponse());
    }
}
