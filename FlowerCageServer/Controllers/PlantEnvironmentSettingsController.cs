using Microsoft.AspNetCore.Mvc;
using FlowerCageServer.DTOs.PlantEnvironmentSettings;
using FlowerCageServer.Services;

namespace FlowerCageServer.Controllers;

[ApiController]
[Route("api/plant-environment-settings")]
public class PlantEnvironmentSettingsController : ControllerBase
{
    private readonly IPlantEnvironmentSettingService _settingService;

    public PlantEnvironmentSettingsController(IPlantEnvironmentSettingService settingService)
    {
        _settingService = settingService;
    }

    [HttpGet("{plantProfileId:guid}")]
    public async Task<ActionResult<PlantEnvironmentSettingResponse>> Get(Guid plantProfileId, CancellationToken cancellationToken)
        => Ok(await _settingService.GetByPlantProfileAsync(plantProfileId, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<PlantEnvironmentSettingResponse>> Upsert([FromBody] UpsertPlantEnvironmentSettingRequest request, CancellationToken cancellationToken)
        => Ok(await _settingService.UpsertAsync(request, cancellationToken));
}
