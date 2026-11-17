using Microsoft.Extensions.Logging;
using QuanLyCafe.Domain.Catalog;
using QuanLyCafe.Domain.Common;
using QuanLyCafe.Application.Catalog;

#pragma warning disable CA1050 // Declare types in namespaces
public class CatalogService(
    IProductRepository repo,
    IMaterialRepository materials,
    ILogger<CatalogService> log)
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

    public async Task SetBomAsync(
    Guid productId, SetBomRequest req, CancellationToken ct)
    {
        var product = await repo.GetWithBomAsync(productId, ct)
            ?? throw new NotFoundException("Không tìm thấy sản phẩm.");

        if (req.Lines is null)
            throw new ValidationException(
                ErrorCodes.ValidationFailed, "Bạn cần gửi danh sách định lượng.");

        const decimal maxQuantity = 999_999_999_999_999.999m;

        // Kiểm tra từng dòng trước khi cộng các dòng trùng.
        foreach (var line in req.Lines)
        {
            if (line is null || line.MaterialId == Guid.Empty)
                throw new ValidationException(
                    ErrorCodes.ValidationFailed, "Mã nguyên liệu không hợp lệ.");

            var quantity = line.QuantityRequired;

            if (quantity <= 0 || quantity > maxQuantity ||
                decimal.Round(quantity, 3) != quantity)
            {
                throw new ValidationException(
                    ErrorCodes.ValidationFailed,
                    "Định lượng phải lớn hơn 0, trong giới hạn lưu trữ và có tối đa 3 chữ số thập phân.");
            }
        }

        // MaterialId → tổng định lượng trong request.
        var quantities = req.Lines
            .GroupBy(line => line.MaterialId)
            .ToDictionary(group => group.Key, group => group.Sum(line => line.QuantityRequired));

        if (quantities.Values.Any(quantity => quantity > maxQuantity))
            throw new ValidationException(
                ErrorCodes.ValidationFailed, "Tổng định lượng vượt giới hạn lưu trữ.");

        var existingMaterials = await materials.GetByIdsAsync(quantities.Keys, ct);

        foreach (var materialId in quantities.Keys)
        {
            if (!existingMaterials.ContainsKey(materialId))
                throw new NotFoundException($"Không tìm thấy nguyên liệu {materialId}.");
        }

        // Dòng cũ vắng trong request mới thì xóa.
        var currentLines = product.ProductBOMs.ToDictionary(line => line.MaterialID);
        var removedLines = currentLines.Values
            .Where(line => !quantities.ContainsKey(line.MaterialID))
            .ToList();

        repo.RemoveBomLines(removedLines);

        // Cùng MaterialID thì sửa tại chỗ. MaterialID mới thì thêm dòng.
        foreach (var (materialId, quantity) in quantities)
        {
            if (currentLines.TryGetValue(materialId, out var existingLine))
            {
                existingLine.QuantityRequired = quantity;
            }
            else
            {
                product.ProductBOMs.Add(new ProductBOM
                {
                    ProductID = product.ProductID,
                    MaterialID = materialId,
                    QuantityRequired = quantity
                });
            }
        }

        await repo.SaveChangesAsync(ct);
    }

    public async Task<List<BomLineDto>> GetBomAsync(
    Guid productId, CancellationToken ct)
    {
        var product = await repo.GetWithBomAsync(productId, ct)
            ?? throw new NotFoundException("Không tìm thấy sản phẩm.");

        if (product.ProductBOMs.Count == 0)
            return [];

        var materialsById = await materials.GetByIdsAsync(
            product.ProductBOMs.Select(line => line.MaterialID), ct);

        return product.ProductBOMs.Select(line =>
        {
            var material = materialsById[line.MaterialID];

            return new BomLineDto(
                line.MaterialID,
                material.MaterialName,
                material.Unit,
                line.QuantityRequired);
        }).ToList();
    }
}


