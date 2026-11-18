
using QuanLyCafe.Desktop.Forms.Catalog;
using QuanLyCafe.Desktop.Forms.Ordering;
using QuanLyCafe.Desktop.Forms.Staff;
using QuanLyCafe.Desktop.Models;
using QuanLyCafe.Desktop.Services;

namespace QuanLyCafe.Desktop.Forms;

public class frmMain : Form
{
    private readonly IAuthApi _auth;
    private readonly MenuStrip _menu = new();
    private readonly Label _hello = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 16) };
    private readonly Label _lowStock = new() { Dock = DockStyle.Bottom, Height = 36, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.DarkRed, Font = new Font("Segoe UI", 11, FontStyle.Bold) };
    private readonly Dictionary<Guid, LowStockAlertDto> _lowItems = [];

    public bool LoggedOut { get; private set; }   // true = quay về form đăng nhập

    public frmMain(IAuthApi auth)
    {
        _auth = auth;
        Text = "Quản lý quán Cafe";
        WindowState = FormWindowState.Maximized;
        _hello.Text = $"Xin chào, {Program.Session.FullName} ({Program.Session.Role})";
        Controls.Add(_hello);
        Controls.Add(_lowStock);
        Controls.Add(_menu);
        MainMenuStrip = _menu;
        BuildMenu(Program.Session.Role);
        if (Program.Session.Role == "Manager") ListenLowStock();
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

        if (manager || cashier || waiter)
            Add("Gọi món", "frmPOS", () => new frmPOS());          // SCR_004
        if (manager || cashier)
            Add("Thanh toán", "frmPayment");                       // SCR_005
        if (manager || barista)
            Add("Điều phối pha chế", "frmBar", () => new frmBar()); // SCR_006
        if (manager)
        {
            _menu.Items.Add("Sản phẩm", null, (_, _) =>
            {
                using var form = new frmProducts(Program.Api);
                form.ShowDialog(this);
            });                        // SCR_007
            _menu.Items.Add("Định lượng BOM", null, (_, _) =>
            {
                using var form = new frmBom(Program.Api);
                form.ShowDialog(this);
            });                       // SCR_008
            Add("Kho", "frmInventory");                            // SCR_009
            Add("Nhân viên", "frmStaff", () => new frmStaff());    // SCR_010
            Add("Báo cáo", "frmReport");                           // SCR_011
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

    private void Add(string text, string formName, Func<Form>? open = null)
    {
        _menu.Items.Add(text, null, (_, _) =>
        {
            if (open is null) { MessageBox.Show(this, $"{formName} chưa làm", text); return; }
            using var f = open();
            f.ShowDialog(this);
        });
    }

    internal void ForceLogout()
    {
        throw new NotImplementedException();
    }
}