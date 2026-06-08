using Microsoft.AspNetCore.Mvc;
using FlowerCageServer.DTOs.Alerts;
using FlowerCageServer.Services;

namespace FlowerCageServer.Controllers;

[ApiController]
[Route("api/alerts")]
public class AlertsController : ControllerBase
{
    private readonly IAlertService _alertService;

    public AlertsController(IAlertService alertService)
    {
        _alertService = alertService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<AlertEventResponse>>> Get([FromQuery] Guid? cageDeviceId, [FromQuery] Guid? plantProfileId, CancellationToken cancellationToken)
        => Ok(await _alertService.GetAsync(cageDeviceId, plantProfileId, cancellationToken));
}
