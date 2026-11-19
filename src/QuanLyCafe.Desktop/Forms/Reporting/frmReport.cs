using System.Globalization;
using QuanLyCafe.Desktop.Models;
using QuanLyCafe.Desktop.Services;
using ScottPlot.WinForms;

namespace QuanLyCafe.Desktop.Forms.Reporting;

public sealed class frmReport : Form
{
    private static readonly CultureInfo VietnameseCulture = CultureInfo.GetCultureInfo("vi-VN");
    private const string MoneyFormat = "#,0.##";
    private static readonly (string Name, string Value)[] RevenueGroups =
        [("Theo ngày", "day"), ("Theo tháng", "month"), ("Theo ca", "shift")];

    private readonly ApiClient _api;
    private readonly DateTimePicker _from = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Width = 120 };
    private readonly DateTimePicker _to = new() { Format = DateTimePickerFormat.Custom, CustomFormat = "dd/MM/yyyy", Width = 120 };
    private readonly ComboBox _groupBy = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 125 };
    private readonly Button _load = new() { Text = "Xem báo cáo", AutoSize = true };
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly FormsPlot _plot = new() { Dock = DockStyle.Fill };
    private readonly DataGridView _revenue = CreateGrid();
    private readonly DataGridView _products = CreateGrid();
    private readonly DataGridView _inventory = CreateGrid();
    private readonly Label _status = new() { AutoSize = true, Margin = new Padding(4, 8, 4, 4) };
    private CancellationTokenSource? _request;

    public frmReport(ApiClient api)
    {
        _api = api;
        Text = "Báo cáo";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(1020, 680);
        MinimumSize = new Size(800, 500);
        AutoScaleMode = AutoScaleMode.Font;
        _from.Value = _to.Value = DateTime.Today;
        _groupBy.Items.AddRange(RevenueGroups.Select(group => group.Name).ToArray());
        _groupBy.SelectedIndex = 0;

        var filters = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        filters.Controls.AddRange([
            FilterLabel("Từ ngày"), _from, FilterLabel("Đến ngày"), _to,
            FilterLabel("Doanh thu"), _groupBy, _load
        ]);

        AddColumn(_revenue, "Ngày / Tháng / Ca", nameof(RevenuePointDto.Label));
        AddColumn(_revenue, "Số hóa đơn", nameof(RevenuePointDto.OrderCount), "N0");
        AddColumn(_revenue, "Doanh thu (₫)", nameof(RevenuePointDto.Revenue), MoneyFormat);

        AddColumn(_products, "Tên món", nameof(TopProductDto.ProductName));
        AddColumn(_products, "Số lượng bán", nameof(TopProductDto.Quantity), "N0");
        AddColumn(_products, "Doanh thu (₫)", nameof(TopProductDto.Revenue), MoneyFormat);

        AddColumn(_inventory, "Nguyên liệu", nameof(InventoryReportDto.MaterialName));
        AddColumn(_inventory, "Đơn vị", nameof(InventoryReportDto.Unit));
        AddColumn(_inventory, "Tồn hiện tại", nameof(InventoryReportDto.StockQuantity), "0.###");
        AddColumn(_inventory, "Mức tối thiểu", nameof(InventoryReportDto.MinQuantity), "0.###");
        AddColumn(_inventory, "Hao hụt trong kỳ", nameof(InventoryReportDto.WastedQuantity), "0.###");

        var revenueLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2 };
        revenueLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 58));
        revenueLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 42));
        revenueLayout.Controls.Add(_plot, 0, 0);
        revenueLayout.Controls.Add(_revenue, 0, 1);
        AddTab("Doanh thu", revenueLayout);
        AddTab("Món bán chạy", _products);
        AddTab("Tồn kho", _inventory);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
            ColumnCount = 1,
            RowCount = 3
        };
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        layout.Controls.Add(filters, 0, 0);
        layout.Controls.Add(_tabs, 0, 1);
        layout.Controls.Add(_status, 0, 2);
        Controls.Add(layout);

        AcceptButton = _load;
        _load.Click += async (_, _) => await LoadReportsAsync();
        Shown += async (_, _) => await LoadReportsAsync();
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _request?.Cancel();
        base.OnFormClosed(e);
    }

    private async Task LoadReportsAsync()
    {
        if (_request is not null)
            return;

        if (_from.Value.Date > _to.Value.Date)
        {
            _status.Text = "Từ ngày phải trước hoặc bằng Đến ngày.";
            return;
        }

        var from = _from.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var to = _to.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var groupBy = RevenueGroups[_groupBy.SelectedIndex].Value;
        var range = $"from={from}&to={to}";

        using var request = new CancellationTokenSource();
        _request = request;
        _load.Enabled = _from.Enabled = _to.Enabled = _groupBy.Enabled = false;
        _status.Text = "Đang tải báo cáo…";

        try
        {
            _revenue.DataSource = _products.DataSource = _inventory.DataSource = null;
            DrawRevenue([]);

            var revenue = _api.GetAsync<List<RevenuePointDto>>(
                $"api/reports/revenue?{range}&groupBy={groupBy}", request.Token);
            var products = _api.GetAsync<List<TopProductDto>>($"api/reports/top-products?{range}", request.Token);
            var inventory = _api.GetAsync<List<InventoryReportDto>>($"api/reports/inventory?{range}", request.Token);

            await Task.WhenAll(revenue, products, inventory);
            request.Token.ThrowIfCancellationRequested();

            var revenueRows = await revenue ?? [];
            _revenue.DataSource = revenueRows;
            _products.DataSource = await products ?? [];
            _inventory.DataSource = await inventory ?? [];
            DrawRevenue(revenueRows);

            var total = revenueRows.Sum(row => row.Revenue).ToString(MoneyFormat, VietnameseCulture);
            var count = revenueRows.Sum(row => row.OrderCount).ToString("N0", VietnameseCulture);
            var period = $"{_from.Value:dd/MM/yyyy} – {_to.Value:dd/MM/yyyy}";
            _status.Text = revenueRows.Count == 0
                ? $"{period}  •  Không có hóa đơn đã thanh toán"
                : $"{period}  •  {total} ₫  •  {count} hóa đơn đã thanh toán";
        }
        catch (OperationCanceledException) when (request.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (!IsDisposed && !request.IsCancellationRequested)
                _status.Text = $"Không tải được báo cáo. {ex.Message}";
        }
        finally
        {
            _request = null;
            if (!IsDisposed && !request.IsCancellationRequested)
                _load.Enabled = _from.Enabled = _to.Enabled = _groupBy.Enabled = true;
        }
    }

    private void DrawRevenue(IReadOnlyList<RevenuePointDto> rows)
    {
        var plot = _plot.Plot;
        plot.Clear();
        plot.YLabel("Doanh thu (₫)");
        var ticks = new ScottPlot.TickGenerators.NumericManual();
        plot.Axes.Bottom.TickGenerator = ticks;

        if (rows.Count == 0)
        {
            plot.Axes.SetLimits(0, 1, 0, 1);
        }
        else
        {
            plot.Add.Bars(rows.Select(row => (double)row.Revenue).ToArray());
            var tickStep = (int)Math.Ceiling(rows.Count / 10.0);
            for (var index = 0; index < rows.Count; index += tickStep)
                ticks.AddMajor(index, rows[index].Label);

            plot.Axes.Margins(bottom: 0);
            plot.Axes.AutoScale();
        }

        _plot.Refresh();
    }

    private void AddTab(string title, Control content)
    {
        var tab = new TabPage(title) { Padding = new Padding(6) };
        tab.Controls.Add(content);
        _tabs.TabPages.Add(tab);
    }

    private static Label FilterLabel(string text) =>
        new() { Text = text, AutoSize = true, Margin = new Padding(4, 7, 4, 0) };

    private static DataGridView CreateGrid() => new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AutoGenerateColumns = false,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect,
        RowHeadersVisible = false
    };

    private static void AddColumn(DataGridView grid, string title, string property, string? format = null)
    {
        var column = new DataGridViewTextBoxColumn { HeaderText = title, DataPropertyName = property };
        if (format is not null)
        {
            column.DefaultCellStyle.Format = format;
            column.DefaultCellStyle.FormatProvider = VietnameseCulture;
            column.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
        }

        grid.Columns.Add(column);
    }
}
