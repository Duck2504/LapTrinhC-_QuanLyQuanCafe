using QuanLyCafe.Domain.Payment;
using QuanLyCafe.Domain.Staff;

namespace QuanLyCafe.Domain.Ordering;

public class Order
{
    public Guid OrderID { get; set; } = Guid.NewGuid();
    public DateTime OrderDate { get; set; } = DateTime.Now;
    public OrderType OrderType { get; set; }
    public OrderStatus Status { get; set; } = OrderStatus.Open;
    public decimal TotalAmount { get; set; }
    public PaymentMethod? PaymentMethod { get; set; }
    public DateTime? PaidAt { get; set; }

    public Guid? TableID { get; set; }
    public Table? Table { get; set; }

    public Guid UserID { get; set; }
    public User User { get; set; } = null!;

    public Guid? CashierID { get; set; }
    public User? Cashier { get; set; }

    public List<OrderDetail> OrderDetails { get; set; } = [];
}
