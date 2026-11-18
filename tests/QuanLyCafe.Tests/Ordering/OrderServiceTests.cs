using QuanLyCafe.Application.Common;
using QuanLyCafe.Application.Inventory;
using QuanLyCafe.Application.Ordering;
using QuanLyCafe.Domain.Catalog;
using QuanLyCafe.Domain.Common;
using QuanLyCafe.Domain.Ordering;

namespace QuanLyCafe.Tests.Ordering;

public class OrderServiceTests
{
    private readonly Table _ban1 = new() { TableName = "Bàn 1" };
    private readonly Product _cafeSua = new() { ProductName = "Cà phê sữa", Price = 25000 };
    private readonly Product _ngungBan = new() { ProductName = "Trà đào", Price = 30000, IsActive = false };
    private readonly FakeOrders _repo;
    private readonly OrderService _service;
    private readonly Guid _waiterId = Guid.NewGuid();

    public OrderServiceTests()
    {
        _repo = new FakeOrders([_ban1], [_cafeSua, _ngungBan]);
        _service = new OrderService(_repo, _inventory, _notifier);
    }

    private readonly FakeInventory _inventory = new();
    private readonly FakeNotifier _notifier = new();

    private Task<OrderDto> MoBan1() => _service.CreateAsync(new CreateOrderRequest(OrderType.DineIn, _ban1.TableID), _waiterId);

    [Fact]
    public async Task Mo_ban_thi_ban_dang_phuc_vu_va_so_do_ban_co_order()
    {
        var order = await MoBan1();

        Assert.Equal(OrderStatus.Open, order.Status);
        Assert.Equal("Bàn 1", order.TableName);
        var ban = Assert.Single(await _service.GetTablesAsync());
        Assert.Equal((TableStatus.Serving, (Guid?)order.Id), (ban.Status, ban.OpenOrderId));
    }

    [Fact]
    public async Task Mo_order_thu_hai_cho_cung_ban_ERR_ORDER_002()
    {
        await MoBan1();

        var ex = await Assert.ThrowsAsync<ConflictException>(MoBan1);
        Assert.Equal(ErrorCodes.OrderTableHasOpenOrder, ex.Code);
        Assert.Single(_repo.Orders);
    }

    [Theory]
    [InlineData(OrderType.DineIn, false)]  // tại bàn mà không chọn bàn
    [InlineData(OrderType.Takeaway, true)] // mang đi mà gắn bàn
    [InlineData((OrderType)9, false)]
    public async Task Tao_order_sai_loai_tra_400(OrderType type, bool withTable)
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.CreateAsync(new CreateOrderRequest(type, withTable ? _ban1.TableID : null), _waiterId));
        Assert.Empty(_repo.Orders);
    }

    [Fact]
    public async Task Goi_mon_chep_gia_tinh_tong_va_giu_mon_dang_lam()
    {
        var order = await MoBan1();
        await _service.UpdateItemsAsync(order.Id, new([new(_cafeSua.ProductID, 1, "ít đá")]));
        _repo.Orders[0].OrderDetails[0].Status = OrderDetailStatus.Preparing; // bar đã bắt đầu làm

        var dto = await _service.UpdateItemsAsync(order.Id, new([new(_cafeSua.ProductID, 2, "  không đường ")]));

        Assert.Equal(75000, dto.TotalAmount);
        Assert.Equal(2, dto.Items.Count);
        Assert.Contains(dto.Items, i => i is { Status: OrderDetailStatus.Preparing, Quantity: 1, Note: "ít đá" });
        Assert.Contains(dto.Items, i => i is { Status: OrderDetailStatus.Pending, Quantity: 2, UnitPrice: 25000, Note: "không đường" });
        Assert.Equal(dto.Items.Count, (await _service.GetAsync(order.Id)).Items.Count);
        Assert.Equal([order.Id, order.Id], _repo.LockedOrders); // mỗi lần sửa món đều khóa order
    }

    [Fact]
    public async Task Sua_order_da_thanh_toan_ERR_ORDER_001()
    {
        var order = await MoBan1();
        _repo.Orders[0].Status = OrderStatus.Paid;

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _service.UpdateItemsAsync(order.Id, new([new(_cafeSua.ProductID, 1, null)])));
        Assert.Equal(ErrorCodes.OrderAlreadyPaid, ex.Code);
    }

    [Fact]
    public async Task Goi_mon_sai_tra_400()
    {
        var id = (await MoBan1()).Id;

        await Assert.ThrowsAsync<ValidationException>(() => _service.UpdateItemsAsync(id, new([new(_cafeSua.ProductID, 0, null)])));
        await Assert.ThrowsAsync<ValidationException>(() => _service.UpdateItemsAsync(id, new([new(_ngungBan.ProductID, 1, null)])));
        await Assert.ThrowsAsync<ValidationException>(() => _service.UpdateItemsAsync(id, new([new(Guid.NewGuid(), 1, null)])));
        await Assert.ThrowsAsync<ValidationException>(() => _service.UpdateItemsAsync(id, new([new(_cafeSua.ProductID, 1, new string('x', 201))])));
        await Assert.ThrowsAsync<ValidationException>(() => _service.UpdateItemsAsync(id, new(null)));
        Assert.Empty(_repo.Orders[0].OrderDetails);
    }

    [Fact]
    public async Task Order_khong_ton_tai_tra_404()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Sua_mon_da_co_giu_nguyen_id_va_gia_da_chep()
    {
        var order = await MoBan1();
        var first = Assert.Single((await _service.UpdateItemsAsync(order.Id, new([new(_cafeSua.ProductID, 1, null)]))).Items);
        _cafeSua.Price = 30000; // đổi giá sau khi đã gọi món

        var dto = await _service.UpdateItemsAsync(order.Id,
            new([new(_cafeSua.ProductID, 3, "ít đá", first.Id), new(_cafeSua.ProductID, 1, null)]));

        var kept = Assert.Single(dto.Items, i => i.Id == first.Id); // phiếu Bar đã gửi không bị đổi ID
        Assert.Equal((3, 25000m, "ít đá"), (kept.Quantity, kept.UnitPrice, kept.Note));
        Assert.Equal(2, dto.Items.Count);
        Assert.Equal(3 * 25000 + 30000, dto.TotalAmount);
    }

    [Fact]
    public async Task Sua_mon_bar_da_bat_dau_lam_ERR_CON_001()
    {
        var order = await MoBan1();
        var line = Assert.Single((await _service.UpdateItemsAsync(order.Id, new([new(_cafeSua.ProductID, 1, null)]))).Items);
        _repo.Orders[0].OrderDetails[0].Status = OrderDetailStatus.Preparing;

        var ex = await Assert.ThrowsAsync<ConflictException>(() =>
            _service.UpdateItemsAsync(order.Id, new([new(_cafeSua.ProductID, 5, null, line.Id)])));
        Assert.Equal(ErrorCodes.ConcurrencyConflict, ex.Code);
        await Assert.ThrowsAsync<ValidationException>(() =>
            _service.UpdateItemsAsync(order.Id, new([new(_cafeSua.ProductID, 1, null, Guid.NewGuid())])));
        Assert.Equal(1, _repo.Orders[0].OrderDetails[0].Quantity);
    }

    [Fact]
    public async Task Gui_order_kiem_kho_mon_dang_cho_roi_phat_TicketCreated()
    {
        var order = await MoBan1();
        await _service.UpdateItemsAsync(order.Id, new([new(_cafeSua.ProductID, 1, "ít đá"), new(_cafeSua.ProductID, 2, null)]));
        _repo.Orders[0].OrderDetails[0].Status = OrderDetailStatus.Preparing; // đã gửi lần trước, bar đang làm

        await _service.SubmitAsync(order.Id);

        Assert.Equal([new StockCheckItem(_cafeSua.ProductID, 2)], _inventory.Checked); // chỉ món còn Chờ, gộp theo sản phẩm
        var ticket = Assert.Single(_notifier.Tickets);
        Assert.Equal((order.Id, "Bàn 1"), (ticket.OrderId, ticket.TableName));
        Assert.Equal(2, Assert.Single(ticket.Items).Quantity);
    }

    [Fact]
    public async Task Gui_order_thieu_nguyen_lieu_khong_phat_phieu()
    {
        var order = await MoBan1();
        await _service.UpdateItemsAsync(order.Id, new([new(_cafeSua.ProductID, 1, null)]));
        _inventory.Error = new BusinessRuleException(ErrorCodes.StockInsufficient, "Hết sữa");

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.SubmitAsync(order.Id));
        Assert.Equal(ErrorCodes.StockInsufficient, ex.Code);
        Assert.Empty(_notifier.Tickets);
    }

    [Fact]
    public async Task Gui_order_rong_hoac_da_thanh_toan_bi_chan()
    {
        var order = await MoBan1();
        await Assert.ThrowsAsync<ValidationException>(() => _service.SubmitAsync(order.Id));

        await _service.UpdateItemsAsync(order.Id, new([new(_cafeSua.ProductID, 1, null)]));
        _repo.Orders[0].Status = OrderStatus.Paid;
        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.SubmitAsync(order.Id));
        Assert.Equal(ErrorCodes.OrderAlreadyPaid, ex.Code);
        Assert.Empty(_notifier.Tickets);
    }

    private class FakeInventory : IInventoryService
    {
        public List<StockCheckItem> Checked { get; } = [];
        public Exception? Error { get; set; }

        public Task CheckStockAsync(IReadOnlyCollection<StockCheckItem> items, CancellationToken ct = default)
        {
            if (Error is not null) throw Error;
            Checked.AddRange(items);
            return Task.CompletedTask;
        }
    }

    private class FakeNotifier : IRealtimeNotifier
    {
        public List<BarTicketDto> Tickets { get; } = [];

        public Task TicketCreatedAsync(BarTicketDto ticket, CancellationToken ct = default)
        {
            Tickets.Add(ticket);
            return Task.CompletedTask;
        }
    }

    private class FakeOrders(List<Table> tables, List<Product> products) : IOrderRepository
    {
        public List<Order> Orders { get; } = [];

        public Task<List<Table>> GetTablesAsync(CancellationToken ct = default) => Task.FromResult(tables);

        public Task<Table?> GetTableAsync(Guid tableId, CancellationToken ct = default) =>
            Task.FromResult(tables.FirstOrDefault(t => t.TableID == tableId));

        public Task<Dictionary<Guid, Guid>> GetOpenOrderIdsByTableAsync(CancellationToken ct = default) =>
            Task.FromResult(Orders.Where(o => o.Status == OrderStatus.Open && o.TableID != null)
                .ToDictionary(o => o.TableID!.Value, o => o.OrderID));

        public Task<bool> HasOpenOrderAsync(Guid tableId, CancellationToken ct = default) =>
            Task.FromResult(Orders.Any(o => o.TableID == tableId && o.Status == OrderStatus.Open));

        public Task<Order?> GetOrderAsync(Guid orderId, CancellationToken ct = default) =>
            Task.FromResult(Orders.FirstOrDefault(o => o.OrderID == orderId));

        public Task<List<Product>> GetProductsAsync(IEnumerable<Guid> productIds, CancellationToken ct = default) =>
            Task.FromResult(products.Where(p => productIds.Contains(p.ProductID)).ToList());

        public void Add(Order order) => Orders.Add(order);

        public void AddDetails(IEnumerable<OrderDetail> details)
        {
            foreach (var d in details) d.Order.OrderDetails.Add(d);
        }

        public void RemoveDetails(IEnumerable<OrderDetail> details)
        {
            foreach (var d in details.ToList()) d.Order.OrderDetails.Remove(d);
        }

        public List<Guid> LockedOrders { get; } = [];

        public Task<T> InOrderLockAsync<T>(Guid orderId, Func<Task<T>> work, CancellationToken ct = default)
        {
            LockedOrders.Add(orderId);
            return work();
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
