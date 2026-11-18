using System.ComponentModel;
using QuanLyCafe.Desktop.Models;
using QuanLyCafe.Desktop.Services;

namespace QuanLyCafe.Desktop.Forms.Ordering;

// SCR_CAFE_004 — Sơ đồ bàn / Gọi món: sơ đồ bàn, thực đơn và order trên một form (Manager, Cashier, Waiter).
public class frmPOS : Form
{
    // Một dòng món trên form. Chỉ món Chờ được sửa/xóa (tới khi Bar bấm Đang làm); Id null = món mới chưa gửi.
    private class Line
    {
        public Guid? Id { get; init; }
        public Guid ProductId { get; init; }
        public string ProductName { get; init; } = "";
        public decimal UnitPrice { get; init; }
        public int Quantity { get; set; } = 1;
        public string? Note { get; set; }
        public OrderDetailStatus Status { get; init; }
        public string StatusText => Status switch
        {
            OrderDetailStatus.Pending => "Chờ",
            OrderDetailStatus.Preparing => "Đang làm",
            OrderDetailStatus.Done => "Xong",
            _ => "Đã giao",
        };
        public decimal Amount => Quantity * UnitPrice;
    }

    private readonly FlowLayoutPanel pnlTables = new() { Dock = DockStyle.Fill, AutoScroll = true };
    private readonly ListBox lstMenu = new() { Dock = DockStyle.Fill, FormattingEnabled = true };
    private readonly DataGridView grid = new()
    {
        Dock = DockStyle.Fill, AllowUserToAddRows = false, AllowUserToDeleteRows = false, MultiSelect = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoGenerateColumns = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false,
    };
    private readonly Label lblOrder = new() { Dock = DockStyle.Top, Height = 28, Font = new Font("Segoe UI", 11, FontStyle.Bold), Text = "Chọn bàn hoặc Mang đi" };
    private readonly Label lblTotal = new() { AutoSize = true, Font = new Font("Segoe UI", 11, FontStyle.Bold), Padding = new Padding(16, 6, 0, 0) };
    private readonly Button btnTakeaway = new() { Text = "Mang đi", AutoSize = true };
    private readonly Button btnRefresh = new() { Text = "Làm mới", AutoSize = true };
    private readonly Button btnAdd = new() { Text = "Thêm món →", Dock = DockStyle.Bottom, Height = 32 };
    private readonly Button btnRemove = new() { Text = "Xóa món", AutoSize = true };
    private readonly Button btnSubmit = new() { Text = "Gửi pha chế", AutoSize = true };

    private readonly BindingList<Line> _lines = [];
    private OrderDto? _order;
    private bool _dirty; // có thay đổi món chưa lưu

    private Line? Selected => grid.CurrentRow?.DataBoundItem as Line;

    public frmPOS()
    {
        Text = "Sơ đồ bàn / Gọi món";
        Size = new Size(1200, 700);
        StartPosition = FormStartPosition.CenterParent;

        grid.Columns.AddRange(
            new DataGridViewTextBoxColumn { HeaderText = "Món", DataPropertyName = nameof(Line.ProductName), ReadOnly = true, FillWeight = 160 },
            new DataGridViewTextBoxColumn { HeaderText = "SL", DataPropertyName = nameof(Line.Quantity), FillWeight = 40 },
            new DataGridViewTextBoxColumn { HeaderText = "Ghi chú", DataPropertyName = nameof(Line.Note), FillWeight = 160 },
            new DataGridViewTextBoxColumn { HeaderText = "Trạng thái", DataPropertyName = nameof(Line.StatusText), ReadOnly = true, FillWeight = 70 },
            new DataGridViewTextBoxColumn { HeaderText = "Đơn giá", DataPropertyName = nameof(Line.UnitPrice), ReadOnly = true, FillWeight = 70, DefaultCellStyle = { Format = "N0" } },
            new DataGridViewTextBoxColumn { HeaderText = "Thành tiền", DataPropertyName = nameof(Line.Amount), ReadOnly = true, FillWeight = 80, DefaultCellStyle = { Format = "N0" } });
        grid.DataSource = _lines;

        var tablesBox = new GroupBox { Text = "Sơ đồ bàn", Dock = DockStyle.Left, Width = 320 };
        var tableButtons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        tableButtons.Controls.AddRange([btnTakeaway, btnRefresh]);
        tablesBox.Controls.Add(pnlTables);
        tablesBox.Controls.Add(tableButtons);

        var menuBox = new GroupBox { Text = "Thực đơn (nhấp đúp để thêm)", Dock = DockStyle.Left, Width = 280 };
        menuBox.Controls.Add(lstMenu);
        menuBox.Controls.Add(btnAdd);

        var orderBox = new GroupBox { Text = "Order", Dock = DockStyle.Fill };
        var orderButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true };
        orderButtons.Controls.AddRange([btnRemove, btnSubmit, lblTotal]);
        orderBox.Controls.Add(grid);
        orderBox.Controls.Add(lblOrder);
        orderBox.Controls.Add(orderButtons);

        Controls.Add(orderBox);
        Controls.Add(menuBox);
        Controls.Add(tablesBox);

        lstMenu.Format += (_, e) => { if (e.ListItem is MenuItemDto m) e.Value = $"{m.ProductName} — {m.Price:N0}đ"; };
        Load += async (_, _) => await RunAsync(LoadAsync);
        btnRefresh.Click += async (_, _) => await RunAsync(RefreshAsync);
        btnTakeaway.Click += async (_, _) => await RunAsync(OpenTakeawayAsync);
        btnAdd.Click += (_, _) => AddSelectedMenuItem();
        lstMenu.DoubleClick += (_, _) => AddSelectedMenuItem();
        btnRemove.Click += (_, _) => RemoveSelected();
        btnSubmit.Click += async (_, _) => await RunAsync(SubmitAsync);
        grid.CellBeginEdit += (_, e) => e.Cancel = _lines[e.RowIndex].Status != OrderDetailStatus.Pending;
        grid.CellEndEdit += (_, _) => { _dirty = true; UpdateTotal(); };
        grid.DataError += (_, e) =>
        {
            MessageBox.Show(this, "Số lượng phải là số nguyên lớn hơn 0.", Text);
            e.Cancel = true;
        };
        FormClosing += (_, e) => e.Cancel = !ConfirmDiscard();
        ShowOrder(null);
    }

    private async Task LoadAsync()
    {
        lstMenu.DataSource = await Program.Api.GetAsync<List<MenuItemDto>>("api/menu") ?? [];
        await LoadTablesAsync();
    }

    private async Task LoadTablesAsync()
    {
        var tables = await Program.Api.GetAsync<List<TableDto>>("api/tables") ?? [];
        foreach (Control c in pnlTables.Controls.Cast<Control>().ToList()) c.Dispose();
        foreach (var t in tables)
        {
            var b = new Button
            {
                Text = t.Status == TableStatus.Serving ? $"{t.Name}\nĐang phục vụ" : $"{t.Name}\nTrống",
                Size = new Size(92, 64),
                BackColor = t.Status == TableStatus.Serving ? Color.SandyBrown : Color.PaleGreen,
                // Bàn đang mở trên form: viền đậm để biết đang sửa bàn nào.
                FlatStyle = t.OpenOrderId is { } id && id == _order?.Id ? FlatStyle.Flat : FlatStyle.Standard,
            };
            b.FlatAppearance.BorderSize = 3;
            b.Click += async (_, _) => await RunAsync(() => OpenTableAsync(t));
            pnlTables.Controls.Add(b);
        }
    }

    // Làm mới sơ đồ bàn và order đang mở (bar có thể đã đổi trạng thái món).
    private async Task RefreshAsync()
    {
        if (!ConfirmDiscard()) return;
        if (_order is not null) ShowOrder(await Program.Api.GetAsync<OrderDto>($"api/orders/{_order.Id}"));
        await LoadTablesAsync();
    }

    private async Task OpenTableAsync(TableDto t)
    {
        if (!ConfirmDiscard()) return;
        if (t.OpenOrderId is { } id)
            ShowOrder(await Program.Api.GetAsync<OrderDto>($"api/orders/{id}"));
        else
        {
            if (MessageBox.Show(this, $"Mở order cho {t.Name}?", Text, MessageBoxButtons.YesNo) != DialogResult.Yes) return;
            ShowOrder(await Program.Api.PostAsync<OrderDto>("api/orders", new { orderType = OrderType.DineIn, tableId = t.Id }));
        }
        await LoadTablesAsync();
    }

    private async Task OpenTakeawayAsync()
    {
        if (!ConfirmDiscard()) return;
        if (MessageBox.Show(this, "Tạo order mang đi mới?", Text, MessageBoxButtons.YesNo) != DialogResult.Yes) return;
        ShowOrder(await Program.Api.PostAsync<OrderDto>("api/orders", new { orderType = OrderType.TakeAway }));
        await LoadTablesAsync();
    }

    private void ShowOrder(OrderDto? o)
    {
        _order = o;
        _lines.Clear();
        foreach (var i in o?.Items ?? [])
            _lines.Add(new Line { Id = i.Id, ProductId = i.ProductId, ProductName = i.ProductName, UnitPrice = i.UnitPrice, Quantity = i.Quantity, Note = i.Note, Status = i.Status });
        _dirty = false;

        lblOrder.Text = o is null ? "Chọn bàn hoặc Mang đi"
            : $"{o.TableName ?? "Mang đi"} — order lúc {o.OrderDate:HH:mm}" + (o.Status == OrderStatus.Open ? "" : " (đã đóng)");
        var editable = o?.Status == OrderStatus.Open;
        btnAdd.Enabled = btnRemove.Enabled = btnSubmit.Enabled = editable;
        grid.ReadOnly = !editable;
        UpdateTotal();
    }

    private void AddSelectedMenuItem()
    {
        if (_order?.Status != OrderStatus.Open || lstMenu.SelectedItem is not MenuItemDto m) return;
        _lines.Add(new Line { ProductId = m.ProductId, ProductName = m.ProductName, UnitPrice = m.Price, Status = OrderDetailStatus.Pending });
        _dirty = true;
        UpdateTotal();
    }

    private void RemoveSelected()
    {
        if (Selected is not { } l) return;
        if (l.Status != OrderDetailStatus.Pending)
        {
            MessageBox.Show(this, "Chỉ xóa được món đang Chờ (pha chế chưa làm).", Text);
            return;
        }
        _lines.Remove(l);
        _dirty = true;
        UpdateTotal();
    }

    // Không có nút Lưu riêng: món Chờ luôn là món đã gửi Bar. Lưu (PUT, kèm Id dòng cũ) rồi gửi (submit → TicketCreated);
    // thiếu nguyên liệu → ERR-STOCK-001 / ERR-BOM-001, món Bar đã bắt đầu làm → ERR-CON-001.
    private async Task SubmitAsync()
    {
        grid.EndEdit();
        var items = _lines.Where(l => l.Status == OrderDetailStatus.Pending)
            .Select(l => new { id = l.Id, productId = l.ProductId, quantity = l.Quantity, note = l.Note }).ToList();
        ShowOrder(await Program.Api.PutAsync<OrderDto>($"api/orders/{_order!.Id}/items", new { items }));
        ShowOrder(await Program.Api.PostAsync<OrderDto>($"api/orders/{_order!.Id}/submit"));
        MessageBox.Show(this, "Đã gửi order xuống pha chế.", Text);
    }

    private void UpdateTotal() => lblTotal.Text = $"Tổng: {_lines.Sum(l => l.Amount):N0}đ";

    private bool ConfirmDiscard() =>
        !_dirty || MessageBox.Show(this, "Order có món chưa lưu. Bỏ các thay đổi này?", Text,
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;

    // Khóa form khi đang gọi API; lỗi hiện qua ErrorPresenter theo mã ERR-*.
    private async Task RunAsync(Func<Task> action)
    {
        UseWaitCursor = true;
        Enabled = false;
        try { await action(); }
        catch (Exception ex) { ErrorPresenter.Show(this, ex); }
        finally { Enabled = true; UseWaitCursor = false; }
    }
}
