using System.Globalization;
using QuanLyCafe.Desktop.Models;
using QuanLyCafe.Desktop.Services;

namespace QuanLyCafe.Desktop.Forms.Payment;

public sealed class frmPayment : Form
{
    private readonly ApiClient _api;
    private readonly ComboBox _openOrders = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 350 };
    private readonly TextBox _orderId = new() { Width = 290, PlaceholderText = "Nhập mã đơn" };
    private readonly Button _refresh = new() { Text = "Làm mới", AutoSize = true };
    private readonly Button _load = new() { Text = "Tải đơn", AutoSize = true };
    private readonly ComboBox _method = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 115 };
    private readonly NumericUpDown _cash = new() { Maximum = decimal.MaxValue, DecimalPlaces = 2, ThousandsSeparator = true, Width = 125 };
    private readonly Label _summary = new() { AutoSize = true, Text = "Chọn một đơn để xem món và tổng tiền." };
    private readonly Label _change = new() { AutoSize = true };
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

    public frmPayment(ApiClient api)
    {
        _api = api;
        Text = "Thanh toán";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(820, 490);
        MinimumSize = new Size(720, 440);

        _openOrders.DisplayMember = nameof(OpenOrderOption.Label);
        _method.Items.AddRange(Enum.GetValues<PaymentMethod>()
            .Select(method => new PaymentOption(method, InvoicePrinter.MethodName(method))).ToArray());
        _method.DisplayMember = nameof(PaymentOption.Label);
        _method.SelectedIndex = 0;

        AddColumn("Món", nameof(InvoiceLineDto.ProductName), 32);
        AddColumn("SL", nameof(InvoiceLineDto.Quantity), 9);
        AddColumn("Đơn giá", nameof(InvoiceLineDto.UnitPrice), 17, "#,0.##");
        AddColumn("Thành tiền", nameof(InvoiceLineDto.LineTotal), 18, "#,0.##");
        AddColumn("Ghi chú", nameof(InvoiceLineDto.Note), 24);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(16), ColumnCount = 1, RowCount = 6 };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(Row(Caption("Đơn đang mở"), _openOrders, _refresh), 0, 0);
        layout.Controls.Add(Row(Caption("Mã đơn"), _orderId, _load), 0, 1);
        layout.Controls.Add(Row(_summary), 0, 2);
        layout.Controls.Add(_items, 0, 3);
        layout.Controls.Add(Row(Caption("Phương thức"), _method, Caption("Tiền khách đưa"), _cash, _change), 0, 4);
        layout.Controls.Add(Row(_pay, _preview), 0, 5);
        Controls.Add(layout);

        Shown += async (_, _) => await RunAsync(RefreshOrdersAsync);
        _refresh.Click += async (_, _) => await RunAsync(RefreshOrdersAsync);
        _openOrders.SelectionChangeCommitted += async (_, _) =>
        {
            if (_openOrders.SelectedItem is OpenOrderOption option)
            {
                _orderId.Text = option.Id.ToString();
                await RunAsync(LoadOrderAsync);
            }
        };
        _orderId.TextChanged += (_, _) => ShowOrder(null);
        _load.Click += async (_, _) => await RunAsync(LoadOrderAsync);
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
            $"{order.TableName ?? "Mang đi"}   {order.OrderDate:HH:mm dd/MM}   {InvoicePrinter.Money(order.TotalAmount)}")).ToArray());
    }

    private async Task LoadOrderAsync()
    {
        ShowOrder(null);
        if (!Guid.TryParse(_orderId.Text.Trim(), out var id) || id == Guid.Empty)
            throw new ApiException(0, null, "Vui lòng nhập mã đơn hợp lệ.");

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
                _ => "Chưa thanh toán"
            };
            _summary.Text = $"{order.TableName ?? "Mang đi"}   |   {status}   |   Tổng tiền {InvoicePrinter.Money(order.TotalAmount)}";
        }
        UpdateControls();
    }

    private PaymentMethod SelectedMethod => ((PaymentOption)_method.SelectedItem!).Method;

    private void UpdateControls()
    {
        var canPay = _order is { Status: OrderStatus.Open, Items.Count: > 0 };
        _orderId.Enabled = _load.Enabled = _refresh.Enabled = !_busy;
        _openOrders.Enabled = !_busy && _openOrders.Items.Count > 0;
        _method.Enabled = _pay.Enabled = !_busy && canPay;
        _cash.Enabled = !_busy && canPay && SelectedMethod == PaymentMethod.Cash;
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

    private sealed record OpenOrderOption(Guid Id, string Label);
    private sealed record PaymentOption(PaymentMethod Method, string Label);
}
