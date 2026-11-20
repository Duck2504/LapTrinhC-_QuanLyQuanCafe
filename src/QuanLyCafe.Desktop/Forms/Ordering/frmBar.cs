using QuanLyCafe.Desktop.Models;
using QuanLyCafe.Desktop.Services;

namespace QuanLyCafe.Desktop.Forms.Ordering;

public class frmBar : Form
{
    private record Row(Guid ItemId, DateTime OrderDate, string Table, string ProductName, int Quantity, string? Note,
        OrderDetailStatus Status)
    {
        public string Time => OrderDate.ToString("HH:mm");
        public string StatusText => Status switch
        {
            OrderDetailStatus.Pending => "Chờ",
            OrderDetailStatus.Preparing => "Đang làm",
            _ => "Xong",
        };
    }

    private readonly DataGridView grid = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, MultiSelect = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoGenerateColumns = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false,
        Font = new Font("Segoe UI", 11),
    };
    private readonly Button btnPreparing = new() { Text = "Đang làm", AutoSize = true };
    private readonly Button btnDone = new() { Text = "Xong", AutoSize = true };
    private readonly Button btnUnavailable = new() { Text = "Hết NL", AutoSize = true };
    private readonly Button btnReload = new() { Text = "Tải lại", AutoSize = true };
    private readonly Label lblConn = new() { AutoSize = true, Padding = new Padding(16, 6, 0, 0) };

    private readonly BarHubClient _hub = new(Program.BaseUrl, Program.Session);
    private int _loadSeq; 

    private Row? Selected => grid.CurrentRow?.DataBoundItem as Row;

    public frmBar()
    {
        Text = "Điều phối pha chế";
        Theme.Apply(this);
        Theme.Shortcuts(this, btnReload);
        Size = new Size(1000, 650);
        StartPosition = FormStartPosition.CenterParent;

        grid.Columns.AddRange(
            new DataGridViewTextBoxColumn { HeaderText = "Giờ order", DataPropertyName = nameof(Row.Time), FillWeight = 50 },
            new DataGridViewTextBoxColumn { HeaderText = "Bàn", DataPropertyName = nameof(Row.Table), FillWeight = 60 },
            new DataGridViewTextBoxColumn { HeaderText = "Món", DataPropertyName = nameof(Row.ProductName), FillWeight = 140 },
            new DataGridViewTextBoxColumn { HeaderText = "SL", DataPropertyName = nameof(Row.Quantity), FillWeight = 30 },
            new DataGridViewTextBoxColumn { HeaderText = "Ghi chú", DataPropertyName = nameof(Row.Note), FillWeight = 160 },
            new DataGridViewTextBoxColumn { HeaderText = "Trạng thái", DataPropertyName = nameof(Row.StatusText), FillWeight = 60 });
        grid.CellFormatting += (_, e) =>
        {
            if (e.RowIndex >= 0 && grid.Rows[e.RowIndex].DataBoundItem is Row r && e.CellStyle is not null)
                e.CellStyle.BackColor = r.Status switch
                {
                    OrderDetailStatus.Preparing => Color.LightYellow,
                    OrderDetailStatus.Done => Color.Honeydew,
                    _ => grid.DefaultCellStyle.BackColor,
                };
        };

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true };
        buttons.Controls.AddRange([btnPreparing, btnDone, btnUnavailable, btnReload, lblConn]);
        Controls.Add(grid);
        Controls.Add(buttons);

        btnPreparing.Click += async (_, _) => await RunAsync(() => SetStatusAsync(OrderDetailStatus.Preparing));
        btnDone.Click += async (_, _) => await RunAsync(() => SetStatusAsync(OrderDetailStatus.Done));
        btnUnavailable.Click += async (_, _) => await RunAsync(ReportUnavailableAsync);
        btnReload.Click += async (_, _) => await RunAsync(ConnectAndLoadAsync);
        grid.SelectionChanged += (_, _) => UpdateButtons();
        grid.CellDoubleClick += async (_, e) =>
        {
            if (e.RowIndex < 0) return;
            if (btnPreparing.Enabled) await RunAsync(() => SetStatusAsync(OrderDetailStatus.Preparing));
            else if (btnDone.Enabled) await RunAsync(() => SetStatusAsync(OrderDetailStatus.Done));
        };

        _hub.TicketCreated += OnTicketCreated;
        _hub.ItemStatusChanged += OnItemStatusChanged;
        _hub.ConnectionChanged += OnConnectionChanged;

        _retry.Tick += async (_, _) =>
        {
            if (_hub.IsConnected || !Enabled) return;
            try { await _hub.StartAsync(); } catch (Exception) { return; }
            OnConnectionChanged(_hub.IsConnected);
        };

        Load += async (_, _) => { await RunAsync(ConnectAndLoadAsync); _retry.Start(); };
        FormClosing += (_, _) =>
        {
            _retry.Stop();
            _hub.TicketCreated -= OnTicketCreated;
            _hub.ItemStatusChanged -= OnItemStatusChanged;
            _hub.ConnectionChanged -= OnConnectionChanged;
        };
        FormClosed += async (_, _) => { _retry.Dispose(); await _hub.DisposeAsync(); };
    }

    private readonly System.Windows.Forms.Timer _retry = new() { Interval = 10_000 };

    private void OnTicketCreated(BarTicketDto _) => OnHubEvent();
    private void OnItemStatusChanged(ItemStatusChangedDto _) => OnHubEvent();
    private void OnConnectionChanged(bool connected) => OnUi(() =>
    {
        ShowConnection(connected);
        if (connected) _ = ReloadAsync();
    });

    private async Task ConnectAndLoadAsync()
    {
        try
        {
            await _hub.StartAsync();
            ShowConnection(_hub.IsConnected);
        }
        catch (Exception)
        {
            ShowConnection(false);
        }
        await LoadAsync();
    }

    private async Task LoadAsync()
    {
        var seq = ++_loadSeq;
        var tickets = await Program.Api.GetAsync<List<BarTicketDto>>("api/bar/tickets") ?? [];
        if (seq != _loadSeq || IsDisposed) return;

        var keep = Selected?.ItemId;
        var rows = tickets.OrderBy(t => t.OrderDate)
            .SelectMany(t => t.Items.Select(i => new Row(i.Id, t.OrderDate, t.TableName ?? "Mang đi", i.ProductName, i.Quantity, i.Note, i.Status)))
            .ToList();
        grid.DataSource = rows;
        var again = rows.FindIndex(r => r.ItemId == keep);
        if (again >= 0) grid.CurrentCell = grid.Rows[again].Cells[0];
        UpdateButtons();
    }

    private async Task ReloadAsync()
    {
        try { await LoadAsync(); }
        catch (Exception ex) { lblConn.Text = "Tải danh sách lỗi: " + (ex is ApiException api ? ErrorMessages.For(api) : ex.Message); }
    }

    private void OnHubEvent() => OnUi(() => _ = ReloadAsync());

    private void OnUi(Action a)
    {
        if (IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(a); }
        catch (InvalidOperationException) { }
    }

    private async Task SetStatusAsync(OrderDetailStatus status)
    {
        if (Selected is not { } row) return;
        try { await Program.Api.PutAsync($"api/order-items/{row.ItemId}/status", new UpdateItemStatusRequest(status)); }
        finally { await ReloadAsync(); }
    }

    private async Task ReportUnavailableAsync()
    {
        if (Selected is not { } row) return;
        if (MessageBox.Show(this, $"Báo hết nguyên liệu cho {row.ProductName} ({row.Table})?\nPhục vụ và thu ngân sẽ nhận thông báo để đổi món.",
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        await Program.Api.PostAsync($"api/order-items/{row.ItemId}/unavailable");
        MessageBox.Show(this, "Đã báo phục vụ và thu ngân.", Text);
    }

    private void UpdateButtons()
    {
        var barista = Program.Session.Role == "Barista";
        var status = Selected?.Status;
        btnPreparing.Enabled = barista && status == OrderDetailStatus.Pending;
        btnDone.Enabled = barista && status == OrderDetailStatus.Preparing;
        btnUnavailable.Enabled = barista && status == OrderDetailStatus.Pending;
    }

    private void ShowConnection(bool connected) =>
        lblConn.Text = Program.Session.Role != "Barista" ? "Chế độ xem — bấm Tải lại để cập nhật"
            : connected ? "● Đang nhận phiếu realtime" : "○ Mất kết nối realtime — đang tự thử lại";

    private async Task RunAsync(Func<Task> action)
    {
        UseWaitCursor = true;
        Enabled = false;
        try { await action(); }
        catch (Exception ex) { ErrorPresenter.Show(this, ex); }
        finally { Enabled = true; UseWaitCursor = false; UpdateButtons(); }
    }
}
