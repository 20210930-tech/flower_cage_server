using Microsoft.AspNetCore.Mvc;
using FlowerCageServer.DTOs.CageDevices;
using FlowerCageServer.Services;

namespace FlowerCageServer.Controllers;

[ApiController]
[Route("api/cage-devices")]
public class CageDevicesController : ControllerBase
{
    private readonly ICageDeviceService _deviceService;

    public CageDevicesController(ICageDeviceService deviceService)
    {
        _deviceService = deviceService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<CageDeviceResponse>>> GetByUser([FromQuery] Guid userId, CancellationToken cancellationToken)
        => Ok(await _deviceService.GetByUserAsync(userId, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<CageDeviceResponse>> Create([FromBody] CageDeviceCreateRequest request, CancellationToken cancellationToken)
    {
        var response = await _deviceService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetByUser), new { userId = response.UserId }, response);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CageDeviceResponse>> Update(Guid id, [FromBody] CageDeviceUpdateRequest request, CancellationToken cancellationToken)
        => Ok(await _deviceService.UpdateAsync(id, request, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await _deviceService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
