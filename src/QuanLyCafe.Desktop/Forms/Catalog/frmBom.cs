using System.ComponentModel;
using QuanLyCafe.Desktop.Models;
using QuanLyCafe.Desktop.Services;

namespace QuanLyCafe.Desktop.Forms.Catalog;

public class frmBom : Form
{
    private const decimal MaxQuantity = 999_999_999_999_999.999m;

    private readonly ApiClient _api;
    private Guid _productId;
    private readonly BindingList<BomRow> _rows = new();

    private readonly DataGridView _grid = new();
    private readonly ComboBox _materials = new();
    private readonly NumericUpDown _quantity = new();
    private readonly Button _add = new() { Text = "Thêm dòng", AutoSize = true };
    private readonly Button _remove = new() { Text = "Xóa dòng", AutoSize = true };
    private readonly Button _save = new() { Text = "Lưu BOM", AutoSize = true };


    private readonly ComboBox _products = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        DisplayMember = nameof(ProductDto.ProductName),
        Dock = DockStyle.Top
    };
    public frmBom(ApiClient api, Guid productId = default, string? productName = null)
    {
        _api = api;
        _productId = productId;
        BuildUi(productName);

        Shown += async (_, _) =>
        {
            if (_productId == Guid.Empty)
                await LoadProductsAsync();
            else
                await LoadBomAsync();
        };
    }

    private void BuildUi(string? productName)
    {
        Text = "Thiết lập BOM";
        Theme.Apply(this);
        Theme.Shortcuts(this);
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(700, 400);
        MinimumSize = new Size(720, 360);

        var title = new Label
        {
            Text = $"Sản phẩm: {productName}",
            Dock = DockStyle.Top,
            Height = 32,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _grid.Dock = DockStyle.Fill;
        _grid.AutoGenerateColumns = false;
        _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        _grid.AllowUserToAddRows = false;
        _grid.AllowUserToDeleteRows = false;
        _grid.MultiSelect = false;
        _grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _grid.Columns.AddRange(
            new DataGridViewTextBoxColumn
            {
                HeaderText = "Nguyên liệu",
                DataPropertyName = nameof(BomRow.MaterialName),
                ReadOnly = true
            },
            new DataGridViewTextBoxColumn
            {
                HeaderText = "Đơn vị",
                DataPropertyName = nameof(BomRow.Unit),
                ReadOnly = true
            },
            new DataGridViewTextBoxColumn
            {
                HeaderText = "Định lượng",
                DataPropertyName = nameof(BomRow.QuantityRequired),
                ValueType = typeof(decimal)
            });

        _grid.DataSource = _rows;
        _grid.DataError += (_, e) =>
        {
            e.ThrowException = false;
            MessageBox.Show(this, "Định lượng phải là số.");
        };

        _materials.DropDownStyle = ComboBoxStyle.DropDown;
        _materials.AutoCompleteMode = AutoCompleteMode.SuggestAppend;
        _materials.AutoCompleteSource = AutoCompleteSource.ListItems;
        _materials.DisplayMember = nameof(MaterialDto.MaterialName);
        _materials.Width = 175;

        _quantity.DecimalPlaces = 3;
        _quantity.Minimum = 0.001m;
        _quantity.Maximum = MaxQuantity;
        _quantity.Increment = 0.001m;
        _quantity.Value = 1m;
        _quantity.Width = 105;

        var close = new Button { Text = "Đóng", AutoSize = true };
        close.Click += (_, _) => Close();
        _add.Click += (_, _) => AddLine();
        _remove.Click += (_, _) => RemoveLine();
        _save.Click += async (_, _) => await SaveBomAsync();

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            Padding = new Padding(6)
        };
        actions.Controls.AddRange([_materials, _quantity, _add, _remove, _save, close]);

        Controls.Add(_grid);
        Controls.Add(actions);
        if (_productId == Guid.Empty)
        {
            title.Text = "Chọn sản phẩm cần thiết lập BOM";
            Controls.Add(_products);
            _products.SelectionChangeCommitted += async (_, _) =>
                await SelectProductAsync();
        }

        Controls.Add(title);
        SetEditingEnabled(false);
    }

    private async Task LoadProductsAsync()
    {
        try
        {
            var products = await _api.GetAsync<List<ProductDto>>("/api/products");
            if (IsDisposed)
                return;

            if (products is not { Count: > 0 })
            {
                MessageBox.Show(this, "Chưa có sản phẩm để thiết lập BOM.");
                return;
            }

            _products.DataSource = products;
            _products.SelectedIndex = -1;
        }
        catch (ApiException ex)
        {
            if (!IsDisposed)
                ErrorPresenter.Show(this, ex);
        }
    }

    private async Task SelectProductAsync()
    {
        if (_products.SelectedItem is not ProductDto product)
            return;

        _products.Enabled = false;
        _productId = product.ProductId;
        Text = $"Thiết lập BOM — {product.ProductName}";

        await LoadBomAsync();

        if (!IsDisposed && !_save.Enabled)
        {
            _products.SelectedIndex = -1;
            _products.Enabled = true;
        }
    }

    private async Task LoadBomAsync()
    {
        try
        {
            var materials = await _api.GetAsync<List<MaterialDto>>("/api/materials");
            var bom = await _api.GetAsync<List<BomLineDto>>(
                $"/api/products/{_productId}/bom");

            if (IsDisposed)
                return;

            if (materials is null || bom is null)
            {
                MessageBox.Show(this, "API chưa trả về dữ liệu nguyên liệu hoặc BOM.");
                return;
            }

            _materials.DataSource = materials;
            foreach (var line in bom)
                _rows.Add(new BomRow(line));

            SetEditingEnabled(true);
        }
        catch (ApiException ex)
        {
            if (!IsDisposed)
                ErrorPresenter.Show(this, ex);
        }
    }

    private void AddLine()
    {
        if (!_grid.EndEdit() || _materials.SelectedItem is not MaterialDto material)
            return;

        var existing = _rows.FirstOrDefault(row => row.MaterialId == material.MaterialId);

        if (existing is null)
        {
            _rows.Add(new BomRow(new BomLineDto(
                material.MaterialId, material.MaterialName, material.Unit, _quantity.Value)));
            return;
        }

        var total = existing.QuantityRequired + _quantity.Value;
        if (!IsValidQuantity(total))
        {
            MessageBox.Show(this, "Tổng định lượng không hợp lệ.");
            return;
        }

        existing.QuantityRequired = total;
        _rows.ResetItem(_rows.IndexOf(existing));
    }

    private void RemoveLine()
    {
        if (_grid.EndEdit() && _grid.CurrentRow?.DataBoundItem is BomRow row)
            _rows.Remove(row);
    }

    private async Task SaveBomAsync()
    {
        if (!_grid.EndEdit())
            return;

        if (_rows.Any(row => !IsValidQuantity(row.QuantityRequired)))
        {
            MessageBox.Show(this, "Kiểm tra định lượng từng dòng. Giá trị phải lớn hơn 0, "
                + "trong giới hạn cho phép và có tối đa 3 chữ số thập phân.");
            return;
        }

        var request = new SaveBomRequest(_rows.Select(row =>
            new BomLineRequest(row.MaterialId, row.QuantityRequired)).ToList());

        SetEditingEnabled(false);
        try
        {
            await _api.PutAsync($"/api/products/{_productId}/bom", request);

            if (!IsDisposed)
                MessageBox.Show(this, "Đã lưu BOM.");
        }
        catch (ApiException ex)
        {
            if (!IsDisposed)
                ErrorPresenter.Show(this, ex);
        }
        finally
        {
            if (!IsDisposed)
                SetEditingEnabled(true);
        }
    }

    private void SetEditingEnabled(bool enabled)
    {
        _grid.Enabled = enabled;
        _materials.Enabled = enabled;
        _quantity.Enabled = enabled;
        _add.Enabled = enabled;
        _remove.Enabled = enabled;
        _save.Enabled = enabled;
    }

    private static bool IsValidQuantity(decimal value) =>
        value > 0 && value <= MaxQuantity && decimal.Round(value, 3) == value;

    private sealed class BomRow(BomLineDto line)
    {
        public Guid MaterialId { get; } = line.MaterialId;
        public string MaterialName { get; } = line.MaterialName;
        public string Unit { get; } = line.Unit;
        public decimal QuantityRequired { get; set; } = line.QuantityRequired;
    }
}