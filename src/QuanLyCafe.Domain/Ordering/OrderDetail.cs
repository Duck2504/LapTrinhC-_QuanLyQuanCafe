using QuanLyCafe.Domain.Catalog;

namespace QuanLyCafe.Domain.Ordering;

public class OrderDetail
{
    public Guid OrderDetailID { get; set; } = Guid.NewGuid();
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public OrderDetailStatus Status { get; set; } = OrderDetailStatus.Pending;
    public string? Note { get; set; }

    public Guid OrderID { get; set; }
    public Order Order { get; set; } = null!;

    public Guid ProductID { get; set; }
    public Product Product { get; set; } = null!;
}
