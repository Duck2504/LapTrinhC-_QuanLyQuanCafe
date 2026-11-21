using QuanLyCafe.Desktop.Services;

namespace QuanLyCafe.Desktop.Forms.Staff;

// SCR_CAFE_010 — Quản lý nhân viên & phân quyền (chỉ Manager).
public class frmStaff : Form
{
    private record UserDto(Guid Id, string Username, string FullName, int RoleId, string Role, bool IsActive);
    private record RoleDto(int Id, string Name);

    private readonly DataGridView grid = new()
    {
        Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, MultiSelect = false,
        SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoGenerateColumns = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, RowHeadersVisible = false,
    };
    private readonly TextBox txtSearch = new() { Dock = DockStyle.Top, PlaceholderText = "Tìm theo tên đăng nhập / họ tên..." };
    private List<UserDto> _users = [];
    private readonly TextBox txtUsername = new() { Width = 220 };
    private readonly TextBox txtFullName = new() { Width = 220 };
    private readonly ComboBox cboRole = new() { Width = 220, DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Name", ValueMember = "Id" };
    private readonly TextBox txtPassword = new() { Width = 220, UseSystemPasswordChar = true };
    private readonly Button btnNew = new() { Text = "Mới", AutoSize = true };
    private readonly Button btnSave = new() { Text = "Lưu", AutoSize = true };
    private readonly Button btnDeactivate = new() { Text = "Vô hiệu hóa", AutoSize = true };
    private readonly Button btnResetPassword = new() { Text = "Cấp lại mật khẩu", AutoSize = true };

    private UserDto? Selected => grid.CurrentRow?.DataBoundItem as UserDto;

    public frmStaff()
    {
        Text = "Quản lý nhân viên & phân quyền";
        Theme.Apply(this);
        Theme.Shortcuts(this);
        Size = new Size(900, 500);
        StartPosition = FormStartPosition.CenterParent;

        grid.Columns.AddRange(
            new DataGridViewTextBoxColumn { HeaderText = "Tên đăng nhập", DataPropertyName = "Username" },
            new DataGridViewTextBoxColumn { HeaderText = "Họ tên", DataPropertyName = "FullName" },
            new DataGridViewTextBoxColumn { HeaderText = "Vai trò", DataPropertyName = "Role" },
            new DataGridViewCheckBoxColumn { HeaderText = "Đang hoạt động", DataPropertyName = "IsActive" });

        var fields = new TableLayoutPanel { Dock = DockStyle.Right, Width = 340, Padding = new Padding(8), ColumnCount = 2, AutoSize = true };
        AddRow(fields, "Tên đăng nhập", txtUsername);
        AddRow(fields, "Họ tên", txtFullName);
        AddRow(fields, "Vai trò", cboRole);
        AddRow(fields, "Mật khẩu", txtPassword);
        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        buttons.Controls.AddRange([btnNew, btnSave, btnDeactivate, btnResetPassword]);
        fields.Controls.Add(buttons);
        fields.SetColumnSpan(buttons, 2);

        Controls.Add(grid);
        Controls.Add(fields);
        Controls.Add(txtSearch);
        txtSearch.TextChanged += (_, _) => ApplyFilter(Selected?.Id);

        Load += async (_, _) => await RunAsync(LoadAsync);
        grid.SelectionChanged += (_, _) => ShowSelected();
        btnNew.Click += (_, _) => { grid.ClearSelection(); ShowSelected(); };
        btnSave.Click += async (_, _) => await RunAsync(SaveAsync);
        btnDeactivate.Click += async (_, _) => await RunAsync(DeactivateAsync);
        btnResetPassword.Click += async (_, _) => await RunAsync(ResetPasswordAsync);
    }

    private static void AddRow(TableLayoutPanel p, string label, Control c)
    {
        p.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left });
        p.Controls.Add(c);
    }

    private async Task LoadAsync()
    {
        cboRole.DataSource = await Program.Api.GetAsync<List<RoleDto>>("api/roles");
        await ReloadUsersAsync(null);
    }

    private async Task ReloadUsersAsync(Guid? selectId)
    {
        _users = await Program.Api.GetAsync<List<UserDto>>("api/users") ?? [];
        ApplyFilter(selectId);
    }

    private void ApplyFilter(Guid? selectId)
    {
        grid.DataSource = _users.Where(u => TextSearch.Matches(u.Username, txtSearch.Text)
                                            || TextSearch.Matches(u.FullName, txtSearch.Text)).ToList();
        grid.ClearSelection();
        var row = grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r => ((UserDto)r.DataBoundItem!).Id == selectId);
        if (row is not null) grid.CurrentCell = row.Cells[0];
        ShowSelected();
    }

    // Không chọn dòng = chế độ thêm mới: nhập username + mật khẩu.
    private void ShowSelected()
    {
        var u = grid.SelectedRows.Count > 0 ? Selected : null;
        txtUsername.Text = u?.Username ?? "";
        txtUsername.ReadOnly = u is not null;
        txtFullName.Text = u?.FullName ?? "";
        if (u is not null) cboRole.SelectedValue = u.RoleId;
        else cboRole.SelectedIndex = -1; // tạo mới phải chọn vai trò, không kế thừa vai trò dòng trước
        txtPassword.Text = "";
        txtPassword.Enabled = u is null;
        btnDeactivate.Enabled = u?.IsActive == true;
        btnResetPassword.Enabled = u is not null;
    }

    private async Task SaveAsync()
    {
        var u = grid.SelectedRows.Count > 0 ? Selected : null;
        var roleId = cboRole.SelectedValue as int? ?? 0;
        var saved = u is null
            ? await Program.Api.PostAsync<UserDto>("api/users", new { username = txtUsername.Text, password = txtPassword.Text, fullName = txtFullName.Text, roleId })
            : await Program.Api.PutAsync<UserDto>($"api/users/{u.Id}", new { fullName = txtFullName.Text, roleId });
        await ReloadUsersAsync(saved?.Id);
        MessageBox.Show(this, "Đã lưu.", Text);
    }

    private async Task DeactivateAsync()
    {
        if (Selected is not { } u) return;
        if (MessageBox.Show(this, $"Vô hiệu hóa tài khoản '{u.Username}'? Người này sẽ không đăng nhập được nữa.",
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
        await Program.Api.PostAsync($"api/users/{u.Id}/deactivate");
        await ReloadUsersAsync(u.Id);
    }

    private async Task ResetPasswordAsync()
    {
        if (Selected is not { } u) return;
        var newPassword = Prompt($"Mật khẩu mới cho '{u.Username}' (ít nhất 6 ký tự):");
        if (string.IsNullOrEmpty(newPassword)) return;
        await Program.Api.PostAsync($"api/users/{u.Id}/reset-password", new { newPassword });
        MessageBox.Show(this, "Đã cấp lại mật khẩu.", Text);
    }

    private string? Prompt(string message)
    {
        using var dlg = new Form { Text = Text, Size = new Size(360, 150), StartPosition = FormStartPosition.CenterParent, FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false, MaximizeBox = false };
        var txt = new TextBox { Left = 12, Top = 36, Width = 320, UseSystemPasswordChar = true };
        var ok = new Button { Text = "OK", Left = 176, Top = 70, DialogResult = DialogResult.OK };
        var cancel = new Button { Text = "Hủy", Left = 257, Top = 70, DialogResult = DialogResult.Cancel };
        dlg.Controls.AddRange([new Label { Text = message, Left = 12, Top = 12, AutoSize = true }, txt, ok, cancel]);
        dlg.AcceptButton = ok;
        dlg.CancelButton = cancel;
        return dlg.ShowDialog(this) == DialogResult.OK ? txt.Text : null;
    }

    // Khóa nút khi đang gọi API; lỗi hiện qua ErrorPresenter theo mã ERR-*.
    private async Task RunAsync(Func<Task> action)
    {
        UseWaitCursor = true;
        Enabled = false;
        try { await action(); }
        catch (Exception ex) { ErrorPresenter.Show(this, ex); }
        finally { Enabled = true; UseWaitCursor = false; }
    }
}
