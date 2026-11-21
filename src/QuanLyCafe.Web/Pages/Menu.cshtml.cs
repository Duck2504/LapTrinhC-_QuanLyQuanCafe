using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace QuanLyCafe.Web.Pages;

public class MenuModel(CafeApi api) : PageModel
{
    [BindProperty(SupportsGet = true)] public string? Q { get; set; }
    [BindProperty(SupportsGet = true)] public Guid? Cat { get; set; }

    public List<CategoryDto> Categories { get; private set; } = [];
    public List<MenuItemDto> Items { get; private set; } = [];

    public async Task OnGetAsync(CancellationToken ct)
    {
        var categories = api.GetCategoriesAsync(ct);
        Items = await api.GetMenuAsync(Q, Cat, ct);
        Categories = await categories;
    }
}
