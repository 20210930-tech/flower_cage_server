using Microsoft.AspNetCore.Mvc;
using FlowerCageServer.DTOs.Controls;
using FlowerCageServer.Services;

namespace FlowerCageServer.Controllers;

[ApiController]
[Route("api/control-schedules")]
public class ControlSchedulesController : ControllerBase
{
    private readonly IControlScheduleService _scheduleService;

    public ControlSchedulesController(IControlScheduleService scheduleService)
    {
        _scheduleService = scheduleService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ControlScheduleResponse>>> Get([FromQuery] Guid cageDeviceId, CancellationToken cancellationToken)
        => Ok(await _scheduleService.GetAsync(cageDeviceId, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ControlScheduleResponse>> Create([FromBody] ControlScheduleCreateRequest request, CancellationToken cancellationToken)
    {
        var response = await _scheduleService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { cageDeviceId = response.CageDeviceId }, response);
    }

    [HttpPatch("{id:guid}/enabled")]
    public async Task<ActionResult<ControlScheduleResponse>> SetEnabled(Guid id, [FromBody] ControlScheduleEnabledUpdateRequest request, CancellationToken cancellationToken)
        => Ok(await _scheduleService.SetEnabledAsync(id, request.Enabled, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _scheduleService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
