using Microsoft.EntityFrameworkCore;
using QuanLyCafe.Domain.Staff;
using QuanLyCafe.Infrastructure.Persistence;

namespace QuanLyCafe.Infrastructure.Repositories.Staff;

public class UserRepository(AppDbContext db) : IUserRepository
{
    public Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default) =>
        db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.Username == username, ct);

    public Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default) =>
        db.Users.Include(u => u.Role).FirstOrDefaultAsync(u => u.UserID == userId, ct);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
