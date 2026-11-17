namespace QuanLyCafe.Desktop.Models;

// GET /api/products
public record ProductDto(Guid ProductId, string ProductName, decimal Price, bool IsActive);

// POST, PUT /api/products
public record SaveProductRequest(string ProductName, decimal Price, bool IsActive);

// GET /api/menu (chỉ sản phẩm đang bán)
public record MenuItemDto(Guid ProductId, string ProductName, decimal Price);

// GET /api/products/{id}/bom
public record BomLineDto(Guid MaterialId, string MaterialName, string Unit, decimal QuantityRequired);

// PUT /api/products/{id}/bom
public record BomLineRequest(Guid MaterialId, decimal QuantityRequired);
public record SaveBomRequest(List<BomLineRequest> Lines);

public record CreateProductRequest(string ProductName, decimal Price);
public record UpdateProductRequest(string ProductName, decimal Price, bool IsActive);