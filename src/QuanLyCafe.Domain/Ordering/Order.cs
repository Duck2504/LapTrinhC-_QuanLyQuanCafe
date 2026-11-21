using QuanLyCafe.Domain.Common;
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
    public string? PaymentResultJson { get; set; }

    public Guid? TableID { get; set; }
    public Table? Table { get; set; }

    // NULL = khách tự gọi món qua QR trên Web.
    public Guid? UserID { get; set; }
    public User? User { get; set; }

    public Guid? CashierID { get; set; }
    public User? Cashier { get; set; }

    public List<OrderDetail> OrderDetails { get; set; } = [];

    // Quy tắc: order đã thanh toán (hoặc đã hủy) không sửa được → ERR-ORDER-001.
    public void EnsureEditable()
    {
        if (Status != OrderStatus.Open)
            throw new BusinessRuleException(ErrorCodes.OrderAlreadyPaid, "Order đã thanh toán hoặc đã hủy, không thể sửa.");
    }

    public void MarkPaid(PaymentMethod method, Guid cashierId, DateTime paidAt)
    {
        EnsureEditable();
        Status = OrderStatus.Paid;
        PaymentMethod = method;
        CashierID = cashierId;
        PaidAt = paidAt;
    }
}
