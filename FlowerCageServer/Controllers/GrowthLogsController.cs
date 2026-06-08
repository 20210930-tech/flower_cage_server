using Microsoft.AspNetCore.Mvc;
using FlowerCageServer.DTOs.GrowthLogs;
using FlowerCageServer.Entities;
using FlowerCageServer.Repositories;
using FlowerCageServer.Services;

namespace FlowerCageServer.Controllers;

[ApiController]
[Route("api/growth-logs")]
public class GrowthLogsController : ControllerBase
{
    private readonly IGrowthLogService _growthLogService;
    private readonly IRepository<GrowthLog> _growthRepository;

    public GrowthLogsController(IGrowthLogService growthLogService, IRepository<GrowthLog> growthRepository)
    {
        _growthLogService = growthLogService;
        _growthRepository = growthRepository;
    }

    // 저장된 스냅샷/생육 이미지 서빙
    [HttpGet("{id:guid}/image")]
    public async Task<IActionResult> GetImage(Guid id, CancellationToken cancellationToken)
    {
        var log = await _growthRepository.GetByIdAsync(id, cancellationToken);
        if (log?.CameraImagePlaceholderPath is null) return NotFound();

        var absPath = Path.Combine(AppContext.BaseDirectory,
            log.CameraImagePlaceholderPath.Replace('/', Path.DirectorySeparatorChar));
        if (!System.IO.File.Exists(absPath)) return NotFound();

        var bytes = await System.IO.File.ReadAllBytesAsync(absPath, cancellationToken);
        return File(bytes, "image/jpeg");
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<GrowthLogResponse>>> Get([FromQuery] Guid plantProfileId, CancellationToken cancellationToken)
        => Ok(await _growthLogService.GetAsync(plantProfileId, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<GrowthLogResponse>> Create([FromBody] GrowthLogCreateRequest request, CancellationToken cancellationToken)
    {
        var response = await _growthLogService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { plantProfileId = response.PlantProfileId }, response);
    }
}
