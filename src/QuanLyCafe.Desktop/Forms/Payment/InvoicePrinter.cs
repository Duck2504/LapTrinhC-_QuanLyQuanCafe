using System.Drawing.Printing;
using System.Globalization;
using QuanLyCafe.Desktop.Models;

namespace QuanLyCafe.Desktop.Forms.Payment;

internal static class InvoicePrinter
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    internal static string Money(decimal amount) => $"{amount.ToString("#,0.##", Vietnamese)} đ";

    internal static string MethodName(PaymentMethod? method) => method switch
    {
        PaymentMethod.Cash => "Tiền mặt",
        PaymentMethod.Momo => "MoMo",
        PaymentMethod.VNPay => "VNPay",
        PaymentMethod.Card => "Thẻ",
        _ => "—"
    };

    internal static void ShowPreview(IWin32Window owner, InvoiceDto invoice)
    {
        var details = new List<string>
        {
            $"Mã đơn  {invoice.OrderId}",
            $"Thanh toán lúc  {invoice.PaidAt:HH:mm dd/MM/yyyy}",
            $"Phục vụ  {invoice.TableName ?? "Mang đi"}",
            $"Thu ngân  {invoice.CashierName ?? "—"}",
            $"Phương thức  {MethodName(invoice.PaymentMethod)}",
            ""
        };
        foreach (var line in invoice.Lines)
        {
            details.Add(line.ProductName);
            details.Add($"    {line.Quantity} × {Money(line.UnitPrice)} = {Money(line.LineTotal)}");
            if (!string.IsNullOrWhiteSpace(line.Note))
                details.Add($"    Ghi chú  {line.Note}");
        }
        details.Add("");
        details.Add($"TỔNG TIỀN  {Money(invoice.TotalAmount)}");

        var body = string.Join(Environment.NewLine, details);
        using var document = new PrintDocument { DocumentName = $"Hóa đơn {invoice.OrderId}" };
        document.DefaultPageSettings.Margins = new Margins(25, 25, 25, 25);
        using var titleFont = new Font("Segoe UI", 16, FontStyle.Bold);
        using var bodyFont = new Font("Segoe UI", 10);
        using var format = new StringFormat { FormatFlags = StringFormatFlags.LineLimit };
        var offset = 0;
        document.BeginPrint += (_, _) => offset = 0;
        document.PrintPage += (sender, page) =>
        {
            var graphics = page.Graphics!;
            var bounds = page.MarginBounds;
            const string title = "HÓA ĐƠN THANH TOÁN";
            var titleBounds = new RectangleF(bounds.Left, bounds.Top, bounds.Width, bounds.Height);
            var titleHeight = graphics.MeasureString(title, titleFont, bounds.Width).Height;
            graphics.DrawString(title, titleFont, Brushes.Black, titleBounds);
            var top = bounds.Top + titleHeight + 12;
            var bodyBounds = new RectangleF(bounds.Left, top, bounds.Width, bounds.Bottom - top);
            if (bodyBounds.Width <= 0 || bodyBounds.Height < bodyFont.GetHeight(graphics))
                throw new InvalidOperationException("Vùng in quá nhỏ để hiển thị hóa đơn.");

            var remaining = body[offset..];
            graphics.MeasureString(remaining, bodyFont, bodyBounds.Size, format, out var fitted, out _);
            if (fitted == 0)
                throw new InvalidOperationException("Không thể dàn trang hóa đơn với khổ giấy hiện tại.");

            graphics.DrawString(remaining[..fitted], bodyFont, Brushes.Black, bodyBounds, format);
            offset += fitted;
            page.HasMorePages = offset < body.Length;
        };

        using var preview = new PrintPreviewDialog { Document = document, Width = 950, Height = 700 };
        preview.ShowDialog(owner);
    }
}
