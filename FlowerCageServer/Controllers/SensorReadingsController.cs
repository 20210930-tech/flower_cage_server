using Microsoft.AspNetCore.Mvc;
using FlowerCageServer.DTOs.Sensors;
using FlowerCageServer.Services;

namespace FlowerCageServer.Controllers;

[ApiController]
[Route("api/sensor-readings")]
public class SensorReadingsController : ControllerBase
{
    private readonly ISensorReadingService _sensorReadingService;

    public SensorReadingsController(ISensorReadingService sensorReadingService)
    {
        _sensorReadingService = sensorReadingService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<SensorReadingResponse>>> Get(
        [FromQuery] Guid? cageDeviceId,
        [FromQuery] Guid? plantProfileId,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
        => Ok(await _sensorReadingService.GetAsync(cageDeviceId, plantProfileId, Math.Clamp(take, 1, 500), cancellationToken));

    [HttpPost]
    public async Task<ActionResult<SensorReadingCreateResponse>> Create([FromBody] SensorReadingCreateRequest request, CancellationToken cancellationToken)
    {
        var response = await _sensorReadingService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get),
            new { cageDeviceId = response.Reading.CageDeviceId, plantProfileId = response.Reading.PlantProfileId },
            response);
    }
}
