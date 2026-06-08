using Microsoft.AspNetCore.Mvc;
using FlowerCageServer.DTOs.Plants;
using FlowerCageServer.Services;

namespace FlowerCageServer.Controllers;

[ApiController]
[Route("api/plant-profiles")]
public class PlantProfilesController : ControllerBase
{
    private readonly IPlantProfileService _plantProfileService;

    public PlantProfilesController(IPlantProfileService plantProfileService)
    {
        _plantProfileService = plantProfileService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<PlantProfileResponse>>> GetByUser([FromQuery] Guid userId, CancellationToken cancellationToken)
        => Ok(await _plantProfileService.GetByUserAsync(userId, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<PlantProfileResponse>> Create([FromBody] PlantProfileCreateRequest request, CancellationToken cancellationToken)
    {
        var response = await _plantProfileService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetByUser), new { userId = response.UserId }, response);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<PlantProfileResponse>> Update(Guid id, [FromBody] PlantProfileUpdateRequest request, CancellationToken cancellationToken)
        => Ok(await _plantProfileService.UpdateAsync(id, request, cancellationToken));
}
