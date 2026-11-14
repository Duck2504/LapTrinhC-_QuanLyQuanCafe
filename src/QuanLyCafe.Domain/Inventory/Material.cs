namespace QuanLyCafe.Domain.Inventory;

public class Material
{
    public Guid MaterialID { get; set; } = Guid.NewGuid();
    public string MaterialName { get; set; } = "";
    public string Unit { get; set; } = "";
    public decimal StockQuantity { get; set; } // concurrency token (cấu hình ở Infrastructure)
    public decimal MinQuantity { get; set; }
}
