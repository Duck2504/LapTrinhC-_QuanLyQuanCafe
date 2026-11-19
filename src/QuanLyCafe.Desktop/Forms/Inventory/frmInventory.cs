using QuanLyCafe.Desktop.Models;
using QuanLyCafe.Desktop.Services;

namespace QuanLyCafe.Desktop.Forms.Inventory;

public class frmInventory : Form
{
    private readonly ApiClient _api;
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
    private readonly List<ComboBox> _materialPickers = new();
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        ReadOnly = true,
        AutoGenerateColumns = false,
        AllowUserToAddRows = false,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
    };

    public frmInventory(ApiClient api)
    {
        _api = api;
        Text = "Quản lý kho";
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(760, 420);

        AddStockTab();

        AddEntryTab("Nhập", "Số lượng nhập", 0.001m,
            async (id, quantity, note) =>
            {
                await _api.PostAsync("/api/stock/receipts",
                    new StockReceiptRequest(id, quantity, note));
                return "Đã nhập kho.";
            });

        AddEntryTab("Xuất", "Số lượng xuất", 0.001m,
            async (id, quantity, note) =>
            {
                await _api.PostAsync("/api/stock/issues",
                    new StockIssueRequest(id, quantity, note));
                return "Đã xuất kho.";
            });

        AddEntryTab("Kiểm kê", "Số lượng thực tế", 0m,
            async (id, quantity, note) =>
            {
                var result = await _api.PostAsync<StocktakeResultDto>(
                    "/api/stock/stocktakes",
                    new StocktakeRequest(id, quantity, note));

                return result is null
                    ? "API không trả kết quả kiểm kê."
                    : $"Tồn trước kiểm kê {result.SystemQuantity}\n"
                      + $"Thực tế {result.ActualQuantity}\n"
                      + $"Chênh lệch {result.Difference:+0.###;-0.###;0}";
            });

        Controls.Add(_tabs);
        Shown += async (_, _) => await RefreshStockAsync();
    }

    private void AddStockTab()
    {
        _grid.Columns.AddRange(
            Column("Nguyên liệu", nameof(MaterialDto.MaterialName)),
            Column("Đơn vị", nameof(MaterialDto.Unit)),
            Column("Tồn hiện tại", nameof(MaterialDto.StockQuantity)),
            Column("Mức tối thiểu", nameof(MaterialDto.MinQuantity)));

        _grid.DataBindingComplete += (_, _) =>
        {
            foreach (DataGridViewRow row in _grid.Rows)
            {
                if (row.DataBoundItem is MaterialDto { IsLow: true })
                    row.DefaultCellStyle.BackColor = Color.MistyRose;
            }
        };

        var tab = new TabPage("Tồn kho");
        tab.Controls.Add(_grid);
        _tabs.TabPages.Add(tab);
    }

    private static DataGridViewTextBoxColumn Column(string title, string property) =>
        new() { HeaderText = title, DataPropertyName = property };

    private void AddEntryTab(
        string title, string quantityLabel, decimal minimum,
        Func<Guid, decimal, string?, Task<string>> submitAsync)
    {
        var material = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            DisplayMember = nameof(MaterialDto.MaterialName),
            Width = 260
        };
        _materialPickers.Add(material);

        var quantity = new NumericUpDown
        {
            Minimum = minimum,
            Maximum = 999_999_999_999_999.999m,
            DecimalPlaces = 3,
            Value = minimum,
            Width = 180
        };

        var note = new TextBox { Width = 260, MaxLength = 200 };
        var save = new Button { Text = "Xác nhận", AutoSize = true };

        save.Click += async (_, _) =>
        {
            if (material.SelectedItem is not MaterialDto selected)
            {
                MessageBox.Show(this, "Bạn cần chọn nguyên liệu.");
                return;
            }

            _tabs.Enabled = false;
            try
            {
                var message = await submitAsync(
                    selected.MaterialId, quantity.Value, note.Text.Trim());

                if (IsDisposed)
                    return;

                MessageBox.Show(this, message);
                note.Clear();
                await LoadStockAsync();
            }
            catch (ApiException ex)
            {
                if (!IsDisposed)
                    ErrorPresenter.Show(this, ex);
            }
            finally
            {
                if (!IsDisposed)
                    _tabs.Enabled = true;
            }
        };

        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(16),
            WrapContents = false
        };

        panel.Controls.AddRange([
            new Label { Text = "Nguyên liệu", AutoSize = true }, material,
            new Label { Text = quantityLabel, AutoSize = true }, quantity,
            new Label { Text = "Ghi chú", AutoSize = true }, note, save
        ]);

        var tab = new TabPage(title);
        tab.Controls.Add(panel);
        _tabs.TabPages.Add(tab);
    }

    private async Task RefreshStockAsync()
    {
        _tabs.Enabled = false;
        try
        {
            await LoadStockAsync();
        }
        catch (ApiException ex)
        {
            if (!IsDisposed)
                ErrorPresenter.Show(this, ex);
        }
        finally
        {
            if (!IsDisposed)
                _tabs.Enabled = true;
        }
    }

    private async Task LoadStockAsync()
    {
        var materials = await _api.GetAsync<List<MaterialDto>>("/api/materials");
        if (IsDisposed)
            return;

        _grid.DataSource = materials;

        foreach (var picker in _materialPickers)
        {
            var selectedId = (picker.SelectedItem as MaterialDto)?.MaterialId;
            picker.DataSource = materials?.ToList();
            picker.SelectedIndex = materials?.FindIndex(m => m.MaterialId == selectedId) ?? -1;
        }
    }
}