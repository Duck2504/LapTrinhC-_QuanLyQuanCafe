using Microsoft.Extensions.Logging;
using QuanLyCafe.Domain.Common;
using QuanLyCafe.Domain.Staff;

namespace QuanLyCafe.Application.Staff;

// US-SEC-03: tạo tài khoản, gán vai trò, vô hiệu hóa, cấp lại mật khẩu (chỉ Manager).
public class StaffService(IUserRepository users, ILogger<StaffService> logger)
{
    public async Task<List<UserDto>> GetUsersAsync(CancellationToken ct = default) =>
        (await users.GetAllAsync(ct)).Select(ToDto).ToList();

    public async Task<List<RoleDto>> GetRolesAsync(CancellationToken ct = default) =>
        (await users.GetRolesAsync(ct)).Select(r => new RoleDto(r.RoleID, r.RoleName)).ToList();

    public async Task<UserDto> CreateAsync(CreateUserRequest req, Guid managerId, CancellationToken ct = default)
    {
        var username = req.Username?.Trim();
        if (string.IsNullOrEmpty(username) || username.Length > 50)
            throw new ValidationException(ErrorCodes.ValidationFailed, "Tên đăng nhập bắt buộc, tối đa 50 ký tự.");
        CheckPassword(req.Password);
        var fullName = CheckFullName(req.FullName);
        var role = await GetRoleAsync(req.RoleId, ct);
        if (await users.GetByUsernameAsync(username, ct) is not null)
            throw new ValidationException(ErrorCodes.ValidationFailed, "Tên đăng nhập đã tồn tại.");

        var user = new User { Username = username, FullName = fullName, RoleID = role.RoleID, Role = role };
        user.PasswordHash = AuthService.Hasher.HashPassword(user, req.Password!);
        users.Add(user);
        await users.SaveChangesAsync(ct);
        logger.LogInformation("Tạo tài khoản {UserId} ({Role}) bởi {ManagerId}", user.UserID, role.RoleName, managerId);
        return ToDto(user);
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest req, Guid managerId, CancellationToken ct = default)
    {
        var user = await GetUserAsync(id, ct);
        var fullName = CheckFullName(req.FullName);
        var role = await GetRoleAsync(req.RoleId, ct);
        if (id == managerId && role.RoleID != user.RoleID)
            throw new BusinessRuleException(ErrorCodes.ValidationFailed, "Không thể tự đổi vai trò của chính mình.");

        user.FullName = fullName;
        user.RoleID = role.RoleID;
        user.Role = role;
        await users.SaveChangesAsync(ct);
        logger.LogInformation("Sửa tài khoản {UserId} ({Role}) bởi {ManagerId}", user.UserID, role.RoleName, managerId);
        return ToDto(user);
    }

    public async Task DeactivateAsync(Guid id, Guid managerId, CancellationToken ct = default)
    {
        if (id == managerId)
            throw new BusinessRuleException(ErrorCodes.ValidationFailed, "Không thể tự vô hiệu hóa tài khoản đang đăng nhập.");
        var user = await GetUserAsync(id, ct);
        user.IsActive = false;
        await users.SaveChangesAsync(ct);
        logger.LogInformation("Vô hiệu hóa tài khoản {UserId} bởi {ManagerId}", user.UserID, managerId);
    }

    public async Task ResetPasswordAsync(Guid id, ResetPasswordRequest req, Guid managerId, CancellationToken ct = default)
    {
        var user = await GetUserAsync(id, ct);
        CheckPassword(req.NewPassword);
        user.PasswordHash = AuthService.Hasher.HashPassword(user, req.NewPassword!);
        await users.SaveChangesAsync(ct);
        logger.LogInformation("Cấp lại mật khẩu {UserId} bởi {ManagerId}", user.UserID, managerId);
    }

    private async Task<User> GetUserAsync(Guid id, CancellationToken ct) =>
        await users.GetByIdAsync(id, ct) ?? throw new NotFoundException("Không tìm thấy tài khoản.");

    private async Task<Role> GetRoleAsync(int roleId, CancellationToken ct) =>
        (await users.GetRolesAsync(ct)).FirstOrDefault(r => r.RoleID == roleId)
        ?? throw new ValidationException(ErrorCodes.ValidationFailed, "Vai trò không hợp lệ.");

    private static string CheckFullName(string? fullName)
    {
        var name = fullName?.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 100)
            throw new ValidationException(ErrorCodes.ValidationFailed, "Họ tên bắt buộc, tối đa 100 ký tự.");
        return name;
    }

    private static void CheckPassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < AuthService.MinPasswordLength)
            throw new ValidationException(ErrorCodes.ValidationFailed, $"Mật khẩu phải có ít nhất {AuthService.MinPasswordLength} ký tự.");
    }

    private static UserDto ToDto(User u) => new(u.UserID, u.Username, u.FullName, u.RoleID, u.Role.RoleName, u.IsActive);
}
