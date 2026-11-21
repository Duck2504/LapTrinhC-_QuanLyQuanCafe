using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace QuanLyCafe.Web.Pages;

// Khách quét QR trên bàn → /Ban/{tableId}: xem món đang gọi và gọi thêm món.
public class BanModel(CafeApi api) : PageModel
{
    public class LineInput
    {
        public Guid ProductId { get; set; }
        public int Quantity { get; set; }
        public string? Note { get; set; }
    }

    [BindProperty] public List<LineInput> Lines { get; set; } = [];

    public GuestTableDto Table { get; private set; } = null!;
    public List<MenuItemDto> Menu { get; private set; } = [];
    public string? Error { get; private set; }
    [TempData] public string? Success { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        if (!await LoadAsync(id, ct)) return NotFound();
        // Nhớ bàn để trang chi tiết món có nút quay lại gọi món.
        Response.Cookies.Append("table", id.ToString(), new CookieOptions
            { HttpOnly = true, SameSite = SameSiteMode.Lax, MaxAge = TimeSpan.FromHours(4) });
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id, CancellationToken ct)
    {
        var items = Lines.Where(l => l.Quantity > 0)
            .Select(l => new OrderItemRequest(l.ProductId, l.Quantity, string.IsNullOrWhiteSpace(l.Note) ? null : l.Note.Trim()))
            .ToList();
        if (items.Count == 0)
            Error = "Bạn chưa chọn món nào.";
        else
        {
            try
            {
                await api.PlaceOrderAsync(id, items, ct);
                Success = $"Đã gửi {items.Sum(i => i.Quantity)} món xuống quầy pha chế. Cảm ơn bạn!";
                return RedirectToPage(new { id }); // PRG: tải lại trang không gửi order lần hai
            }
            catch (ApiException e) when (e.Status != HttpStatusCode.NotFound)
            {
                Error = e.Message;
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
            {
                // Hết thời gian chờ: order có thể đã vào hệ thống → nhắc xem lại trước khi gửi lại để tránh trùng.
                Error = "Chưa xác nhận được order. Bạn xem lại mục \"Món bàn đã gọi\" (bấm Cập nhật trạng thái) trước khi gửi lại.";
            }
        }

        return await LoadAsync(id, ct) ? Page() : NotFound();
    }

    public int PostedQuantity(Guid productId) => Lines.FirstOrDefault(l => l.ProductId == productId)?.Quantity ?? 0;
    public string? PostedNote(Guid productId) => Lines.FirstOrDefault(l => l.ProductId == productId)?.Note;

    private async Task<bool> LoadAsync(Guid id, CancellationToken ct)
    {
        var menu = api.GetMenuAsync(null, null, ct);
        try { Table = await api.GetTableAsync(id, ct); }
        catch (ApiException e) when (e.Status == HttpStatusCode.NotFound) { return false; }
        Menu = await menu;
        return true;
    }
}
