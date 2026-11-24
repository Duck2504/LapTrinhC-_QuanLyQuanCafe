using System.Drawing.Printing;
using QRCoder;
using QuanLyCafe.Desktop.Models;
using QuanLyCafe.Desktop.Services;

namespace QuanLyCafe.Desktop.Forms.Catalog;

// Mã QR gọi món tại bàn (chỉ Manager): khách quét → {WebBaseUrl}/Ban/{tableId}.
public class frmTableQr : Form
{
    private readonly ApiClient _api;
    private readonly ListBox lstTables = new() { Dock = DockStyle.Left, Width = 200, DisplayMember = nameof(TableDto.Name), IntegralHeight = false };
    private readonly PictureBox picQr = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.White };
    private readonly TextBox txtUrl = new() { Dock = DockStyle.Fill, ReadOnly = true };
    // Điện thoại khách không mở được "localhost" của máy chủ → mặc định dùng IP LAN, Manager sửa được (IP khác, tên miền).
    private readonly TextBox txtBase = new() { Dock = DockStyle.Fill, Text = DefaultWebBase() };
    private readonly Button btnCopy = new() { Text = "Copy", Dock = DockStyle.Right, Width = 80 };
    private readonly Button btnSave = new() { Text = "Lưu PNG", AutoSize = true };
    private readonly Button btnPrint = new() { Text = "In", AutoSize = true };
    private readonly Button btnRefresh = new() { Text = "Làm mới", AutoSize = true };
    private byte[]? _png;

    private TableDto? Selected => lstTables.SelectedItem as TableDto;

    public frmTableQr(ApiClient api)
    {
        _api = api;
        Text = "Mã QR gọi món tại bàn";
        Theme.Apply(this);
        Theme.Shortcuts(this, btnRefresh);
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(640, 520);

        var urlBar = new Panel { Dock = DockStyle.Bottom, Height = txtUrl.PreferredHeight + 8, Padding = new Padding(8, 4, 8, 4) };
        urlBar.Controls.Add(txtUrl);
        urlBar.Controls.Add(btnCopy);
        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, Padding = new Padding(4) };
        buttons.Controls.AddRange([btnSave, btnPrint, btnRefresh]);

        var baseBar = new Panel { Dock = DockStyle.Top, Height = txtBase.PreferredHeight + 8, Padding = new Padding(8, 4, 8, 4) };
        baseBar.Controls.Add(txtBase);
        baseBar.Controls.Add(new Label { Text = "Địa chỉ Web:", Dock = DockStyle.Left, AutoSize = true, Padding = new Padding(0, 4, 6, 0) });
        txtBase.TextChanged += (_, _) => ShowQr();

        Controls.Add(picQr);
        Controls.Add(baseBar);
        Controls.Add(urlBar);
        Controls.Add(buttons);
        Controls.Add(lstTables);

        lstTables.SelectedIndexChanged += (_, _) => ShowQr();
        btnCopy.Click += (_, _) => { if (txtUrl.TextLength > 0) Clipboard.SetText(txtUrl.Text); };
        btnSave.Click += (_, _) => SavePng();
        btnPrint.Click += (_, _) => Print();
        btnRefresh.Click += async (_, _) => await LoadTablesAsync();
        Load += async (_, _) => await LoadTablesAsync();
        FormClosed += (_, _) => picQr.Image?.Dispose();
    }

    private async Task LoadTablesAsync()
    {
        btnRefresh.Enabled = false;
        try
        {
            var tables = await _api.GetAsync<List<TableDto>>("/api/tables") ?? [];
            if (IsDisposed) return;
            lstTables.DataSource = tables.OrderBy(t => t.Name).ToList();
            ShowQr();
        }
        catch (ApiException ex) { if (!IsDisposed) ErrorPresenter.Show(this, ex); }
        finally { if (!IsDisposed) btnRefresh.Enabled = true; }
    }

    private string UrlFor(TableDto t) => $"{txtBase.Text.Trim().TrimEnd('/')}/Ban/{t.Id}";

    private static string DefaultWebBase()
    {
        try
        {
            var ip = System.Net.Dns.GetHostAddresses(System.Net.Dns.GetHostName())
                .FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork && !System.Net.IPAddress.IsLoopback(a));
            return ip is null ? Program.WebBaseUrl : Program.WebBaseUrl.Replace("localhost", ip.ToString());
        }
        catch (System.Net.Sockets.SocketException) { return Program.WebBaseUrl; }
    }

    private void ShowQr()
    {
        picQr.Image?.Dispose();
        picQr.Image = null;
        _png = null;
        txtUrl.Text = "";
        btnSave.Enabled = btnPrint.Enabled = btnCopy.Enabled = Selected is not null;
        if (Selected is not { } t) return;

        txtUrl.Text = UrlFor(t);
        using var data = QRCodeGenerator.GenerateQrCode(txtUrl.Text, QRCodeGenerator.ECCLevel.M);
        _png = new PngByteQRCode(data).GetGraphic(20);
        using var ms = new MemoryStream(_png);
        using var img = Image.FromStream(ms);
        picQr.Image = new Bitmap(img); // chép ra để không phụ thuộc stream đã đóng
    }

    private void SavePng()
    {
        if (_png is null || Selected is not { } t) return;
        using var dlg = new SaveFileDialog { Filter = "Ảnh PNG|*.png", FileName = $"QR-{t.Name}.png" };
        if (dlg.ShowDialog(this) != DialogResult.OK) return;
        try { File.WriteAllBytes(dlg.FileName, _png); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { MessageBox.Show(this, "Không lưu được file: " + ex.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
    }

    private void Print()
    {
        if (picQr.Image is not { } qr || Selected is not { } t) return;
        var url = txtUrl.Text;
        using var doc = new PrintDocument { DocumentName = $"QR {t.Name}" };
        using var titleFont = new Font("Segoe UI", 20, FontStyle.Bold);
        using var bodyFont = new Font("Segoe UI", 10);
        using var center = new StringFormat { Alignment = StringAlignment.Center };
        doc.PrintPage += (_, e) =>
        {
            var g = e.Graphics!;
            var b = e.MarginBounds;
            g.DrawString(t.Name, titleFont, Brushes.Black, new RectangleF(b.Left, b.Top, b.Width, 50), center);
            g.DrawString("Quét mã để gọi món", bodyFont, Brushes.Black, new RectangleF(b.Left, b.Top + 50, b.Width, 25), center);
            int size = Math.Min(b.Width, 400);
            g.DrawImage(qr, b.Left + (b.Width - size) / 2, b.Top + 85, size, size);
            g.DrawString(url, bodyFont, Brushes.Black, new RectangleF(b.Left, b.Top + 95 + size, b.Width, 40), center);
        };
        using var preview = new PrintPreviewDialog { Document = doc, Width = 800, Height = 700 };
        preview.ShowDialog(this);
    }
}
