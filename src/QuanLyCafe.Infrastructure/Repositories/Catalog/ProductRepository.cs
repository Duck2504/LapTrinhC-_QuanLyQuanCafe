
using Microsoft.EntityFrameworkCore;
using QuanLyCafe.Domain.Catalog;
using QuanLyCafe.Infrastructure.Persistence;

public class ProductRepository(AppDbContext db) : IProductRepository
{
    public Task<List<Product>> GetAllAsync(bool onlyActive, CancellationToken ct) =>
        db.Products.AsNoTracking()
            .Where(p => !onlyActive || p.IsActive)
            .OrderBy(p => p.ProductName)
            .ToListAsync(ct);

    public Task<Product?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Products.FirstOrDefaultAsync(p => p.ProductID == id, ct);

    public async Task AddAsync(Product p, CancellationToken ct) =>
        await db.Products.AddAsync(p, ct);

    public Task SaveChangesAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
    public Task<Product?> GetWithBomAsync(Guid id, CancellationToken ct) =>
        db.Products
            .Include(p => p.ProductBOMs)
            .FirstOrDefaultAsync(p => p.ProductID == id, ct);

    public void RemoveBomLines(IEnumerable<ProductBOM> lines) =>
        db.ProductBOMs.RemoveRange(lines);
}