using QuanLyCafe.Desktop.Services;

namespace QuanLyCafe.Desktop.Forms;

public class frmLogin : Form
{
    private readonly IAuthApi _auth;
    private readonly TextBox txtUsername = new() { PlaceholderText = "Tên đăng nhập", Width = 240 };
    private readonly TextBox txtPassword = new() { PlaceholderText = "Mật khẩu", Width = 240, UseSystemPasswordChar = true };
    private readonly Button btnLogin = new() { Text = "Đăng nhập", Width = 240, Height = 34 };

    public frmLogin(IAuthApi auth)
    {
        _auth = auth;
        Text = "Đăng nhập";
        Theme.Apply(this);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        ClientSize = new Size(300, 220);

        var title = new Label { Text = "QUẢN LÝ QUÁN CAFE", Font = new Font(Font.FontFamily, 13, FontStyle.Bold), AutoSize = true };
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(30, 25, 0, 0), WrapContents = false
        };
        panel.Controls.AddRange(new Control[] { title, txtUsername, txtPassword, btnLogin });
        foreach (Control c in panel.Controls) c.Margin = new Padding(0, 0, 0, 12);
        Controls.Add(panel);

        AcceptButton = btnLogin;                 // Enter = đăng nhập
        btnLogin.Click += async (_, _) => await DoLoginAsync();
    }

    private async Task DoLoginAsync()
    {
        ErrorPresenter.Clear(txtUsername);
        ErrorPresenter.Clear(txtPassword);
        if (string.IsNullOrWhiteSpace(txtUsername.Text) || string.IsNullOrEmpty(txtPassword.Text))
        {
            ErrorPresenter.Show(this, new ApiException(0, null, "Vui lòng nhập tên đăng nhập và mật khẩu"), txtUsername);
            return;
        }

        btnLogin.Enabled = false;
        try
        {
            var res = await _auth.LoginAsync(txtUsername.Text.Trim(), txtPassword.Text);
            Program.Session.Set(res.Token, res.FullName, res.Role);
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex)
        {
            // Giữ lại tên đăng nhập, chỉ xóa mật khẩu (ERR-AUTH-001)
            txtPassword.Clear();
            ErrorPresenter.Show(this, ex, txtPassword);
            txtPassword.Focus();
        }
        finally { btnLogin.Enabled = true; }
    }
}