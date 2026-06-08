namespace FlowerCageServer.DTOs.Users;

public record UserCreateRequest(string Name);

public record UserUpdateRequest(string Name);

public record UserResponse(Guid Id, string Name, DateTime CreatedAt, DateTime UpdatedAt);
