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

    public Task<List<User>> GetAllAsync(CancellationToken ct = default) =>
        db.Users.Include(u => u.Role).OrderBy(u => u.Username).ToListAsync(ct);

    public Task<List<Role>> GetRolesAsync(CancellationToken ct = default) =>
        db.Roles.OrderBy(r => r.RoleID).ToListAsync(ct);

    public void Add(User user) => db.Users.Add(user);

    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
