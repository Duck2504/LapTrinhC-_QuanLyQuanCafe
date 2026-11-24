using System.Globalization;
using System.Net;

namespace QuanLyCafe.Web;

// DTO khớp JSON của QuanLyCafe.Api (camelCase, enum dạng số).
public record CategoryDto(Guid CategoryId, string CategoryName);
public record MenuItemDto(Guid ProductId, string ProductName, decimal Price,
    Guid? CategoryId, string? CategoryName, string? Description, string? ImageUrl);
public record OrderItemDto(Guid Id, Guid ProductId, string ProductName, int Quantity, decimal UnitPrice, int Status, string? Note);
public record OrderDto(Guid Id, DateTime OrderDate, int Status, string? TableName, decimal TotalAmount, List<OrderItemDto> Items);
public record GuestTableDto(Guid Id, string Name, OrderDto? OpenOrder);
public record OrderItemRequest(Guid ProductId, int Quantity, string? Note);

public class ApiException(HttpStatusCode status, string message) : Exception(message)
{
    public HttpStatusCode Status { get; } = status;
}

public class CafeApi(HttpClient http)
{
    public Task<List<CategoryDto>> GetCategoriesAsync(CancellationToken ct) =>
        GetAsync<List<CategoryDto>>("api/public/categories", ct);

    public Task<List<MenuItemDto>> GetMenuAsync(string? q, Guid? categoryId, CancellationToken ct)
    {
        var query = new List<string>();
        if (!string.IsNullOrWhiteSpace(q)) query.Add("q=" + Uri.EscapeDataString(q.Trim()));
        if (categoryId is { } c) query.Add("categoryId=" + c);
        return GetAsync<List<MenuItemDto>>("api/public/menu" + (query.Count > 0 ? "?" + string.Join('&', query) : ""), ct);
    }

    public Task<MenuItemDto> GetProductAsync(Guid id, CancellationToken ct) =>
        GetAsync<MenuItemDto>($"api/public/products/{id}", ct);

    public Task<GuestTableDto> GetTableAsync(Guid id, CancellationToken ct) =>
        GetAsync<GuestTableDto>($"api/public/tables/{id}", ct);

    public async Task<OrderDto> PlaceOrderAsync(Guid tableId, List<OrderItemRequest> items, CancellationToken ct) =>
        await ReadAsync<OrderDto>(await http.PostAsJsonAsync($"api/public/tables/{tableId}/orders", new { items }, ct), ct);

    private async Task<T> GetAsync<T>(string url, CancellationToken ct) => await ReadAsync<T>(await http.GetAsync(url, ct), ct);

    private record Problem(string? Title, string? Code);

    private static async Task<T> ReadAsync<T>(HttpResponseMessage res, CancellationToken ct)
    {
        if (res.IsSuccessStatusCode)
            return (await res.Content.ReadFromJsonAsync<T>(ct))!;

        Problem? p = null;
        try { p = await res.Content.ReadFromJsonAsync<Problem>(ct); } catch { /* body không phải ProblemDetails */ }
        var message = p?.Code switch
        {
            // Không lộ số tồn kho nội bộ cho khách.
            "ERR-STOCK-001" or "ERR-BOM-001" => "Có món tạm hết, bạn vui lòng chọn món khác hoặc giảm số lượng.",
            "ERR-ORDER-002" or "ERR-CON-001" => "Bàn đang được cập nhật, bạn vui lòng bấm gửi lại.",
            _ when res.StatusCode >= HttpStatusCode.InternalServerError => "Hệ thống đang bận, vui lòng thử lại sau.",
            _ => p?.Title ?? "Yêu cầu không hợp lệ.",
        };
        throw new ApiException(res.StatusCode, message);
    }
}

public static class Fmt
{
    private static readonly CultureInfo Vi = CultureInfo.GetCultureInfo("vi-VN");
    public static string Vnd(decimal v) => v.ToString("#,0", Vi) + " ₫";

    public static string ItemStatus(int s) => s switch
    {
        0 => "Chờ pha chế",
        1 => "Đang làm",
        2 => "Đã xong",
        3 => "Đã mang ra",
        _ => "",
    };
}
