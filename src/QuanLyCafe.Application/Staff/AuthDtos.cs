namespace QuanLyCafe.Application.Staff;

public record LoginRequest(string? Username, string? Password);

public record LoginResponse(string Token, DateTime ExpiresAt, Guid UserId, string FullName, string Role);

public record ChangePasswordRequest(string? CurrentPassword, string? NewPassword);
