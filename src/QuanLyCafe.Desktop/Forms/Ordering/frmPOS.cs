using System.ComponentModel;
using QuanLyCafe.Desktop.Models;
using QuanLyCafe.Desktop.Forms.Payment;
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
        public string StatusText => StatusName(Status);
        public decimal Amount => Quantity * UnitPrice;
    }

    private readonly FlowLayoutPanel pnlTables = new() { Dock = DockStyle.Fill, AutoScroll = true };
    private readonly FlowLayoutPanel pnlMenu = new() { Dock = DockStyle.Fill, AutoScroll = true };
    private readonly TextBox txtSearch = new() { Dock = DockStyle.Top, PlaceholderText = "Tìm món..." };
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
    private readonly Button btnPlus = new() { Text = "+", Width = 40 };
    private readonly Button btnMinus = new() { Text = "−", Width = 40 };
    private readonly Button btnRemove = new() { Text = "Xóa món", AutoSize = true };
    private readonly Button btnSubmit = new() { Text = "Gửi pha chế", AutoSize = true };
    private readonly Button btnDeliver = new() { Text = "Đã giao", AutoSize = true };
    private readonly Button btnMerge = new() { Text = "Gộp bàn...", AutoSize = true };
    private readonly Button btnSplit = new() { Text = "Tách món...", AutoSize = true };
    private readonly Button btnPay = new() { Text = "Thanh toán", AutoSize = true };
    private readonly Label lblNotice = new() { Dock = DockStyle.Bottom, Height = 30, TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
    private readonly BarHubClient _hub = new(Program.BaseUrl, Program.Session);

    private readonly BindingList<Line> _lines = [];
    private OrderDto? _order;
    private bool _dirty; // có thay đổi món chưa lưu

    private Line? Selected => grid.CurrentRow?.DataBoundItem as Line;

    public frmPOS()
    {
        Text = "Sơ đồ bàn / Gọi món";
        Theme.Apply(this);
        Theme.Shortcuts(this, btnRefresh);
        Size = new Size(1200, 700);
        StartPosition = FormStartPosition.CenterParent;

        grid.Columns.AddRange(
            new DataGridViewTextBoxColumn { HeaderText = "Món", DataPropertyName = nameof(Line.ProductName), ReadOnly = true, FillWeight = 160 },
            new DataGridViewTextBoxColumn { HeaderText = "SL", DataPropertyName = nameof(Line.Quantity), FillWeight = 40 },
            new DataGridViewTextBoxColumn { HeaderText = "Ghi chú", DataPropertyName = nameof(Line.Note), FillWeight = 110 },
            new DataGridViewTextBoxColumn { HeaderText = "Trạng thái", DataPropertyName = nameof(Line.StatusText), ReadOnly = true, FillWeight = 70 },
            new DataGridViewTextBoxColumn { HeaderText = "Đơn giá", DataPropertyName = nameof(Line.UnitPrice), ReadOnly = true, FillWeight = 70, DefaultCellStyle = { Format = "N0" } },
            new DataGridViewTextBoxColumn { HeaderText = "Thành tiền", DataPropertyName = nameof(Line.Amount), ReadOnly = true, FillWeight = 100, DefaultCellStyle = { Format = "N0" } });
        grid.DataSource = _lines;

        var tablesBox = new GroupBox { Text = "Sơ đồ bàn", Dock = DockStyle.Left, Width = 320 };
        var tableButtons = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true };
        tableButtons.Controls.AddRange([btnTakeaway, btnRefresh]);
        tablesBox.Controls.Add(pnlTables);
        tablesBox.Controls.Add(tableButtons);

        var menuBox = new GroupBox { Text = "Thực đơn (bấm để thêm)", Dock = DockStyle.Left, Width = 360 };
        menuBox.Controls.Add(pnlMenu);
        menuBox.Controls.Add(txtSearch);

        var orderBox = new GroupBox { Text = "Order", Dock = DockStyle.Fill };
        var orderButtons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true };
        orderButtons.Controls.AddRange([btnMinus, btnPlus, btnRemove, btnSubmit, btnDeliver, btnMerge, btnSplit, btnPay, lblTotal]);
        orderBox.Controls.Add(grid);
        orderBox.Controls.Add(lblOrder);
        orderBox.Controls.Add(orderButtons);

        Controls.Add(orderBox);
        Controls.Add(menuBox);
        Controls.Add(tablesBox);
        Controls.Add(lblNotice);

        // Tải dữ liệu trước: hub chậm/mất kết nối không được chặn sơ đồ bàn.
        Load += async (_, _) => { await RunAsync(LoadAsync); await ConnectHubAsync(); };
        btnRefresh.Click += async (_, _) => { await RunAsync(RefreshAsync); await ConnectHubAsync(); };
        btnDeliver.Click += async (_, _) => await RunAsync(DeliverAsync);
        btnMerge.Click += async (_, _) => await RunAsync(MergeAsync);
        btnSplit.Click += async (_, _) => await RunAsync(SplitAsync);
        _hub.ItemStatusChanged += OnItemStatusChanged;
        _hub.ItemUnavailable += OnItemUnavailable;
        FormClosed += async (_, _) =>
        {
            _hub.ItemStatusChanged -= OnItemStatusChanged;
            _hub.ItemUnavailable -= OnItemUnavailable;
            await _hub.DisposeAsync();
        };
        btnTakeaway.Click += async (_, _) => await RunAsync(OpenTakeawayAsync);
        btnPay.Visible = Program.Session.Role == "Cashier";
        btnPay.Click += async (_, _) =>
        {
            if (_order is null || !ConfirmDiscard()) return;
            var id = _order.Id;
            using (var f = new frmPayment(Program.Api, id)) f.ShowDialog(this);
            await RunAsync(async () =>
            {
                ShowOrder(await Program.Api.GetAsync<OrderDto>($"api/orders/{id}"));
                await LoadTablesAsync();
            });
        };
        txtSearch.TextChanged += (_, _) => FilterMenu();
        btnPlus.Click += (_, _) => ChangeQuantity(+1);
        btnMinus.Click += (_, _) => ChangeQuantity(-1);
        btnRemove.Click += (_, _) => RemoveSelected();
        btnSubmit.Click += async (_, _) => await RunAsync(SubmitAsync);
        grid.CellBeginEdit += (_, e) => e.Cancel = _lines[e.RowIndex].Status != OrderDetailStatus.Pending;
        grid.CellEndEdit += (_, _) => { _dirty = true; UpdateTotal(); };
        grid.DataError += (_, e) =>
        {
            MessageBox.Show(this, "Số lượng phải là số nguyên lớn hơn 0.", Text);
            e.Cancel = true;
        };
        FormClosing += (_, e) => e.Cancel = Program.Session.IsLoggedIn && !ConfirmDiscard(); // hết phiên thì đóng luôn
        ShowOrder(null);
    }

    private async Task LoadAsync()
    {
        var menu = await Program.Api.GetAsync<List<MenuItemDto>>("api/menu") ?? [];
        foreach (Control c in pnlMenu.Controls.Cast<Control>().ToList()) c.Dispose();
        foreach (var m in menu)
        {
            var b = new Button
            {
                Text = $"{m.ProductName}\n{m.Price:N0}đ", Size = new Size(150, 80), Tag = m.ProductName,
                FlatStyle = FlatStyle.Flat, BackColor = Color.White, ForeColor = Theme.Primary, Cursor = Cursors.Hand,
            };
            b.FlatAppearance.BorderColor = Theme.PrimaryLight;
            b.Click += (_, _) => AddMenuItem(m);
            pnlMenu.Controls.Add(b);
        }
        FilterMenu();
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
                Size = new Size(140, 64),
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
        pnlMenu.Enabled = btnPlus.Enabled = btnMinus.Enabled = btnRemove.Enabled = btnSubmit.Enabled = editable;
        btnDeliver.Enabled = editable && Program.Session.Role == "Waiter"; // API chỉ cho Phục vụ chuyển Đã giao
        btnMerge.Enabled = btnSplit.Enabled = editable && o!.TableId is not null;
        btnPay.Enabled = editable;
        grid.ReadOnly = !editable;
        UpdateTotal();
    }

    private void FilterMenu()
    {
        foreach (Control c in pnlMenu.Controls)
            c.Visible = TextSearch.Matches((string)c.Tag!, txtSearch.Text);
    }

    private void AddMenuItem(MenuItemDto m)
    {
        if (_order?.Status != OrderStatus.Open) return;
        var same = _lines.FirstOrDefault(l => l.Id is null && l.ProductId == m.ProductId && string.IsNullOrWhiteSpace(l.Note));
        if (same is not null)
        {
            same.Quantity++;
            _lines.ResetItem(_lines.IndexOf(same));
        }
        else
            _lines.Add(new Line { ProductId = m.ProductId, ProductName = m.ProductName, UnitPrice = m.Price, Status = OrderDetailStatus.Pending });
        _dirty = true;
        UpdateTotal();
    }

    private void ChangeQuantity(int delta)
    {
        if (Selected is not { } l || l.Status != OrderDetailStatus.Pending || l.Quantity + delta < 1) return;
        l.Quantity += delta;
        _lines.ResetItem(_lines.IndexOf(l));
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

    // thiếu nguyên liệu → ERR-STOCK-001 / ERR-BOM-001, món Bar đã bắt đầu làm → ERR-CON-001.
    private async Task SubmitAsync()
    {
        grid.EndEdit();
        var items = _lines.Where(l => l.Status == OrderDetailStatus.Pending)
            .Select(l => new { id = l.Id, productId = l.ProductId, quantity = l.Quantity, note = l.Note }).ToList();
        ShowOrder(await Program.Api.PutAsync<OrderDto>($"api/orders/{_order!.Id}/items", new { items }));
        ShowOrder(await Program.Api.PostAsync<OrderDto>($"api/orders/{_order!.Id}/submit"));
        lblNotice.ForeColor = Color.DarkGreen;
        lblNotice.Text = $"{DateTime.Now:HH:mm} Đã gửi order xuống pha chế.";
    }

    // Phục vụ bấm Đã giao cho món Bar đã làm Xong.
    private async Task DeliverAsync()
    {
        if (Selected is not { Id: { } id } l || l.Status != OrderDetailStatus.Done)
        {
            MessageBox.Show(this, "Chọn món đang ở trạng thái Xong để giao.", Text);
            return;
        }
        if (!ConfirmDiscard()) return;
        await Program.Api.PutAsync($"api/order-items/{id}/status", new UpdateItemStatusRequest(OrderDetailStatus.Delivered));
        ShowOrder(await Program.Api.GetAsync<OrderDto>($"api/orders/{_order!.Id}"));
    }

    // Gộp toàn bộ món bàn đang mở vào một bàn khác đang có order; bàn này về Trống.
    private async Task MergeAsync()
    {
        if (!ConfirmDiscard()) return;
        var tables = (await Program.Api.GetAsync<List<TableDto>>("api/tables") ?? [])
            .Where(t => t.OpenOrderId is not null && t.Id != _order!.TableId).ToList();
        if (tables.Count == 0) { MessageBox.Show(this, "Không có bàn nào khác đang có order để gộp.", Text); return; }
        if (PickTable($"Gộp {_order!.TableName} vào bàn", tables, 0) is not { } pick) return;

        ShowOrder(await Program.Api.PostAsync<OrderDto>("api/tables/merge", new { sourceTableId = _order.TableId, targetTableId = pick.Table.Id }));
        await LoadTablesAsync();
    }

    // Tách món đang chọn (một phần hoặc cả dòng) sang bàn khác; bàn trống sẽ được mở order mới.
    private async Task SplitAsync()
    {
        if (!ConfirmDiscard()) return;
        if (Selected is not { Id: { } id } l) { MessageBox.Show(this, "Chọn một món đã gửi để tách.", Text); return; }
        var tables = (await Program.Api.GetAsync<List<TableDto>>("api/tables") ?? []).Where(t => t.Id != _order!.TableId).ToList();
        if (PickTable($"Tách {l.ProductName} sang bàn", tables, l.Quantity) is not { } pick) return;

        await Program.Api.PostAsync<OrderDto>("api/tables/split", new
        {
            sourceTableId = _order!.TableId,
            targetTableId = pick.Table.Id,
            items = new[] { new { orderItemId = id, quantity = pick.Quantity } },
        });
        ShowOrder(await Program.Api.GetAsync<OrderDto>($"api/orders/{_order.Id}"));
        await LoadTablesAsync();
    }

    // Hộp chọn bàn đích (+ số lượng khi maxQuantity > 0). null = hủy.
    private (TableDto Table, int Quantity)? PickTable(string title, List<TableDto> tables, int maxQuantity)
    {
        using var f = new Form
        {
            Text = title, FormBorderStyle = FormBorderStyle.FixedDialog, StartPosition = FormStartPosition.CenterParent,
            MinimizeBox = false, MaximizeBox = false, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(12),
        };
        var cbo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240, DisplayMember = nameof(TableDto.Name) };
        cbo.Items.AddRange([.. tables]);
        if (tables.Count > 0) cbo.SelectedIndex = 0;
        var qty = new NumericUpDown { Minimum = 1, Maximum = Math.Max(1, maxQuantity), Value = Math.Max(1, maxQuantity), Width = 80 };
        var ok = new Button { Text = "Đồng ý", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = "Hủy", DialogResult = DialogResult.Cancel, AutoSize = true };
        var panel = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill };
        panel.Controls.AddRange([new Label { Text = "Bàn đích", AutoSize = true }, cbo]);
        if (maxQuantity > 0) panel.Controls.AddRange([new Label { Text = $"Số lượng (tối đa {maxQuantity})", AutoSize = true }, qty]);
        var buttons = new FlowLayoutPanel { AutoSize = true };
        buttons.Controls.AddRange([ok, cancel]);
        panel.Controls.Add(buttons);
        f.Controls.Add(panel);
        f.AcceptButton = ok;
        f.CancelButton = cancel;

        if (f.ShowDialog(this) != DialogResult.OK || cbo.SelectedItem is not TableDto t) return null;
        return (t, (int)qty.Value);
    }

    // BarHub: kết nối lỗi vẫn dùng form được, chỉ không nhận thông báo realtime (bấm Làm mới để thử lại).
    private async Task ConnectHubAsync()
    {
        try
        {
            await _hub.StartAsync();
            if (lblNotice.ForeColor == Color.Gray) lblNotice.Text = "";
        }
        catch (Exception) { lblNotice.ForeColor = Color.Gray; lblNotice.Text = "Chưa nhận được thông báo pha chế (mất kết nối) — bấm Làm mới để thử lại."; }
    }

    private void OnItemStatusChanged(ItemStatusChangedDto c) => OnUi(async () =>
    {
        lblNotice.ForeColor = c.Status == OrderDetailStatus.Done ? Color.DarkGreen : Color.Black;
        lblNotice.Text = $"{DateTime.Now:HH:mm} {c.TableName ?? "Mang đi"}: {c.ProductName} → {StatusName(c.Status)}";
        if (c.OrderId != _order?.Id || _dirty || !Enabled) return;
        try
        {
            var fresh = await Program.Api.GetAsync<OrderDto>($"api/orders/{c.OrderId}");
            if (c.OrderId == _order?.Id && !_dirty && Enabled) ShowOrder(fresh); // trong lúc chờ có thể đã sửa món / đổi bàn
        }
        catch (Exception) { }
    });

    private void OnItemUnavailable(ItemUnavailableDto u) => OnUi(() =>
    {
        lblNotice.ForeColor = Color.Red;
        lblNotice.Text = $"{DateTime.Now:HH:mm} HẾT NGUYÊN LIỆU: {u.ProductName} ({u.TableName ?? "Mang đi"}) — báo khách đổi món.";
        System.Media.SystemSounds.Exclamation.Play();
    });

    private void OnUi(Action a)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(a); }
        catch (InvalidOperationException) { }
    }

    private static string StatusName(OrderDetailStatus s) => s switch
    {
        OrderDetailStatus.Pending => "Chờ",
        OrderDetailStatus.Preparing => "Đang làm",
        OrderDetailStatus.Done => "Xong",
        _ => "Đã giao",
    };

    private void UpdateTotal() => lblTotal.Text = $"Tổng: {_lines.Sum(l => l.Amount):N0}đ";

    private bool ConfirmDiscard() =>
        !_dirty || MessageBox.Show(this, "Order có món chưa lưu. Bỏ các thay đổi này?", Text,
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes;

    private async Task RunAsync(Func<Task> action)
    {
        UseWaitCursor = true;
        Enabled = false;
        try { await action(); }
        catch (Exception ex) { ErrorPresenter.Show(this, ex); }
        finally { Enabled = true; UseWaitCursor = false; }
    }
}
