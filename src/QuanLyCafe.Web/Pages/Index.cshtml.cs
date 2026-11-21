using Microsoft.AspNetCore.Mvc.RazorPages;

namespace QuanLyCafe.Web.Pages;

public class IndexModel(CafeApi api) : PageModel
{
    public List<CategoryDto> Categories { get; private set; } = [];
    public List<MenuItemDto> Featured { get; private set; } = [];
    public int MenuCount { get; private set; }

    public async Task OnGetAsync(CancellationToken ct)
    {
        var categories = api.GetCategoriesAsync(ct);
        var menu = await api.GetMenuAsync(null, null, ct);
        Categories = await categories;
        MenuCount = menu.Count;
        Featured = menu.Take(8).ToList();
    }
}
