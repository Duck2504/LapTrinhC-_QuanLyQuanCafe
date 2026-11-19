using QuanLyCafe.Application.Inventory;
using QuanLyCafe.Application.Payment;
using QuanLyCafe.Domain.Catalog;
using QuanLyCafe.Domain.Common;
using QuanLyCafe.Domain.Inventory;
using QuanLyCafe.Domain.Ordering;

namespace QuanLyCafe.Tests.Payment;

public class PaymentServiceTests
{
    [Fact]
    public async Task CashPayment_ReturnsChangeAndClosesOrder()
    {
        var scenario = new CashPaymentScenario();
        var repository = new PaymentRepositories(scenario);

        var result = await scenario.PayAsync(repository.CreateService());

        Assert.Equal(30_000m, result.Change);
        Assert.Equal(70_000m, result.Invoice.TotalAmount);
        Assert.Equal(scenario.Cashier.FullName, result.Invoice.CashierName);
        Assert.Equal(OrderStatus.Paid, scenario.Order.Status);
        Assert.Equal(TableStatus.Empty, scenario.Table.Status);
        Assert.Equal(40m, scenario.Material.StockQuantity);
        var movement = Assert.Single(repository.Movements);
        Assert.Equal(MovementType.SaleDeduction, movement.MovementType);
        Assert.Equal(60m, movement.Quantity);
        Assert.Equal(scenario.Order.OrderID, movement.OrderID);
        Assert.Equal(scenario.Cashier.UserID, movement.UserID);
    }

    [Fact]
    public async Task RepeatedCashPayment_ReturnsOriginalInvoiceAndDeductsStockOnlyOnce()
    {
        var scenario = new CashPaymentScenario();
        var repository = new PaymentRepositories(scenario);
        var service = repository.CreateService();

        var first = await scenario.PayAsync(service);
        var second = await scenario.PayAsync(service);

        Assert.Equal(first.Change, second.Change);
        Assert.Equal(first.Invoice, second.Invoice with { Lines = first.Invoice.Lines });
        Assert.Equal<InvoiceLineDto>(first.Invoice.Lines, second.Invoice.Lines);
        Assert.Equal(40m, scenario.Material.StockQuantity);
        Assert.Single(repository.Movements);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task CashPayment_WithInsufficientStock_IsRejectedBeforeSaving()
    {
        var scenario = new CashPaymentScenario(stockQuantity: 50m);
        var repository = new PaymentRepositories(scenario);

        var error = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            scenario.PayAsync(repository.CreateService()));

        Assert.Equal(ErrorCodes.StockInsufficient, error.Code);
        Assert.Equal(OrderStatus.Open, scenario.Order.Status);
        Assert.Equal(TableStatus.Serving, scenario.Table.Status);
        Assert.Equal(50m, scenario.Material.StockQuantity);
        Assert.Empty(repository.Movements);
        Assert.Equal(0, repository.SaveCount);
    }

    private sealed class PaymentRepositories(CashPaymentScenario scenario)
        : IPaymentRepository, IOrderRepository, IProductRepository, IMaterialRepository
    {
        public List<StockMovement> Movements { get; } = [];
        public int SaveCount { get; private set; }

        public PaymentService CreateService() =>
            new(this, new InventoryService(this, this), this, new CashPaymentScenario.UnusedGateway());

        public Task<Order?> GetOrderAsync(Guid orderId, CancellationToken ct) =>
            Task.FromResult(orderId == scenario.Order.OrderID ? scenario.Order : null);

        public Task<Product?> GetWithBomAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(id == scenario.Product.ProductID ? scenario.Product : null);

        public Task<T> RunAsync<T>(Guid orderId, Func<Task<T>> work, CancellationToken ct) => work();

        public Task<Dictionary<Guid, Material>> GetMaterialsAsync(IEnumerable<Guid> ids, CancellationToken ct) =>
            Task.FromResult(new[] { scenario.Material }
                .Where(material => ids.Contains(material.MaterialID))
                .ToDictionary(material => material.MaterialID));

        public Task SaveAsync(IEnumerable<StockMovement> movements, CancellationToken ct)
        {
            ++SaveCount;
            Movements.AddRange(movements);
            return Task.CompletedTask;
        }

        public Task<PaymentPreview?> GetPaymentPreviewAsync(Guid orderId, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<List<Table>> GetTablesAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<Table?> GetTableAsync(Guid tableId, CancellationToken ct) => throw new NotSupportedException();
        public Task<Dictionary<Guid, Guid>> GetOpenOrderIdsByTableAsync(CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<bool> HasOpenOrderAsync(Guid tableId, CancellationToken ct) => throw new NotSupportedException();
        public Task<List<Order>> GetOpenOrdersAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<Guid?> GetOrderIdOfDetailAsync(Guid orderDetailId, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<List<Product>> GetProductsAsync(IEnumerable<Guid> productIds, CancellationToken ct) =>
            throw new NotSupportedException();
        public void Add(Order order) => throw new NotSupportedException();
        public void AddDetails(IEnumerable<OrderDetail> details) => throw new NotSupportedException();
        public void RemoveDetails(IEnumerable<OrderDetail> details) => throw new NotSupportedException();
        public Task<T> InOrderLockAsync<T>(Guid orderId, Func<Task<T>> work, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task<T> InOrdersLockAsync<T>(IEnumerable<Guid> orderIds, Func<Task<T>> work, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<List<Product>> GetAllAsync(bool onlyActive, CancellationToken ct) => throw new NotSupportedException();
        public Task<Product?> GetByIdAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task AddAsync(Product product, CancellationToken ct) => throw new NotSupportedException();
        public void RemoveBomLines(IEnumerable<ProductBOM> lines) => throw new NotSupportedException();
        public Task<List<Material>> GetAllAsync(CancellationToken ct) => throw new NotSupportedException();
        public Task<Material?> GetAsync(Guid id, CancellationToken ct) => throw new NotSupportedException();
        public Task<Dictionary<Guid, Material>> GetByIdsAsync(IEnumerable<Guid> ids, CancellationToken ct) =>
            throw new NotSupportedException();
        public Task AddAsync(Material item, CancellationToken ct) => throw new NotSupportedException();
    }
}
