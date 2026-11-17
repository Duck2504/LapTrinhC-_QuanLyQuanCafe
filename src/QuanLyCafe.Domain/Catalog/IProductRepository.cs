namespace QuanLyCafe.Domain.Catalog;

public interface IProductRepository
{
    Task<List<Product>> GetAllAsync(bool onlyActive, CancellationToken ct);
    Task<Product?> GetByIdAsync(Guid id, CancellationToken ct);
    Task AddAsync(Product product, CancellationToken ct);
    Task SaveChangesAsync(CancellationToken ct);
    Task<Product?> GetWithBomAsync(Guid id, CancellationToken ct);
    void RemoveBomLines(IEnumerable<ProductBOM> lines);
}