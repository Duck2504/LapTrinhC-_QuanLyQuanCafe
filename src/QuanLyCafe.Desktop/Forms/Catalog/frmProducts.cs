using System.ComponentModel;
using QuanLyCafe.Desktop.Models;
using QuanLyCafe.Desktop.Services;

namespace QuanLyCafe.Desktop.Forms.Catalog;

public class frmProducts : Form
{
    private readonly ApiClient _api;
    private readonly BindingList<ProductDto> _items = new();
    private List<ProductDto> _all = [];
    private readonly TextBox txtSearch = new() { Dock = DockStyle.Fill, PlaceholderText = "Tìm sản phẩm..." };
    private readonly ComboBox cboFilter = new() { Dock = DockStyle.Right, Width = 220, DropDownStyle = ComboBoxStyle.DropDownList };
    private static readonly CategoryDto NoCategory = new(Guid.Empty, "(Không có)", true);
    private static readonly CategoryDto AllCategories = new(Guid.Empty, "(Tất cả danh mục)", true);
    private List<CategoryDto> _activeCategories = [];
    private Guid? _editingId;   // null = chế độ "tạo"

    private readonly DataGridView dgvProducts = new();
    private readonly TextBox txtName = new();
    private readonly NumericUpDown numPrice = new();
    private readonly CheckBox chkActive = new();
    private readonly ComboBox cboCategory = new() { DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = nameof(CategoryDto.CategoryName) };
    private readonly TextBox txtImageUrl = new() { MaxLength = 300, PlaceholderText = "https://... hoặc /images/..." };
    private readonly TextBox txtDescription = new() { Multiline = true, ScrollBars = ScrollBars.Vertical, MaxLength = 500 };
    private readonly Button btnNew = new(), btnSave = new(), btnDelete = new(),
                            btnRefresh = new(), btnBom = new();

    public frmProducts(ApiClient api)
    {
        _api = api;
        BuildUi();
        Load += async (_, _) => await LoadProductsAsync();
    }

    private void BuildUi()
    {
        Text = "Quản lý sản phẩm";
        Theme.Apply(this);
        Theme.Shortcuts(this, btnRefresh);
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(820, 545);

        // Grid
        dgvProducts.Dock = DockStyle.Top;
        dgvProducts.Height = 300;
        dgvProducts.ReadOnly = true;
        dgvProducts.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        dgvProducts.MultiSelect = false;
        dgvProducts.AllowUserToAddRows = false;
        dgvProducts.AutoGenerateColumns = false;
        dgvProducts.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        dgvProducts.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "colName", HeaderText = "Tên", DataPropertyName = "ProductName" });
        dgvProducts.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "colPrice", HeaderText = "Giá", DataPropertyName = "Price",
              DefaultCellStyle = { Format = "N0" } });
        dgvProducts.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "colCategory", HeaderText = "Danh mục", DataPropertyName = "CategoryName" });
        dgvProducts.Columns.Add(new DataGridViewTextBoxColumn
            { Name = "colStatus", HeaderText = "Trạng thái", DataPropertyName = "IsActive" });
        dgvProducts.DataSource = _items;
        dgvProducts.SelectionChanged += dgvProducts_SelectionChanged;
        dgvProducts.CellFormatting   += dgvProducts_CellFormatting;

        // Ô nhập
        var lblName = new Label { Text = "Tên:", Left = 12, Top = 350, AutoSize = true };
        txtName.SetBounds(90, 346, 300, 25);
        txtName.MaxLength = 100;

        var lblPrice = new Label { Text = "Giá:", Left = 12, Top = 384, AutoSize = true };
        numPrice.SetBounds(90, 380, 150, 25);
        numPrice.Minimum = 0;
        numPrice.Maximum = 100_000_000;
        numPrice.DecimalPlaces = 0;
        numPrice.ThousandsSeparator = true;

        chkActive.Text = "Đang bán";
        chkActive.Checked = true;
        chkActive.SetBounds(90, 414, 120, 25);

        var lblCategory = new Label { Text = "Danh mục:", Left = 410, Top = 350, AutoSize = true };
        cboCategory.SetBounds(500, 346, 300, 25);
        var lblImage = new Label { Text = "Link ảnh:", Left = 410, Top = 384, AutoSize = true };
        txtImageUrl.SetBounds(500, 380, 300, 25);
        var lblDesc = new Label { Text = "Mô tả:", Left = 410, Top = 418, AutoSize = true };
        txtDescription.SetBounds(500, 414, 300, 66);

        // Nút
        void Setup(Button b, string text, int x, EventHandler onClick)
        { b.Text = text; b.SetBounds(x, 497, 100, 32); b.Click += onClick; }

        Setup(btnNew,     "Thêm mới", 12,  btnNew_Click);
        Setup(btnSave,    "Lưu",      120, btnSave_Click);
        Setup(btnDelete,  "Xoá",      228, btnDelete_Click);
        Setup(btnRefresh, "Làm mới",  336, btnRefresh_Click);
        Setup(btnBom, "Thiết lập BOM…", 444, BtnBom_Click);
        btnBom.AutoSize = true;
        btnBom.Enabled = false;   // để Ngày 5

        var searchBar = new Panel { Dock = DockStyle.Top, Height = txtSearch.PreferredHeight };
        searchBar.Controls.Add(txtSearch);
        searchBar.Controls.Add(cboFilter);
        cboFilter.DisplayMember = nameof(CategoryDto.CategoryName);

        Controls.AddRange(new Control[]
        {
            dgvProducts, lblName, txtName, lblPrice, numPrice, chkActive,
            lblCategory, cboCategory, lblImage, txtImageUrl, lblDesc, txtDescription,
            btnNew, btnSave, btnDelete, btnRefresh, btnBom, searchBar
        });
        txtSearch.TextChanged += (_, _) => ApplyFilter();
        cboFilter.SelectedIndexChanged += (_, _) => ApplyFilter();
    }

    
    private async Task LoadProductsAsync()
    {
        try
        {
            var list = await _api.GetAsync<List<ProductDto>>("/api/products");
            var categories = await _api.GetAsync<List<CategoryDto>>("/api/categories") ?? [];
            _all = list ?? [];
            _activeCategories = categories.Where(c => c.IsActive).OrderBy(c => c.CategoryName).ToList();
            var filterId = (cboFilter.SelectedItem as CategoryDto)?.CategoryId;
            cboFilter.DataSource = new[] { AllCategories }.Concat(categories.OrderBy(c => c.CategoryName)).ToList();
            cboFilter.SelectedItem = categories.FirstOrDefault(c => c.CategoryId == filterId) ?? AllCategories;
            SetCategoryChoices(null);
            ApplyFilter();
            if (_items.Count == 0) StartCreate();
        }
        catch (ApiException ex) { ErrorPresenter.Show(this, ex); }
    }

    private void ApplyFilter()
    {
        _items.Clear();
        var categoryId = cboFilter.SelectedItem is CategoryDto c && c != AllCategories ? c.CategoryId : (Guid?)null;
        foreach (var p in _all.Where(p => TextSearch.Matches(p.ProductName, txtSearch.Text)
                                          && (categoryId is null || p.CategoryId == categoryId)))
            _items.Add(p);
    }

    private void dgvProducts_SelectionChanged(object? s, EventArgs e)
    {
        btnBom.Enabled = dgvProducts.SelectedRows.Count > 0;
        if (dgvProducts.CurrentRow?.DataBoundItem is not ProductDto p) return;
        _editingId = p.ProductId;
        txtName.Text = p.ProductName;
        numPrice.Value = p.Price;
        chkActive.Checked = p.IsActive;
        btnDelete.Enabled = p.IsActive;
        SetCategoryChoices(p);
        txtImageUrl.Text = p.ImageUrl ?? "";
        txtDescription.Text = p.Description ?? "";
    }

    // Combo = "(Không có)" + danh mục đang dùng; sản phẩm thuộc danh mục đã ngừng thì thêm riêng danh mục đó
    // để bấm Lưu không gỡ sản phẩm khỏi danh mục (API chỉ chặn gán MỚI vào danh mục ngừng).
    private void SetCategoryChoices(ProductDto? p)
    {
        var choices = new List<CategoryDto> { NoCategory };
        choices.AddRange(_activeCategories);
        var current = _activeCategories.FirstOrDefault(c => c.CategoryId == p?.CategoryId);
        if (current is null && p?.CategoryId is Guid oldId)
        {
            current = new CategoryDto(oldId, $"{p.CategoryName} (ngừng)", false);
            choices.Add(current);
        }
        cboCategory.DataSource = choices;
        cboCategory.SelectedItem = current ?? NoCategory;
    }

    private void dgvProducts_CellFormatting(object? s, DataGridViewCellFormattingEventArgs e)
    {
        if (e.RowIndex < 0 || dgvProducts.Rows[e.RowIndex].DataBoundItem is not ProductDto p) return;
        if (dgvProducts.Columns[e.ColumnIndex].Name == "colStatus")
        {
            e.Value = p.IsActive ? "Đang bán" : "Ngừng bán";
            e.FormattingApplied = true;
        }
        if (!p.IsActive) e.CellStyle.ForeColor = Color.Gray;
    }

    private void StartCreate()
    {
        _editingId = null;
        dgvProducts.ClearSelection();
        btnBom.Enabled = false;
        txtName.Clear();
        numPrice.Value = 0;
        chkActive.Checked = true;
        SetCategoryChoices(null);
        txtImageUrl.Clear();
        txtDescription.Clear();
        btnDelete.Enabled = false;
        txtName.Focus();
    }

    private void btnNew_Click(object? sender, EventArgs e) => StartCreate();
    private async void btnRefresh_Click(object? sender, EventArgs e) => await LoadProductsAsync();

    private async void btnSave_Click(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtName.Text))
        { MessageBox.Show("Tên sản phẩm không được để trống."); txtName.Focus(); return; }

        btnSave.Enabled = false;
        try
        {
            var name = txtName.Text.Trim();
        var price = numPrice.Value;
        var categoryId = cboCategory.SelectedItem is CategoryDto c && c != NoCategory ? c.CategoryId : (Guid?)null;
        var description = string.IsNullOrWhiteSpace(txtDescription.Text) ? null : txtDescription.Text.Trim();
        var imageUrl = string.IsNullOrWhiteSpace(txtImageUrl.Text) ? null : txtImageUrl.Text.Trim();

        if (_editingId is Guid productId)
        {
            await _api.PutAsync(
                $"/api/products/{productId}",
                new SaveProductRequest(name, price, chkActive.Checked, categoryId, description, imageUrl));
        }
        else
        {
            await _api.PostAsync<ProductDto>(
                "/api/products",
                new SaveProductRequest(name, price, true, categoryId, description, imageUrl));
        }
            await LoadProductsAsync();
            StartCreate();
        }
        catch (ApiException ex) { ErrorPresenter.Show(this, ex); }
        finally { btnSave.Enabled = true; }
    }

    private async void btnDelete_Click(object? sender, EventArgs e)
    {
        if (dgvProducts.SelectedRows.Count == 0 ||
        dgvProducts.SelectedRows[0].DataBoundItem is not ProductDto product ||
        !product.IsActive)
        return;

        var answer = MessageBox.Show(
            this, $"Ngừng bán sản phẩm \"{product.ProductName}\"?",
            "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

        if (answer != DialogResult.Yes)
            return;

        try
        {
            await _api.PutAsync(
                $"/api/products/{product.ProductId}",
                new SaveProductRequest(product.ProductName, product.Price, false,
                    // giữ nguyên danh mục/mô tả/ảnh (API cho giữ danh mục cũ dù đã ngừng)
                    product.CategoryId,
                    product.Description, product.ImageUrl));

            await LoadProductsAsync();
        }
        catch (ApiException ex)
        {
            ErrorPresenter.Show(this, ex);
        }
    }
    private void BtnBom_Click(object? sender, EventArgs e)
    {
        if (dgvProducts.SelectedRows.Count == 0 ||
            dgvProducts.SelectedRows[0].DataBoundItem is not ProductDto product)
            return;

        using var form = new frmBom(_api, product.ProductId, product.ProductName);
        form.ShowDialog(this);
    }

}