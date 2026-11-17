public record ProductDto(Guid ProductId, string ProductName, decimal Price, bool IsActive);
public record SaveProductRequest(string ProductName, decimal Price, bool IsActive = true);
public record MenuItemDto(Guid ProductId, string ProductName, decimal Price);