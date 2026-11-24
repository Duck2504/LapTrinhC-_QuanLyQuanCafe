using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace QuanLyCafe.Web.Pages;

public class ProductModel(CafeApi api) : PageModel
{
    public MenuItemDto Item { get; private set; } = null!;
    public List<MenuItemDto> Related { get; private set; } = [];
    public Guid? TableId { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid id, CancellationToken ct)
    {
        try { Item = await api.GetProductAsync(id, ct); }
        catch (ApiException e) when (e.Status == HttpStatusCode.NotFound) { return NotFound(); }

        if (Item.CategoryId is { } cat)
            Related = (await api.GetMenuAsync(null, cat, ct)).Where(p => p.ProductId != id).Take(4).ToList();
        TableId = Guid.TryParse(Request.Cookies["table"], out var t) ? t : null;
        return Page();
    }
}
