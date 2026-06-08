using Microsoft.AspNetCore.Mvc;
using FlowerCageServer.DTOs.SystemEvents;
using FlowerCageServer.Services;

namespace FlowerCageServer.Controllers;

[ApiController]
[Route("api/system-events")]
public class SystemEventsController : ControllerBase
{
    private readonly ISystemEventLogService _systemEventLogService;

    public SystemEventsController(ISystemEventLogService systemEventLogService)
    {
        _systemEventLogService = systemEventLogService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<SystemEventLogResponse>>> Get(CancellationToken cancellationToken)
        => Ok(await _systemEventLogService.GetLogsAsync(cancellationToken));
}
