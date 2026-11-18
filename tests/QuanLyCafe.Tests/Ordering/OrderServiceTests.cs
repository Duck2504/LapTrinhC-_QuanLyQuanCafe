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
    private readonly Table _ban2 = new() { TableName = "Bàn 2" };
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

    // Bàn 1 có 2 món Cà phê sữa (đã gửi Bar), trả về Id 2 dòng.
    private async Task<(Guid OrderId, Guid First, Guid Second)> Ban1CoHaiMon()
    {
        var order = await MoBan1();
        var items = (await _service.UpdateItemsAsync(order.Id, new([new(_cafeSua.ProductID, 1, null), new(_cafeSua.ProductID, 2, null)]))).Items;
        return (order.Id, items[0].Id, items[1].Id);
    }

    [Fact]
    public async Task Phieu_pha_che_chi_gom_mon_chua_giao_cua_order_dang_mo_theo_thu_tu_order()
    {
        var (ban1, item1, _) = await Ban1CoHaiMon();
        _repo.Orders[0].OrderDate = DateTime.Now.AddMinutes(-5);
        _repo.Orders[0].OrderDetails[0].Status = OrderDetailStatus.Delivered;
        var mangDi = await _service.CreateAsync(new CreateOrderRequest(OrderType.Takeaway, null), _waiterId);
        await _service.UpdateItemsAsync(mangDi.Id, new([new(_cafeSua.ProductID, 1, null)]));
        var daTra = await _service.CreateAsync(new CreateOrderRequest(OrderType.Takeaway, null), _waiterId);
        await _service.UpdateItemsAsync(daTra.Id, new([new(_cafeSua.ProductID, 1, null)]));
        _repo.Orders[2].Status = OrderStatus.Paid;

        var tickets = await _service.GetBarTicketsAsync();

        Assert.Equal([ban1, mangDi.Id], tickets.Select(t => t.OrderId));
        Assert.DoesNotContain(tickets[0].Items, i => i.Id == item1);
        Assert.Null(tickets[1].TableName);
    }

    [Fact]
    public async Task Pha_che_lam_xong_phuc_vu_giao_moi_buoc_phat_ItemStatusChanged()
    {
        var (orderId, item, _) = await Ban1CoHaiMon();

        await _service.UpdateItemStatusAsync(item, new(OrderDetailStatus.Preparing), byWaiter: false);
        await _service.UpdateItemStatusAsync(item, new(OrderDetailStatus.Done), byWaiter: false);
        var dto = await _service.UpdateItemStatusAsync(item, new(OrderDetailStatus.Delivered), byWaiter: true);

        Assert.Equal(OrderDetailStatus.Delivered, dto.Status);
        Assert.Equal([OrderDetailStatus.Preparing, OrderDetailStatus.Done, OrderDetailStatus.Delivered], _notifier.Changes.Select(c => c.Status));
        Assert.All(_notifier.Changes, c => Assert.Equal((item, orderId), (c.OrderDetailId, c.OrderId)));
        Assert.Equal(1 + 3, _repo.LockedOrders.Count(id => id == orderId)); // PUT items + 3 lần đổi trạng thái đều khóa order
    }

    [Fact]
    public async Task Doi_trang_thai_sai_vai_tro_hoac_nhay_buoc_bi_chan()
    {
        var (_, item, _) = await Ban1CoHaiMon();

        await Assert.ThrowsAsync<ValidationException>(() => _service.UpdateItemStatusAsync(item, new(OrderDetailStatus.Delivered), byWaiter: false));
        await Assert.ThrowsAsync<ValidationException>(() => _service.UpdateItemStatusAsync(item, new(OrderDetailStatus.Done), byWaiter: true));
        await Assert.ThrowsAsync<ValidationException>(() => _service.UpdateItemStatusAsync(item, new(OrderDetailStatus.Pending), byWaiter: false));
        // Chờ → Xong (bỏ qua Đang làm), hoặc hai pha chế cùng bấm Đang làm → người sau ERR-CON-001.
        var ex = await Assert.ThrowsAsync<ConflictException>(() => _service.UpdateItemStatusAsync(item, new(OrderDetailStatus.Done), byWaiter: false));
        Assert.Equal(ErrorCodes.ConcurrencyConflict, ex.Code);
        await _service.UpdateItemStatusAsync(item, new(OrderDetailStatus.Preparing), byWaiter: false);
        await Assert.ThrowsAsync<ConflictException>(() => _service.UpdateItemStatusAsync(item, new(OrderDetailStatus.Preparing), byWaiter: false));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateItemStatusAsync(Guid.NewGuid(), new(OrderDetailStatus.Preparing), byWaiter: false));

        Assert.Equal(OrderDetailStatus.Preparing, _repo.Orders[0].OrderDetails[0].Status);
        Assert.Single(_notifier.Changes);
    }

    [Fact]
    public async Task Doi_trang_thai_mon_cua_order_da_thanh_toan_ERR_ORDER_001()
    {
        var (_, item, _) = await Ban1CoHaiMon();
        _repo.Orders[0].Status = OrderStatus.Paid;

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(() => _service.UpdateItemStatusAsync(item, new(OrderDetailStatus.Preparing), byWaiter: false));
        Assert.Equal(ErrorCodes.OrderAlreadyPaid, ex.Code);
        Assert.Empty(_notifier.Changes);
    }

    [Fact]
    public async Task Bao_het_nguyen_lieu_phat_ItemUnavailable_khong_doi_du_lieu()
    {
        var (orderId, item1, item2) = await Ban1CoHaiMon();

        await _service.ReportUnavailableAsync(item1);

        var sent = Assert.Single(_notifier.Unavailable);
        Assert.Equal((_cafeSua.ProductID, "Cà phê sữa", orderId, "Bàn 1"), (sent.ProductId, sent.ProductName, sent.OrderId, sent.TableName));
        Assert.All(_repo.Orders[0].OrderDetails, d => Assert.Equal(OrderDetailStatus.Pending, d.Status));

        await _service.UpdateItemStatusAsync(item2, new(OrderDetailStatus.Preparing), byWaiter: false);
        var ex = await Assert.ThrowsAsync<ConflictException>(() => _service.ReportUnavailableAsync(item2)); // đã bắt đầu làm
        Assert.Equal(ErrorCodes.ConcurrencyConflict, ex.Code);
        await Assert.ThrowsAsync<NotFoundException>(() => _service.ReportUnavailableAsync(Guid.NewGuid()));
        Assert.Single(_notifier.Unavailable);
    }

    // Bàn 1: 1 + 2 Cà phê sữa (dòng đầu đang làm); Bàn 2: 1 Cà phê sữa.
    private async Task<(OrderDto Ban1, OrderDto Ban2)> HaiBanCoOrder()
    {
        _repo.Tables.Add(_ban2);
        var (ban1, first, _) = await Ban1CoHaiMon();
        _repo.Orders[0].OrderDetails.Single(d => d.OrderDetailID == first).Status = OrderDetailStatus.Preparing;
        var ban2 = await _service.CreateAsync(new CreateOrderRequest(OrderType.DineIn, _ban2.TableID), _waiterId);
        await _service.UpdateItemsAsync(ban2.Id, new([new(_cafeSua.ProductID, 1, null)]));
        return (await _service.GetAsync(ban1), await _service.GetAsync(ban2.Id));
    }

    [Fact]
    public async Task Gop_ban_chuyen_het_mon_giu_id_va_trang_thai_huy_order_nguon_tra_ban_trong()
    {
        var (ban1, ban2) = await HaiBanCoOrder();

        var dto = await _service.MergeTablesAsync(new(_ban1.TableID, _ban2.TableID));

        Assert.Equal(ban2.Id, dto.Id);
        Assert.Equal(ban1.Items.Concat(ban2.Items).Select(i => (i.Id, i.Status, i.Quantity)).OrderBy(x => x.Id),
            dto.Items.Select(i => (i.Id, i.Status, i.Quantity)).OrderBy(x => x.Id));
        Assert.Equal(4 * 25000, dto.TotalAmount);
        var nguon = _repo.Orders.Single(o => o.OrderID == ban1.Id);
        Assert.Equal((OrderStatus.Cancelled, 0m, 0), (nguon.Status, nguon.TotalAmount, nguon.OrderDetails.Count));
        Assert.Equal((TableStatus.Empty, TableStatus.Serving), (_ban1.Status, _ban2.Status));
        Assert.Equal(ban2.Id, Assert.Single(await _service.GetTablesAsync(), t => t.OpenOrderId is not null).OpenOrderId);
        Assert.Contains(ban1.Id, _repo.LockedOrders.TakeLast(2));
        Assert.Contains(ban2.Id, _repo.LockedOrders.TakeLast(2));
        Assert.Equal(ban2.Id, _notifier.Tickets.Last().OrderId); // Bar tải lại để thấy món đổi bàn
    }

    [Fact]
    public async Task Gop_ban_sai_bi_chan()
    {
        _repo.Tables.Add(_ban2);
        await Assert.ThrowsAsync<ValidationException>(() => _service.MergeTablesAsync(new(_ban1.TableID, _ban2.TableID))); // bàn nguồn chưa có order
        await MoBan1();
        await Assert.ThrowsAsync<ValidationException>(() => _service.MergeTablesAsync(new(_ban1.TableID, _ban2.TableID))); // bàn đích trống
        await Assert.ThrowsAsync<ValidationException>(() => _service.MergeTablesAsync(new(_ban1.TableID, _ban1.TableID)));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.MergeTablesAsync(new(_ban1.TableID, Guid.NewGuid())));

        var ban2 = await _service.CreateAsync(new CreateOrderRequest(OrderType.DineIn, _ban2.TableID), _waiterId);
        _repo.Orders.Single(o => o.OrderID == ban2.Id).Status = OrderStatus.Paid; // order bàn đích đã thanh toán
        await Assert.ThrowsAsync<ValidationException>(() => _service.MergeTablesAsync(new(_ban1.TableID, _ban2.TableID)));
        Assert.Equal(OrderStatus.Open, _repo.Orders[0].Status);
    }

    [Fact]
    public async Task Tach_mot_phan_sang_ban_trong_mo_order_moi_roi_tach_nguyen_dong_sang_ban_do()
    {
        _repo.Tables.Add(_ban2);
        var (ban1, first, second) = await Ban1CoHaiMon();
        _repo.Orders[0].OrderDetails.Single(d => d.OrderDetailID == second).Status = OrderDetailStatus.Done;

        var moi = await _service.SplitTableAsync(new(_ban1.TableID, _ban2.TableID, [new(second, 1)]), _waiterId);

        Assert.NotEqual(ban1, moi.Id);
        Assert.Equal(("Bàn 2", TableStatus.Serving), (moi.TableName, _ban2.Status));
        var tach = Assert.Single(moi.Items);
        Assert.Equal((1, 25000m, OrderDetailStatus.Done), (tach.Quantity, tach.UnitPrice, tach.Status));
        var nguon = await _service.GetAsync(ban1);
        Assert.Equal((2, 2 * 25000m), (nguon.Items.Count, nguon.TotalAmount));
        Assert.Equal(1, nguon.Items.Single(i => i.Id == second).Quantity);

        var dto = await _service.SplitTableAsync(new(_ban1.TableID, _ban2.TableID, [new(first, 1)]), _waiterId);

        Assert.Equal(moi.Id, dto.Id); // bàn đích đã có order → thêm vào order đó
        Assert.Contains(dto.Items, i => i.Id == first); // tách nguyên dòng giữ ID
        Assert.Equal(2 * 25000, dto.TotalAmount);
        Assert.Equal(25000, (await _service.GetAsync(ban1)).TotalAmount);
        Assert.Equal(2, _notifier.Tickets.Count);
    }

    [Fact]
    public async Task Tach_het_mon_qua_so_luong_hoac_sai_dong_bi_chan()
    {
        _repo.Tables.Add(_ban2);
        var (_, first, second) = await Ban1CoHaiMon();

        Task Tach(List<SplitItemRequest> items) =>
            _service.SplitTableAsync(new(_ban1.TableID, _ban2.TableID, items), _waiterId);
        await Assert.ThrowsAsync<ValidationException>(() => Tach([new(first, 1), new(second, 2)])); // tách hết
        await Assert.ThrowsAsync<ValidationException>(() => Tach([new(second, 3)]));
        await Assert.ThrowsAsync<ValidationException>(() => Tach([new(second, 0)]));
        await Assert.ThrowsAsync<ValidationException>(() => Tach([new(second, 1), new(second, 1)]));
        await Assert.ThrowsAsync<ValidationException>(() => Tach([new(Guid.NewGuid(), 1)]));
        await Assert.ThrowsAsync<ValidationException>(() => Tach([]));
        await Assert.ThrowsAsync<ValidationException>(() => _service.SplitTableAsync(new(_ban2.TableID, _ban1.TableID, [new(first, 1)]), _waiterId));

        Assert.Single(_repo.Orders);
        Assert.Equal([1, 2], _repo.Orders[0].OrderDetails.Select(d => d.Quantity));
        Assert.Equal(TableStatus.Empty, _ban2.Status);
    }

    [Fact]
    public async Task Doi_trang_thai_mon_gui_kem_ten_mon_va_ban()
    {
        var (_, item, _) = await Ban1CoHaiMon();

        await _service.UpdateItemStatusAsync(item, new(OrderDetailStatus.Preparing), byWaiter: false);

        var c = Assert.Single(_notifier.Changes);
        Assert.Equal(("Cà phê sữa", "Bàn 1"), (c.ProductName, c.TableName));
    }

    private class FakeInventory : IInventoryService
    {
        public List<StockCheckItem> Checked { get; } = [];
        public Exception? Error { get; set; }

        public Task CheckStockAsync(IEnumerable<StockCheckItem> items, CancellationToken ct)
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

        public List<ItemStatusChangedDto> Changes { get; } = [];
        public List<ItemUnavailableDto> Unavailable { get; } = [];

        public Task ItemStatusChangedAsync(ItemStatusChangedDto change, CancellationToken ct = default)
        {
            Changes.Add(change);
            return Task.CompletedTask;
        }

        public Task ItemUnavailableAsync(ItemUnavailableDto item, CancellationToken ct = default)
        {
            Unavailable.Add(item);
            return Task.CompletedTask;
        }

        public Task LowStockAlertAsync(LowStockAlertDto alert, CancellationToken ct = default) => Task.CompletedTask;
    }

    private class FakeOrders(List<Table> tables, List<Product> products) : IOrderRepository
    {
        public List<Order> Orders { get; } = [];
        public List<Table> Tables => tables;

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

        public Task<List<Order>> GetOpenOrdersAsync(CancellationToken ct = default) =>
            Task.FromResult(Orders.Where(o => o.Status == OrderStatus.Open).ToList());

        public Task<Guid?> GetOrderIdOfDetailAsync(Guid orderDetailId, CancellationToken ct = default) =>
            Task.FromResult(Orders.SelectMany(o => o.OrderDetails).FirstOrDefault(d => d.OrderDetailID == orderDetailId)?.OrderID);

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

        public Task<T> InOrdersLockAsync<T>(IEnumerable<Guid> orderIds, Func<Task<T>> work, CancellationToken ct = default)
        {
            LockedOrders.AddRange(orderIds);
            return work();
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
