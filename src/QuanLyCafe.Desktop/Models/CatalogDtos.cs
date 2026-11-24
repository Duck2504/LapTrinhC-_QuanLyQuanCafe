namespace QuanLyCafe.Desktop.Models;

// GET /api/products
public record ProductDto(Guid ProductId, string ProductName, decimal Price, bool IsActive,
    Guid? CategoryId = null, string? CategoryName = null, string? Description = null, string? ImageUrl = null);

// POST, PUT /api/products
public record SaveProductRequest(string ProductName, decimal Price, bool IsActive,
    Guid? CategoryId = null, string? Description = null, string? ImageUrl = null);

// GET /api/categories (cả danh mục đã ngừng)
public record CategoryDto(Guid CategoryId, string CategoryName, bool IsActive);

// POST, PUT /api/categories — "xoá" = IsActive=false
public record SaveCategoryRequest(string CategoryName, bool IsActive);

// GET /api/menu (chỉ sản phẩm đang bán)
public record MenuItemDto(Guid ProductId, string ProductName, decimal Price);

// GET /api/products/{id}/bom
public record BomLineDto(Guid MaterialId, string MaterialName, string Unit, decimal QuantityRequired);

// PUT /api/products/{id}/bom
public record BomLineRequest(Guid MaterialId, decimal QuantityRequired);
public record SaveBomRequest(List<BomLineRequest> Lines);

public record CreateProductRequest(string ProductName, decimal Price);
public record UpdateProductRequest(string ProductName, decimal Price, bool IsActive);