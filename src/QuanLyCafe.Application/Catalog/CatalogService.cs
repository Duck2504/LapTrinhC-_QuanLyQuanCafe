using Microsoft.Extensions.Logging;
using QuanLyCafe.Domain.Catalog;
using QuanLyCafe.Domain.Common;


#pragma warning disable CA1050 // Declare types in namespaces
public class CatalogService(IProductRepository repo, ILogger<CatalogService> log)
#pragma warning restore CA1050 // Declare types in namespaces
{
    public async Task<List<ProductDto>> GetProductsAsync(CancellationToken ct) =>
        (await repo.GetAllAsync(onlyActive: false, ct)).Select(ToDto).ToList();

    public async Task<List<MenuItemDto>> GetMenuAsync(CancellationToken ct) =>
        (await repo.GetAllAsync(onlyActive: true, ct))
            .Select(p => new MenuItemDto(p.ProductID, p.ProductName, p.Price)).ToList();

    public async Task<ProductDto> CreateAsync(SaveProductRequest req, CancellationToken ct)
    {
        Validate(req);
        var p = new Product { ProductID = Guid.NewGuid(), ProductName = req.ProductName.Trim(),
                              Price = req.Price, IsActive = true };
        await repo.AddAsync(p, ct);
        await repo.SaveChangesAsync(ct);
        return ToDto(p);
    }

    public async Task<ProductDto> UpdateAsync(Guid id, SaveProductRequest req, CancellationToken ct)
    {
        Validate(req);
        var p = await repo.GetByIdAsync(id, ct)
                ?? throw new NotFoundException("Không tìm thấy sản phẩm");
        if (p.Price != req.Price)
            log.LogInformation("Đổi giá {Product}: {Old} -> {New}", p.ProductName, p.Price, req.Price);
        p.ProductName = req.ProductName.Trim();
        p.Price = req.Price;
        p.IsActive = req.IsActive;          // "Xoá" trên form = IsActive=false
        await repo.SaveChangesAsync(ct);
        return ToDto(p);
    }
    private static void Validate(SaveProductRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.ProductName))
            throw new ValidationException(
                "PRODUCT_NAME_REQUIRED",
                "Tên sản phẩm không được để trống"
            );

        if (r.Price < 0)
            throw new ValidationException(
                ErrorCodes.PriceNegative,
                "Giá bán phải lớn hơn hoặc bằng 0"
            );
    }
    private static ProductDto ToDto(Product p) => new(p.ProductID, p.ProductName, p.Price, p.IsActive);
}

