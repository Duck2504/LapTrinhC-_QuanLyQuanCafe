using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using QuanLyCafe.Domain.Staff;

namespace QuanLyCafe.Infrastructure.Persistence;

// 4 role seed bằng HasData (RoleConfiguration). Manager tạo lúc chạy để mật khẩu lấy từ
// user-secrets, không bị ghi cứng vào migration.
public static class DbSeeder
{
    public const int ManagerRoleId = 1;

    /// <returns>false nếu chưa có Manager mà thiếu mật khẩu hoặc username đã bị dùng.</returns>
    public static async Task<bool> SeedManagerAsync(AppDbContext db, string username, string? password)
    {
        if (await db.Users.AnyAsync(u => u.RoleID == ManagerRoleId)) return true;
        if (string.IsNullOrWhiteSpace(password)) return false;
        if (await db.Users.AnyAsync(u => u.Username == username)) return false; // tên đã bị tài khoản khác dùng

        var manager = new User { Username = username, FullName = "Quản lý", RoleID = ManagerRoleId };
        manager.PasswordHash = new PasswordHasher<User>().HashPassword(manager, password); // PBKDF2
        db.Users.Add(manager);
        await db.SaveChangesAsync();
        return true;
    }
}
