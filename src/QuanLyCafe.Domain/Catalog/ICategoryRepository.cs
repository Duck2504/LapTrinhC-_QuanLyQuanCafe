namespace QuanLyCafe.Domain.Catalog;

public interface ICategoryRepository
{
    Task<List<Category>> GetAllAsync(bool onlyActive, CancellationToken ct);
    Task<Category?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<bool> NameExistsAsync(string name, Guid? exceptId, CancellationToken ct);
    void Add(Category category);
    Task SaveChangesAsync(CancellationToken ct);
}
