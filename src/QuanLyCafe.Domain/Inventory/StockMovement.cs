using QuanLyCafe.Domain.Ordering;
using QuanLyCafe.Domain.Staff;

namespace QuanLyCafe.Domain.Inventory;

public class StockMovement
{
    public Guid MovementID { get; set; } = Guid.NewGuid();
    public MovementType MovementType { get; set; }
    public decimal Quantity { get; set; }
    public DateTime MovementDate { get; set; } = DateTime.Now;
    public string? Note { get; set; }

    public Guid MaterialID { get; set; }
    public Material Material { get; set; } = null!;

    public Guid UserID { get; set; }
    public User User { get; set; } = null!;

    public Guid? OrderID { get; set; } // chỉ có khi MovementType = SaleDeduction
    public Order? Order { get; set; }
}
