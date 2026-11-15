namespace QuanLyCafe.Desktop.Models;

public record LoginRequest(string Username, string Password);
public record LoginResponse(string Token, string FullName, string Role);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);