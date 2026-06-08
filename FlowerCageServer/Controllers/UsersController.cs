using Microsoft.AspNetCore.Mvc;
using FlowerCageServer.DTOs.Users;
using FlowerCageServer.Services;

namespace FlowerCageServer.Controllers;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<UserResponse>>> GetAll(CancellationToken cancellationToken)
        => Ok(await _userService.GetAllAsync(cancellationToken));

    [HttpPost]
    public async Task<ActionResult<UserResponse>> Create([FromBody] UserCreateRequest request, CancellationToken cancellationToken)
    {
        var response = await _userService.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetAll), new { id = response.Id }, response);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserResponse>> Update(Guid id, [FromBody] UserUpdateRequest request, CancellationToken cancellationToken)
        => Ok(await _userService.UpdateAsync(id, request, cancellationToken));
}
