namespace QuanLyCafe.Application.Staff;

public record UserDto(Guid Id, string Username, string FullName, int RoleId, string Role, bool IsActive);

public record RoleDto(int Id, string Name);

public record CreateUserRequest(string? Username, string? Password, string? FullName, int RoleId);

public record UpdateUserRequest(string? FullName, int RoleId);

public record ResetPasswordRequest(string? NewPassword);
