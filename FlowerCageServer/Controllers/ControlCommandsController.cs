using Microsoft.AspNetCore.Mvc;
using FlowerCageServer.Common.Enums;
using FlowerCageServer.DTOs.Controls;
using FlowerCageServer.Services;

namespace FlowerCageServer.Controllers;

[ApiController]
[Route("api/control-commands")]
public class ControlCommandsController : ControllerBase
{
    private readonly IControlCommandService _controlCommandService;

    public ControlCommandsController(IControlCommandService controlCommandService)
    {
        _controlCommandService = controlCommandService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<ControlCommandResponse>>> Get([FromQuery] Guid cageDeviceId, [FromQuery] CommandStatus? status, [FromQuery] int take, CancellationToken cancellationToken)
        => Ok(await _controlCommandService.GetAsync(cageDeviceId, status, take, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<ControlCommandResponse>> Create([FromBody] ControlCommandCreateRequest request, CancellationToken cancellationToken)
    {
        var response = await _controlCommandService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { cageDeviceId = response.CageDeviceId }, response);
    }

    [HttpPatch("{id:guid}/status")]
    public async Task<ActionResult<ControlCommandResponse>> UpdateStatus(Guid id, [FromBody] ControlCommandStatusUpdateRequest request, CancellationToken cancellationToken)
        => Ok(await _controlCommandService.UpdateStatusAsync(id, request, cancellationToken));
}
