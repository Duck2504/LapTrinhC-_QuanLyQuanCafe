using QuanLyCafe.Desktop.Forms.Ordering;
using QuanLyCafe.Desktop.Forms.Staff;
using QuanLyCafe.Desktop.Services;

namespace QuanLyCafe.Desktop.Forms;

public class frmMain : Form
{
    private readonly IAuthApi _auth;
    private BarHubClient? _hub;
    private readonly MenuStrip _menu = new();
    private readonly Label _hello = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 16) };

    public bool LoggedOut { get; private set; }   // true = quay về form đăng nhập

    public frmMain(IAuthApi auth)
    {
        _auth = auth;
        Text = "Quản lý quán Cafe";
        WindowState = FormWindowState.Maximized;
        _hello.Text = $"Xin chào, {Program.Session.FullName} ({Program.Session.Role})";
        Controls.Add(_hello);
        Controls.Add(_menu);
        MainMenuStrip = _menu;
        BuildMenu(Program.Session.Role);
        if (Program.Session.Role == "Barista")
            Load += async (_, _) => await StartBarHubAsync();
        FormClosed += async (_, _) => { if (_hub is not null) await _hub.DisposeAsync(); };
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
            Add("Điều phối pha chế", "frmBar");                    // SCR_006
        if (manager)
        {
            Add("Sản phẩm", "frmProducts");                        // SCR_007
            Add("Định lượng BOM", "frmBom");                       // SCR_008
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
        // Các form thật sẽ được nối vào ở những ngày sau
        _menu.Items.Add(text, null, (_, _) =>
        {
            if (open is null) { MessageBox.Show(this, $"{formName} chưa làm", text); return; }
            using var f = open();
            f.ShowDialog(this);
        });
    }

    // shortcut: Barista xem phiếu mới ngay trên frmMain cho mốc 2; ngày 9 chuyển sang frmBar (SCR_006).
    private async Task StartBarHubAsync()
    {
        var tickets = new ListBox { Dock = DockStyle.Bottom, Height = 240, Font = new Font("Segoe UI", 11) };
        Controls.Add(tickets);
        _hub = new BarHubClient(Program.BaseUrl, Program.Session);
        _hub.TicketCreated += t => { if (!IsDisposed) BeginInvoke(() => tickets.Items.Insert(0,
            $"{DateTime.Now:HH:mm} — {t.TableName ?? "Mang đi"}: " +
            string.Join(", ", t.Items.Select(i => $"{i.Quantity}× {i.ProductName}" + (i.Note is null ? "" : $" ({i.Note})"))))); };
        try { await _hub.StartAsync(); }
        catch (Exception ex) { MessageBox.Show(this, "Không kết nối được kênh pha chế: " + ex.Message, Text); }
    }

    internal void ForceLogout()
    {
        throw new NotImplementedException();
    }
}