using System.ComponentModel;
using QuanLyCafe.Desktop.Models;
using QuanLyCafe.Desktop.Services;

namespace QuanLyCafe.Desktop.Forms.Catalog;

public class frmProducts : Form
{
    private readonly ApiClient _api;
    private readonly BindingList<ProductDto> _items = new();
    private List<ProductDto> _all = [];
    private readonly TextBox txtSearch = new() { Dock = DockStyle.Top, PlaceholderText = "Tìm sản phẩm..." };
    private Guid? _editingId;   // null = chế độ "tạo"

    private readonly DataGridView dgvProducts = new();
    private readonly TextBox txtName = new();
    private readonly NumericUpDown numPrice = new();
    private readonly CheckBox chkActive = new();
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
        ClientSize = new Size(640, 512);

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

        // Nút
        void Setup(Button b, string text, int x, EventHandler onClick)
        { b.Text = text; b.SetBounds(x, 457, 100, 32); b.Click += onClick; }

        Setup(btnNew,     "Thêm mới", 12,  btnNew_Click);
        Setup(btnSave,    "Lưu",      120, btnSave_Click);
        Setup(btnDelete,  "Xoá",      228, btnDelete_Click);
        Setup(btnRefresh, "Làm mới",  336, btnRefresh_Click);
        Setup(btnBom, "Thiết lập BOM…", 444, BtnBom_Click);
        btnBom.AutoSize = true;
        btnBom.Enabled = false;   // để Ngày 5

        Controls.AddRange(new Control[]
        {
            dgvProducts, lblName, txtName, lblPrice, numPrice, chkActive,
            btnNew, btnSave, btnDelete, btnRefresh, btnBom, txtSearch
        });
        txtSearch.TextChanged += (_, _) => ApplyFilter();
    }

    
    private async Task LoadProductsAsync()
    {
        try
        {
            var list = await _api.GetAsync<List<ProductDto>>("/api/products");
            _all = list ?? [];
            ApplyFilter();
            if (_items.Count == 0) StartCreate();
        }
        catch (ApiException ex) { ErrorPresenter.Show(this, ex); }
    }

    private void ApplyFilter()
    {
        _items.Clear();
        foreach (var p in _all.Where(p => TextSearch.Matches(p.ProductName, txtSearch.Text)))
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

        if (_editingId is Guid productId)
        {
            await _api.PutAsync(
                $"/api/products/{productId}",
                new SaveProductRequest(name, price, chkActive.Checked));
        }
        else
        {
            await _api.PostAsync<ProductDto>(
                "/api/products",
                new SaveProductRequest(name, price, true));
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
                new SaveProductRequest(product.ProductName, product.Price, false));

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