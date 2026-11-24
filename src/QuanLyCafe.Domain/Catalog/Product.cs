namespace QuanLyCafe.Domain.Catalog;

public class Product
{
    public Guid ProductID { get; set; } = Guid.NewGuid();
    public string ProductName { get; set; } = "";
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
    public string? Description { get; set; }
    public string? ImageUrl { get; set; }

    public Guid? CategoryID { get; set; }
    public Category? Category { get; set; }

    public List<ProductBOM> ProductBOMs { get; set; } = [];
}
