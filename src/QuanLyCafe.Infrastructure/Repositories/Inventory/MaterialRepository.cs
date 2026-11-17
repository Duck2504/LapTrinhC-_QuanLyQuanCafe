using Microsoft.EntityFrameworkCore;
using QuanLyCafe.Domain.Inventory;
using QuanLyCafe.Infrastructure.Persistence;

namespace QuanLyCafe.Infrastructure.Repositories.Inventory;

public class MaterialRepository(AppDbContext db) : IMaterialRepository
{
    public Task<List<Material>> GetAllAsync(CancellationToken ct) =>
        db.Materials.AsNoTracking()
            .OrderBy(m => m.MaterialName)
            .ToListAsync(ct);

    public Task<Material?> GetAsync(Guid id, CancellationToken ct) =>
        db.Materials.FirstOrDefaultAsync(m => m.MaterialID == id, ct);

    public Task<Dictionary<Guid, Material>> GetByIdsAsync(
        IEnumerable<Guid> ids, CancellationToken ct)
    {
        var materialIds = ids.ToArray();

        return db.Materials.AsNoTracking()
            .Where(m => materialIds.Contains(m.MaterialID))
            .ToDictionaryAsync(m => m.MaterialID, ct);
    }

    public async Task AddAsync(Material material, CancellationToken ct) =>
        await db.Materials.AddAsync(material, ct);

    public Task SaveChangesAsync(CancellationToken ct) =>
        db.SaveChangesAsync(ct);
}