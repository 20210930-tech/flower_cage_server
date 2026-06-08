using System.Net;
using Microsoft.EntityFrameworkCore;
using FlowerCageServer.Common.Exceptions;
using FlowerCageServer.DTOs.Users;
using FlowerCageServer.Entities;
using FlowerCageServer.Mapping;
using FlowerCageServer.Repositories;

namespace FlowerCageServer.Services;

public class UserService : IUserService
{
    private readonly IRepository<User> _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISystemEventLogService _systemEventLogService;

    public UserService(IRepository<User> userRepository, IUnitOfWork unitOfWork, ISystemEventLogService systemEventLogService)
    {
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _systemEventLogService = systemEventLogService;
    }

    public async Task<UserResponse> CreateAsync(UserCreateRequest request, CancellationToken cancellationToken = default)
    {
        var entity = new User
        {
            Name = request.Name
        };

        await _userRepository.AddAsync(entity, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _systemEventLogService.LogAsync("UserCreated", nameof(UserService), "User has been created.", new { entity.Id, entity.Name }, cancellationToken);
        return entity.ToResponse();
    }

    public async Task<IReadOnlyCollection<UserResponse>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.Query().OrderBy(x => x.Name).ToListAsync(cancellationToken);
        return users.Select(x => x.ToResponse()).ToList();
    }

    public async Task<UserResponse> UpdateAsync(Guid id, UserUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _userRepository.GetByIdAsync(id, cancellationToken)
            ?? throw new ApiException("User not found.", HttpStatusCode.NotFound);

        entity.Name = request.Name;

        _userRepository.Update(entity);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _systemEventLogService.LogAsync("UserUpdated", nameof(UserService), "User has been updated.", new { entity.Id }, cancellationToken);
        return entity.ToResponse();
    }
}
