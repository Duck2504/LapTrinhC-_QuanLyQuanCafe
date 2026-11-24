public record ProductDto(Guid ProductId, string ProductName, decimal Price, bool IsActive,
    Guid? CategoryId = null, string? CategoryName = null, string? Description = null, string? ImageUrl = null);
public record SaveProductRequest(string ProductName, decimal Price, bool IsActive = true,
    Guid? CategoryId = null, string? Description = null, string? ImageUrl = null);
public record MenuItemDto(Guid ProductId, string ProductName, decimal Price,
    Guid? CategoryId = null, string? CategoryName = null, string? Description = null, string? ImageUrl = null);

public record CategoryDto(Guid CategoryId, string CategoryName, bool IsActive);
public record SaveCategoryRequest(string CategoryName, bool IsActive = true);
