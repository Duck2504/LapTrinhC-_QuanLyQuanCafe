
using QuanLyCafe.Domain.Inventory;

public interface IMaterialRepository
{
    Task<List<Material>> GetAllAsync(CancellationToken ct);
    Task<Material?> GetAsync(Guid id, CancellationToken ct);
    Task<Dictionary<Guid, Material>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct);
    Task AddAsync(Material m, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
}