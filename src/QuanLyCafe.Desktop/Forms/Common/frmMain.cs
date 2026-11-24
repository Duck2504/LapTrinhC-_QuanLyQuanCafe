
using QuanLyCafe.Desktop.Forms.Catalog;
using QuanLyCafe.Desktop.Forms.Inventory;
using QuanLyCafe.Desktop.Forms.Ordering;
using QuanLyCafe.Desktop.Forms.Payment;
using QuanLyCafe.Desktop.Forms.Reporting;
using QuanLyCafe.Desktop.Forms.Staff;
using QuanLyCafe.Desktop.Models;
using QuanLyCafe.Desktop.Services;

namespace QuanLyCafe.Desktop.Forms;

public class frmMain : Form
{
    private readonly IAuthApi _auth;
    private readonly MenuStrip _menu = new();
    private readonly Label _hello = new() { Dock = DockStyle.Top, Height = 64, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(24, 0, 0, 0), Font = new Font("Segoe UI", 15), BackColor = Theme.Primary, ForeColor = Color.White };
    private readonly FlowLayoutPanel _tiles = new() { Dock = DockStyle.Fill, AutoScroll = true };
    private static readonly Size CardSize = new(260, 130);
    private const int CardMargin = 12;
    private readonly Label _lowStock = new() { Dock = DockStyle.Bottom, Height = 36, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.DarkRed, Font = new Font("Segoe UI", 11, FontStyle.Bold) };
    private readonly Dictionary<Guid, LowStockAlertDto> _lowItems = [];

    public bool LoggedOut { get; private set; }   // true = quay về form đăng nhập

    public frmMain(IAuthApi auth)
    {
        _auth = auth;
        Text = "Quản lý quán Cafe";
        Theme.Apply(this);
        WindowState = FormWindowState.Maximized;
        _hello.Text = $"☕  QUÁN CAFE    |    Xin chào, {Program.Session.FullName} · {Program.Session.Role}";
        _tiles.Resize += (_, _) => CenterCards();
        Controls.Add(_tiles);
        Controls.Add(_hello);
        Controls.Add(_lowStock);
        Controls.Add(_menu);
        MainMenuStrip = _menu;
        BuildMenu(Program.Session.Role);
        if (Program.Session.Role == "Manager") ListenLowStock();
        Program.Api.SessionExpired += OnSessionExpired;
        FormClosed += (_, _) => Program.Api.SessionExpired -= OnSessionExpired;
    }

    // API trả 401 (token hết hạn): đóng mọi form (form con bỏ qua hỏi xác nhận khi đã hết phiên) rồi quay về đăng nhập.
    private void OnSessionExpired()
    {
        if (IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke(ForceLogout); } catch (InvalidOperationException) { }
    }

    // LowStockAlert (BarHub → nhóm Manager): hiện danh sách nguyên liệu dưới mức tối thiểu ở chân màn hình.
    private void ListenLowStock()
    {
        var hub = new BarHubClient(Program.BaseUrl, Program.Session);
        hub.LowStockAlert += a =>
        {
            if (IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke(() => ShowLowStock(a)); } catch (InvalidOperationException) { }
        };
        Shown += async (_, _) =>
        {
            try { await hub.StartAsync(); }
            catch (Exception) { _lowStock.Text = "Không kết nối được kênh cảnh báo tồn kho."; }
        };
        FormClosed += async (_, _) => await hub.DisposeAsync();
    }

    private void ShowLowStock(LowStockAlertDto a)
    {
        _lowItems[a.MaterialId] = a;
        _lowStock.Text = "⚠ Dưới mức tối thiểu: " + string.Join(", ", _lowItems.Values.Select(x => $"{x.MaterialName} (còn {x.StockQuantity:0.###})"));
        System.Media.SystemSounds.Exclamation.Play();
    }

    private void BuildMenu(string role)
    {
        bool manager = role == "Manager", cashier = role == "Cashier",
             waiter = role == "Waiter",   barista = role == "Barista";

        if (cashier || waiter) // API gọi món chỉ cho Cashier/Waiter
            Add("🧾", "Gọi món", "Sơ đồ bàn, nhận order", () => new frmPOS());                       // SCR_004
        if (manager || cashier)
            Add("💳", "Thanh toán", "Thu tiền, in hóa đơn", () => new frmPayment(Program.Api));       // SCR_005
        if (manager || barista)
            Add("☕", "Điều phối pha chế", "Hàng đợi món cần pha", () => new frmBar());                // SCR_006
        if (manager)
        {
            Add("🍰", "Sản phẩm", "Thực đơn và giá bán", () => new frmProducts(Program.Api));         // SCR_007
            Add("🗂", "Danh mục", "Nhóm món trên thực đơn", () => new frmCategories(Program.Api));
            Add("⚖", "Định lượng BOM", "Công thức nguyên liệu", () => new frmBom(Program.Api));       // SCR_008
            Add("📦", "Kho", "Nhập, xuất, kiểm kê", () => new frmInventory(Program.Api));              // SCR_009
            Add("👥", "Nhân viên", "Tài khoản và phân quyền", () => new frmStaff());                   // SCR_010
            Add("📊", "Báo cáo", "Doanh thu, món bán chạy", () => new frmReport(Program.Api));         // SCR_011
            Add("🔳", "QR gọi món", "In mã QR đặt tại bàn", () => new frmTableQr(Program.Api));
        }

        var account = new ToolStripMenuItem("Tài khoản");
        account.DropDownItems.Add("Đổi mật khẩu", null, (_, _) =>
        {
            using var f = new frmChangePassword(_auth);
            f.ShowDialog(this);
        });
        account.DropDownItems.Add("Đăng xuất", null, (_, _) =>
        {
            Program.Session.Clear();
            LoggedOut = true;
            Close();
        });
        _menu.Items.Add(account);
    }

    private void Add(string icon, string title, string desc, Func<Form> open)
    {
        var card = new Button { Text = title, Size = CardSize, Margin = new Padding(CardMargin), Cursor = Cursors.Hand };
        bool hover = false;
        card.MouseEnter += (_, _) => { hover = true; card.Invalidate(); };
        card.MouseLeave += (_, _) => { hover = false; card.Invalidate(); };
        card.Paint += (_, e) =>
        {
            var g = e.Graphics;
            var r = card.ClientRectangle;
            g.Clear(hover ? Theme.RowAlt : Color.White);
            using (var border = new Pen(card.Focused ? Theme.Primary : Color.FromArgb(0xE2, 0xD6, 0xC8)))
                g.DrawRectangle(border, 0, 0, r.Width - 1, r.Height - 1);
            using (var accent = new SolidBrush(Theme.Primary))
                g.FillRectangle(accent, 0, 0, 6, r.Height);
            using var iconFont = new Font("Segoe UI Emoji", 20);
            using var titleFont = new Font("Segoe UI", 13, FontStyle.Bold);
            using var descFont = new Font("Segoe UI", 9.5f);
            TextRenderer.DrawText(g, icon, iconFont, new Point(22, 16), Theme.Primary);
            TextRenderer.DrawText(g, title, titleFont, new Point(22, 62), Theme.Primary);
            TextRenderer.DrawText(g, desc, descFont, new Point(23, 92), Color.DimGray);
        };
        card.GotFocus += (_, _) => card.Invalidate();
        card.LostFocus += (_, _) => card.Invalidate();
        card.Click += (_, _) =>
        {
            using var f = open();
            f.ShowDialog(this);
        };
        _tiles.Controls.Add(card);
    }

    private void CenterCards()
    {
        int cell = CardSize.Width + 2 * CardMargin;
        int perRow = Math.Clamp(_tiles.ClientSize.Width / cell, 1, Math.Max(1, Math.Min(4, _tiles.Controls.Count)));
        int side = Math.Max(0, (_tiles.ClientSize.Width - perRow * cell) / 2);
        _tiles.Padding = new Padding(side, 40, side, 0);
    }

    internal void ForceLogout()
    {
        if (LoggedOut) return;
        foreach (var f in Application.OpenForms.Cast<Form>().Where(f => f != this).ToList())
            f.Close();
        LoggedOut = true;
        Close();
    }
}
