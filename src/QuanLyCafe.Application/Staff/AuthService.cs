using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using QuanLyCafe.Domain.Common;
using QuanLyCafe.Domain.Staff;

namespace QuanLyCafe.Application.Staff;

public class AuthService(IUserRepository users, IJwtTokenGenerator tokens, ILogger<AuthService> logger)
{
    public const int MinPasswordLength = 6;
    internal static readonly PasswordHasher<User> Hasher = new(); // PBKDF2, cùng DbSeeder
    private static readonly User DummyUser = new() { PasswordHash = Hasher.HashPassword(new User(), Guid.NewGuid().ToString()) };

    public async Task<LoginResponse> LoginAsync(LoginRequest req, CancellationToken ct = default)
    {
        var user = string.IsNullOrWhiteSpace(req.Username)
            ? null
            : await users.GetByUsernameAsync(req.Username.Trim(), ct);

        // Kiểm mật khẩu trước IsActive để người đoán mò 
        if (user is null)
            Verify(DummyUser, req.Password);
        if (user is null || !Verify(user, req.Password))
        {
            logger.LogWarning("Đăng nhập thất bại: {UserId}", user?.UserID.ToString() ?? "(không tồn tại)");
            throw new UnauthorizedException(ErrorCodes.AuthInvalidCredentials, "Sai tên đăng nhập hoặc mật khẩu.");
        }
        if (!user.IsActive)
        {
            logger.LogWarning("Tài khoản bị vô hiệu hóa đăng nhập: {UserId}", user.UserID);
            throw new UnauthorizedException(ErrorCodes.AuthAccountLocked, "Tài khoản đã bị vô hiệu hóa.");
        }

        var (token, expiresAt) = tokens.Generate(user);
        logger.LogInformation("Đăng nhập thành công: {UserId} ({Role})", user.UserID, user.Role.RoleName);
        return new LoginResponse(token, expiresAt, user.UserID, user.FullName, user.Role.RoleName);
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest req, CancellationToken ct = default)
    {
        var user = await users.GetByIdAsync(userId, ct) ?? throw new NotFoundException("Không tìm thấy tài khoản.");
        if (!Verify(user, req.CurrentPassword))
            throw new ValidationException(ErrorCodes.AuthInvalidCredentials, "Mật khẩu hiện tại không đúng.");
        if (string.IsNullOrEmpty(req.NewPassword) || req.NewPassword.Length < MinPasswordLength)
            throw new ValidationException(ErrorCodes.ValidationFailed, $"Mật khẩu mới phải có ít nhất {MinPasswordLength} ký tự.");

        user.PasswordHash = Hasher.HashPassword(user, req.NewPassword);
        await users.SaveChangesAsync(ct);
        logger.LogInformation("Đổi mật khẩu: {UserId}", user.UserID);
    }

    private static bool Verify(User user, string? password) =>
        !string.IsNullOrEmpty(password)
        && Hasher.VerifyHashedPassword(user, user.PasswordHash, password) != PasswordVerificationResult.Failed;
}
