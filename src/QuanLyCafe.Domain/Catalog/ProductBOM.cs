using QuanLyCafe.Domain.Inventory;

namespace QuanLyCafe.Domain.Catalog;

// Khóa ghép (ProductID, MaterialID); thuộc aggregate Product.
public class ProductBOM
{
    public Guid ProductID { get; set; }
    public Product Product { get; set; } = null!;

    public Guid MaterialID { get; set; }
    public Material Material { get; set; } = null!;

    public decimal QuantityRequired { get; set; }
}
