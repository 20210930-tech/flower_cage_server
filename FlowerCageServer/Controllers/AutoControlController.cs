using Microsoft.AspNetCore.Mvc;
using FlowerCageServer.DTOs.AutoControl;
using FlowerCageServer.Services;

namespace FlowerCageServer.Controllers;

[ApiController]
[Route("api/auto-control")]
public class AutoControlController : ControllerBase
{
    private readonly IAutoControlService _autoControlService;

    public AutoControlController(IAutoControlService autoControlService)
    {
        _autoControlService = autoControlService;
    }

    [HttpPost("evaluate")]
    public async Task<ActionResult<AutoControlEvaluationResponse>> Evaluate([FromBody] AutoControlEvaluationRequest request, CancellationToken cancellationToken)
        => Ok(await _autoControlService.EvaluateAsync(request, cancellationToken));
}
