using System.Globalization;
using QuanLyCafe.Desktop.Models;
using QuanLyCafe.Desktop.Services;

namespace QuanLyCafe.Desktop.Forms.Payment;

public sealed class frmPayment : Form
{
    private readonly ApiClient _api;
    private readonly ListBox _openOrders = new() { Dock = DockStyle.Fill, IntegralHeight = false, ItemHeight = 48, DrawMode = DrawMode.OwnerDrawFixed };
    private readonly Button _refresh = new() { Text = "Làm mới", Dock = DockStyle.Bottom, Height = 34 };
    private readonly ComboBox _method = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 115 };
    private readonly NumericUpDown _cash = new() { Maximum = decimal.MaxValue, DecimalPlaces = 0, Increment = 1000, ThousandsSeparator = true, Width = 140 };
    private readonly Label _summary = new() { AutoSize = true, Font = new Font("Segoe UI", 13, FontStyle.Bold), Text = "Chọn một đơn để xem món và tổng tiền." };
    private readonly Label _change = new() { AutoSize = true, Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = Theme.Primary };
    private readonly List<Button> _quickCash = [];
    private readonly Button _pay = new() { Text = "Thanh toán", AutoSize = true };
    private readonly Button _preview = new() { Text = "Xem trước / In hóa đơn", AutoSize = true };
    private readonly DataGridView _items = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false,
        AutoGenerateColumns = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        RowHeadersVisible = false
    };

    private OrderDto? _order;
    private InvoiceDto? _invoice;
    private decimal? _paidChange;
    private bool _busy;

    public frmPayment(ApiClient api, Guid? openOrderId = null)
    {
        _api = api;
        Text = "Thanh toán";
        Theme.Apply(this);
        Theme.Shortcuts(this, _refresh);
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(820, 490);
        MinimumSize = new Size(720, 440);

        _method.Items.AddRange(Enum.GetValues<PaymentMethod>()
            .Select(method => new PaymentOption(method, InvoicePrinter.MethodName(method))).ToArray());
        _method.DisplayMember = nameof(PaymentOption.Label);
        _method.SelectedIndex = 0;

        AddColumn("Món", nameof(InvoiceLineDto.ProductName), 32);
        AddColumn("SL", nameof(InvoiceLineDto.Quantity), 9);
        AddColumn("Đơn giá", nameof(InvoiceLineDto.UnitPrice), 17, "#,0.##");
        AddColumn("Thành tiền", nameof(InvoiceLineDto.LineTotal), 18, "#,0.##");
        AddColumn("Ghi chú", nameof(InvoiceLineDto.Note), 24);

        _quickCash.Add(QuickCash("Đủ tiền", () => _order?.TotalAmount ?? 0));
        foreach (var amount in new decimal[] { 50_000, 100_000, 200_000, 500_000 })
            _quickCash.Add(QuickCash($"{amount / 1000:0}k", () => amount));

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 6 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        for (var i = 0; i < 4; i++) layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(Row(_summary), 0, 0);
        layout.Controls.Add(_items, 0, 1);
        layout.Controls.Add(Row(Caption("Phương thức"), _method, Caption("Tiền khách đưa"), _cash), 0, 2);
        layout.Controls.Add(Row([.. _quickCash]), 0, 3);
        layout.Controls.Add(Row(_change), 0, 4);
        layout.Controls.Add(Row(_pay, _preview), 0, 5);

        var ordersBox = new GroupBox { Text = "Đơn đang mở", Dock = DockStyle.Left, Width = 230, Padding = new Padding(8) };
        ordersBox.Controls.Add(_openOrders);
        ordersBox.Controls.Add(_refresh);
        Controls.Add(layout);
        Controls.Add(ordersBox);

        Shown += async (_, _) =>
        {
            await RunAsync(RefreshOrdersAsync);
            _openOrders.SelectedItem = _openOrders.Items.OfType<OpenOrderOption>().FirstOrDefault(o => o.Id == openOrderId);
        };
        _refresh.Click += async (_, _) => await RunAsync(RefreshOrdersAsync);
        _openOrders.DrawItem += DrawOrder;
        _openOrders.SelectedIndexChanged += async (_, _) =>
        {
            if (!_busy && _openOrders.SelectedItem is OpenOrderOption option)
                await RunAsync(() => LoadOrderAsync(option.Id));
        };
        _method.SelectedIndexChanged += (_, _) => UpdateControls();
        _cash.ValueChanged += (_, _) => UpdateControls();
        _cash.TextChanged += (_, _) => UpdateControls();
        _pay.Click += async (_, _) => await RunAsync(PayAsync);
        _preview.Click += async (_, _) => await RunAsync(PreviewAsync);
        FormClosing += (_, args) =>
        {
            if (_busy && args.CloseReason == CloseReason.UserClosing && Program.Session.IsLoggedIn) // hết phiên thì cho đóng
                args.Cancel = true;
        };
        UpdateControls();
    }

    private static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        row.Controls.AddRange(controls);
        return row;
    }

    private Button QuickCash(string text, Func<decimal> amount)
    {
        var b = new Button { Text = text, AutoSize = true, MinimumSize = new Size(72, 34) };
        b.Click += (_, _) => _cash.Value = Math.Min(_cash.Maximum, amount());
        return b;
    }

    private void DrawOrder(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0) return;
        var option = (OpenOrderOption)_openOrders.Items[e.Index];
        var selected = (e.State & DrawItemState.Selected) != 0;
        using var back = new SolidBrush(selected ? Theme.PrimaryLight : e.Index % 2 == 0 ? Color.White : Theme.RowAlt);
        e.Graphics.FillRectangle(back, e.Bounds);
        var color = selected ? Color.White : Color.Black;
        var bounds = Rectangle.Inflate(e.Bounds, -8, -4);
        TextRenderer.DrawText(e.Graphics, option.Label, Font, bounds, color, TextFormatFlags.Top | TextFormatFlags.Left);
        using var bold = new Font(Font, FontStyle.Bold);
        TextRenderer.DrawText(e.Graphics, option.Total, bold, bounds, color, TextFormatFlags.Bottom | TextFormatFlags.Left);
    }

    private static Label Caption(string text) =>
        new() { Text = text, AutoSize = true, Margin = new Padding(3, 7, 3, 0) };

    private void AddColumn(string title, string property, int weight, string? format = null)
    {
        var column = new DataGridViewTextBoxColumn
        {
            HeaderText = title, DataPropertyName = property, FillWeight = weight, SortMode = DataGridViewColumnSortMode.NotSortable
        };
        if (format != null)
        {
            column.DefaultCellStyle.Format = format;
            column.DefaultCellStyle.FormatProvider = CultureInfo.GetCultureInfo("vi-VN");
            column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
        _items.Columns.Add(column);
    }

    private async Task RunAsync(Func<Task> action)
    {
        _busy = true;
        UpdateControls();
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            ErrorPresenter.Show(this, ex);
        }
        finally
        {
            _busy = false;
            UpdateControls();
        }
    }

    private async Task RefreshOrdersAsync()
    {
        var orders = await _api.GetAsync<List<OrderDto>>("api/orders/open") ?? [];
        _openOrders.Items.Clear();
        _openOrders.Items.AddRange(orders.Select(order => new OpenOrderOption(order.Id,
            $"{order.TableName ?? "Mang đi"}  ·  {order.OrderDate:HH:mm}", InvoicePrinter.Money(order.TotalAmount))).ToArray());
    }

    private async Task LoadOrderAsync(Guid id)
    {
        ShowOrder(null);
        var order = await _api.GetAsync<OrderDto>($"api/orders/{id}")
            ?? throw new ApiException(0, null, "Không tìm thấy đơn.");
        ShowOrder(order);
    }

    private void ShowOrder(OrderDto? order)
    {
        _order = order;
        _invoice = null;
        _paidChange = null;
        _cash.Value = 0;
        _items.DataSource = order?.Items.Select(item =>
            new InvoiceLineDto(item.ProductName, item.Quantity, item.UnitPrice, item.Note)).ToList();

        if (order is null)
        {
            _summary.Text = "Chọn một đơn để xem món và tổng tiền.";
        }
        else
        {
            var status = order.Status switch
            {
                OrderStatus.Paid => "Đã thanh toán",
                OrderStatus.Cancelled => "Đã hủy",
                _ when Unfinished(order) => "Còn món chưa pha xong",
                _ => "Chưa thanh toán"
            };
            _summary.Text = $"{order.TableName ?? "Mang đi"}   |   {status}\nTổng tiền {InvoicePrinter.Money(order.TotalAmount)}";
        }
        UpdateControls();
    }

    private static bool Unfinished(OrderDto order) =>
        order.Items.Any(i => i.Status is OrderDetailStatus.Pending or OrderDetailStatus.Preparing);

    private PaymentMethod SelectedMethod => ((PaymentOption)_method.SelectedItem!).Method;

    private void UpdateControls()
    {
        var canPay = _order is { Status: OrderStatus.Open, Items.Count: > 0 } && !Unfinished(_order);
        _refresh.Enabled = !_busy;
        _openOrders.Enabled = !_busy && _openOrders.Items.Count > 0;
        _method.Enabled = _pay.Enabled = !_busy && canPay;
        _cash.Enabled = !_busy && canPay && SelectedMethod == PaymentMethod.Cash;
        foreach (var b in _quickCash) b.Enabled = _cash.Enabled;
        _preview.Enabled = !_busy && _order?.Status == OrderStatus.Paid;
        _change.Text = "";

        if (_paidChange is decimal paidChange)
            _change.Text = $"Tiền thối {InvoicePrinter.Money(paidChange)}";
        else if (canPay && SelectedMethod == PaymentMethod.Cash &&
            decimal.TryParse(_cash.Text, out var cashGiven) && cashGiven >= _order!.TotalAmount)
            _change.Text = $"Tiền thối dự kiến {InvoicePrinter.Money(cashGiven - _order.TotalAmount)}";
    }

    private async Task PayAsync()
    {
        if (_order is not { Status: OrderStatus.Open, Items.Count: > 0 } order)
            return;

        var method = SelectedMethod;
        if (method == PaymentMethod.Cash && _cash.Value < order.TotalAmount)
        {
            MessageBox.Show(this, "Số tiền khách đưa chưa đủ.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var request = new PaymentRequest(order.Id, method, method == PaymentMethod.Cash ? _cash.Value : null);
        PaymentResultDto result;
        try
        {
            result = await _api.PostAsync<PaymentResultDto>("api/payments", request)
                ?? throw new ApiException(0, null, "Chưa nhận được kết quả thanh toán. Hãy tải lại đơn để kiểm tra.");
        }
        catch (ApiException ex) when (ex.Code == "ERR-PAY-002")
        {
            var fresh = await _api.GetAsync<OrderDto>($"api/orders/{order.Id}");
            if (fresh != null)
                ShowOrder(fresh);
            throw;
        }

        ShowOrder(order with { Status = OrderStatus.Paid, TotalAmount = result.Invoice.TotalAmount });
        _invoice = result.Invoice;
        _items.DataSource = _invoice.Lines;
        _paidChange = _invoice.PaymentMethod == PaymentMethod.Cash ? result.Change : null;
        if (_openOrders.Items.OfType<OpenOrderOption>().FirstOrDefault(option => option.Id == order.Id) is { } paidOption)
            _openOrders.Items.Remove(paidOption);
    }

    private async Task PreviewAsync()
    {
        if (_order is not { Status: OrderStatus.Paid } order)
            return;

        _invoice ??= await _api.GetAsync<InvoiceDto>($"api/orders/{order.Id}/invoice")
            ?? throw new ApiException(0, null, "Không tải được hóa đơn.");
        InvoicePrinter.ShowPreview(this, _invoice);
    }

    private sealed record OpenOrderOption(Guid Id, string Label, string Total);
    private sealed record PaymentOption(PaymentMethod Method, string Label);
}
