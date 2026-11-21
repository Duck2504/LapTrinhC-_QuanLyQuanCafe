using Microsoft.EntityFrameworkCore;
using QuanLyCafe.Domain.Catalog;
using QuanLyCafe.Infrastructure.Persistence;

namespace QuanLyCafe.Infrastructure.Repositories.Catalog;

public class CategoryRepository(AppDbContext db) : ICategoryRepository
{
    public Task<List<Category>> GetAllAsync(bool onlyActive, CancellationToken ct) =>
        db.Categories.AsNoTracking()
            .Where(c => !onlyActive || c.IsActive)
            .OrderBy(c => c.CategoryName)
            .ToListAsync(ct);

    public Task<Category?> GetByIdAsync(Guid id, CancellationToken ct) =>
        db.Categories.FirstOrDefaultAsync(c => c.CategoryID == id, ct);

    // Collation mặc định của SQL Server không phân biệt hoa thường.
    public Task<bool> NameExistsAsync(string name, Guid? exceptId, CancellationToken ct) =>
        db.Categories.AnyAsync(c => c.CategoryName == name && c.CategoryID != exceptId, ct);

    public void Add(Category category) => db.Categories.Add(category);

    public async Task SaveChangesAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        // Hai Manager tạo cùng tên một lúc: lọt qua NameExistsAsync, unique index chặn → 400 thay vì 500.
        catch (DbUpdateException ex) when (ex.InnerException is Microsoft.Data.SqlClient.SqlException { Number: 2601 or 2627 })
        {
            throw new QuanLyCafe.Domain.Common.ValidationException(QuanLyCafe.Domain.Common.ErrorCodes.ValidationFailed, "Tên danh mục đã tồn tại.");
        }
    }
}
