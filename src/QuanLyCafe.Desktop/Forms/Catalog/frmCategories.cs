using System.ComponentModel;
using QuanLyCafe.Desktop.Models;
using QuanLyCafe.Desktop.Services;

namespace QuanLyCafe.Desktop.Forms.Catalog;

// Quản lý danh mục sản phẩm (chỉ Manager). "Xoá" = ngừng dùng (IsActive=false).
public class frmCategories : Form
{
    private readonly ApiClient _api;
    private readonly BindingList<CategoryDto> _items = new();
    private List<CategoryDto> _all = [];
    private Guid? _editingId;   // null = chế độ "tạo"

    private readonly TextBox txtSearch = new() { Dock = DockStyle.Top, PlaceholderText = "Tìm danh mục..." };
    private readonly DataGridView dgvCategories = new();
    private readonly TextBox txtName = new();
    private readonly CheckBox chkActive = new();
    private readonly Button btnNew = new(), btnSave = new(), btnDelete = new(), btnRefresh = new();

    public frmCategories(ApiClient api)
    {
        _api = api;
        BuildUi();
        Load += async (_, _) => await LoadCategoriesAsync();
    }

    private void BuildUi()
    {
        Text = "Quản lý danh mục";
        Theme.Apply(this);
        Theme.Shortcuts(this, btnRefresh);
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(480, 440);

        dgvCategories.Dock = DockStyle.Top;
        dgvCategories.Height = 260;
        dgvCategories.ReadOnly = true;
        dgvCategories.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvCategories.MultiSelect = false;
        dgvCategories.AllowUserToAddRows = false;
        dgvCategories.AutoGenerateColumns = false;
        dgvCategories.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        dgvCategories.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "colName", HeaderText = "Tên danh mục", DataPropertyName = nameof(CategoryDto.CategoryName) });
        dgvCategories.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "colStatus", HeaderText = "Trạng thái", DataPropertyName = nameof(CategoryDto.IsActive) });
        dgvCategories.DataSource = _items;
        dgvCategories.SelectionChanged += (_, _) => ShowSelected();
        dgvCategories.CellFormatting += dgvCategories_CellFormatting;

        var lblName = new Label { Text = "Tên:", Left = 12, Top = 312, AutoSize = true };
        txtName.SetBounds(90, 308, 300, 25);
        txtName.MaxLength = 50;

        chkActive.Text = "Đang dùng";
        chkActive.Checked = true;
        chkActive.SetBounds(90, 342, 120, 25);

        void Setup(Button b, string text, int x, EventHandler onClick)
        { b.Text = text; b.SetBounds(x, 390, 100, 32); b.Click += onClick; }

        Setup(btnNew,     "Thêm mới", 12,  (_, _) => StartCreate());
        Setup(btnSave,    "Lưu",      120, btnSave_Click);
        Setup(btnDelete,  "Xoá",      228, btnDelete_Click);
        Setup(btnRefresh, "Làm mới",  336, async (_, _) => await LoadCategoriesAsync());

        Controls.AddRange(new Control[]
        {
            dgvCategories, lblName, txtName, chkActive,
            btnNew, btnSave, btnDelete, btnRefresh, txtSearch
        });
        txtSearch.TextChanged += (_, _) => ApplyFilter();
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            _all = await _api.GetAsync<List<CategoryDto>>("/api/categories") ?? [];
            ApplyFilter();
            if (_items.Count == 0) StartCreate();
        }
        catch (ApiException ex) { ErrorPresenter.Show(this, ex); }
    }

    private void ApplyFilter()
    {
        _items.Clear();
        foreach (var c in _all.Where(c => TextSearch.Matches(c.CategoryName, txtSearch.Text)))
            _items.Add(c);
    }

    private void ShowSelected()
    {
        if (dgvCategories.CurrentRow?.DataBoundItem is not CategoryDto c) return;
        _editingId = c.CategoryId;
        txtName.Text = c.CategoryName;
        chkActive.Checked = c.IsActive;
        btnDelete.Enabled = c.IsActive;
    }

    private void dgvCategories_CellFormatting(object? s, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || dgvCategories.Rows[e.RowIndex].DataBoundItem is not CategoryDto c) return;
        if (dgvCategories.Columns[e.ColumnIndex].Name == "colStatus")
        {
            e.Value = c.IsActive ? "Đang dùng" : "Ngừng";
            e.FormattingApplied = true;
        }
        if (!c.IsActive) e.CellStyle.ForeColor = Color.Gray;
    }

    private void StartCreate()
    {
        _editingId = null;
        dgvCategories.ClearSelection();
        txtName.Clear();
        chkActive.Checked = true;
        btnDelete.Enabled = false;
        txtName.Focus();
    }

    private async void btnSave_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtName.Text))
        { MessageBox.Show(this, "Tên danh mục không được để trống."); txtName.Focus(); return; }

        btnSave.Enabled = false;
        try
        {
            var body = new SaveCategoryRequest(txtName.Text.Trim(), chkActive.Checked);
            if (_editingId is Guid id)
                await _api.PutAsync($"/api/categories/{id}", body);
            else
                await _api.PostAsync<CategoryDto>("/api/categories", body);
            await LoadCategoriesAsync();
            StartCreate();
        }
        catch (ApiException ex) { ErrorPresenter.Show(this, ex); }
        finally { btnSave.Enabled = true; }
    }

    private async void btnDelete_Click(object? sender, EventArgs e)
    {
        if (dgvCategories.CurrentRow?.DataBoundItem is not CategoryDto c || !c.IsActive) return;
        if (MessageBox.Show(this, $"Ngừng dùng danh mục \"{c.CategoryName}\"?", "Xác nhận",
                MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

        btnDelete.Enabled = false;
        try
        {
            await _api.PutAsync($"/api/categories/{c.CategoryId}", new SaveCategoryRequest(c.CategoryName, false));
            await LoadCategoriesAsync();
        }
        catch (ApiException ex)
        {
            ErrorPresenter.Show(this, ex);
            btnDelete.Enabled = true; // lỗi thì cho bấm lại; thành công thì nạp lại lưới tự đặt trạng thái nút
        }
    }
}
