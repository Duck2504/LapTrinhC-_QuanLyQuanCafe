using QuanLyCafe.Desktop.Services;

namespace QuanLyCafe.Desktop.Forms;

public class frmChangePassword : Form
{
    private readonly IAuthApi _auth;
    private readonly TextBox txtOld = new() { PlaceholderText = "Mật khẩu cũ", Width = 240, UseSystemPasswordChar = true };
    private readonly TextBox txtNew = new() { PlaceholderText = "Mật khẩu mới", Width = 240, UseSystemPasswordChar = true };
    private readonly TextBox txtConfirm = new() { PlaceholderText = "Nhập lại mật khẩu mới", Width = 240, UseSystemPasswordChar = true };
    private readonly Button btnSave = new() { Text = "Đổi mật khẩu", Width = 240, Height = 34 };

    public frmChangePassword(IAuthApi auth)
    {
        _auth = auth;
        Text = "Đổi mật khẩu";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ClientSize = new Size(300, 220);

        var panel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(30, 25, 0, 0), WrapContents = false };
        panel.Controls.AddRange(new Control[] { txtOld, txtNew, txtConfirm, btnSave });
        foreach (Control c in panel.Controls) c.Margin = new Padding(0, 0, 0, 12);
        Controls.Add(panel);

        AcceptButton = btnSave;
        btnSave.Click += async (_, _) => await SaveAsync();
    }

    private async Task SaveAsync()
    {
        foreach (var t in new[] { txtOld, txtNew, txtConfirm }) ErrorPresenter.Clear(t);

        if (txtNew.Text.Length < 6)
        { ErrorPresenter.Show(this, new ApiException(0, null, "Mật khẩu mới tối thiểu 6 ký tự"), txtNew); return; }
        if (txtNew.Text != txtConfirm.Text)
        { ErrorPresenter.Show(this, new ApiException(0, null, "Mật khẩu nhập lại không khớp"), txtConfirm); return; }

        btnSave.Enabled = false;
        try
        {
            await _auth.ChangePasswordAsync(txtOld.Text, txtNew.Text);
            MessageBox.Show(this, "Đổi mật khẩu thành công", "Thông báo");
            DialogResult = DialogResult.OK;
        }
        catch (Exception ex) { ErrorPresenter.Show(this, ex, txtOld); }
        finally { btnSave.Enabled = true; }
    }
}