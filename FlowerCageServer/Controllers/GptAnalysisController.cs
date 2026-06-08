using Microsoft.AspNetCore.Mvc;
using FlowerCageServer.DTOs.Gpt;
using FlowerCageServer.Services;

namespace FlowerCageServer.Controllers;

[ApiController]
[Route("api/gpt-analyses")]
public class GptAnalysisController : ControllerBase
{
    private readonly IGptAnalysisService _gptAnalysisService;

    public GptAnalysisController(IGptAnalysisService gptAnalysisService)
    {
        _gptAnalysisService = gptAnalysisService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<GptAnalysisResponse>>> Get([FromQuery] Guid? plantProfileId, CancellationToken cancellationToken)
        => Ok(await _gptAnalysisService.GetLogsAsync(plantProfileId, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<GptAnalysisResponse>> Analyze([FromBody] GptAnalysisRequest request, CancellationToken cancellationToken)
    {
        var response = await _gptAnalysisService.AnalyzeAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { plantProfileId = response.PlantProfileId }, response);
    }
}
