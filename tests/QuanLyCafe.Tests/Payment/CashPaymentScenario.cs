using QuanLyCafe.Application.Payment;
using QuanLyCafe.Domain.Catalog;
using QuanLyCafe.Domain.Inventory;
using QuanLyCafe.Domain.Ordering;
using QuanLyCafe.Domain.Payment;
using QuanLyCafe.Domain.Staff;

namespace QuanLyCafe.Tests.Payment;

internal sealed class CashPaymentScenario
{
    public CashPaymentScenario(decimal stockQuantity = 100m)
    {
        Material = new Material { MaterialName = "Milk", Unit = "ml", StockQuantity = stockQuantity };
        Product = new Product { ProductName = "Milk coffee", Price = 35_000m, IsActive = true };
        Product.ProductBOMs.Add(new ProductBOM
        {
            ProductID = Product.ProductID,
            MaterialID = Material.MaterialID,
            QuantityRequired = 30m
        });

        Table = new Table { TableName = "Table 1", Status = TableStatus.Serving };
        Cashier = new User { Username = "cashier-test", FullName = "Test Cashier", PasswordHash = "test", RoleID = 2 };
        Order = new Order
        {
            OrderType = OrderType.DineIn,
            TableID = Table.TableID,
            Table = Table,
            UserID = Cashier.UserID,
            User = Cashier,
            TotalAmount = 70_000m
        };
        Order.OrderDetails.Add(new OrderDetail
        {
            OrderID = Order.OrderID,
            ProductID = Product.ProductID,
            Product = Product,
            Quantity = 2,
            UnitPrice = Product.Price,
            Status = OrderDetailStatus.Delivered
        });
    }

    public Material Material { get; }
    public Product Product { get; }
    public Table Table { get; }
    public User Cashier { get; }
    public Order Order { get; }

    public Task<PaymentResultDto> PayAsync(PaymentService service) => service.PayAsync(
        new PaymentRequest(Order.OrderID, PaymentMethod.Cash, 100_000m),
        Cashier.UserID, Cashier.FullName, CancellationToken.None);

    internal sealed class UnusedGateway : IPaymentGateway
    {
        public Task AuthorizeAsync(Guid orderId, PaymentMethod method, decimal amount, CancellationToken ct) =>
            throw new NotSupportedException("Cash payments do not call the electronic gateway.");
    }
}
