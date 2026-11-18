
using QuanLyCafe.Desktop.Forms.Catalog;
using QuanLyCafe.Desktop.Services;

namespace QuanLyCafe.Desktop.Forms;

public class frmMain : Form
{
    private readonly IAuthApi _auth;
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
    }

    private void BuildMenu(string role)
    {
        bool manager = role == "Manager", cashier = role == "Cashier",
             waiter = role == "Waiter",   barista = role == "Barista";

        if (manager || cashier || waiter)
            Add("Gọi món", "frmPOS");                              // SCR_004
        if (manager || cashier)
            Add("Thanh toán", "frmPayment");                       // SCR_005
        if (manager || barista)
            Add("Điều phối pha chế", "frmBar");                    // SCR_006
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
            Add("Nhân viên", "frmStaff");                          // SCR_010
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

    private void Add(string text, string formName)
    {
        // Các form thật sẽ được nối vào ở những ngày sau
        _menu.Items.Add(text, null, (_, _) =>
            MessageBox.Show(this, $"{formName} chưa làm", text));
    }

    internal void ForceLogout()
    {
        throw new NotImplementedException();
    }
}