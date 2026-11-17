namespace QuanLyCafe.Domain.Staff;

public interface IUserRepository
{
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);

    Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default);

    Task<List<User>> GetAllAsync(CancellationToken ct = default);

    Task<List<Role>> GetRolesAsync(CancellationToken ct = default);

    void Add(User user);

    Task SaveChangesAsync(CancellationToken ct = default);
}
