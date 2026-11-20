using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace QuanLyCafe.Desktop.Services;

public class ApiClient
{
    private readonly HttpClient _http;
    private readonly SessionStore _session;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public event Action? SessionExpired;

    public ApiClient(SessionStore session, string baseUrl)
    {
        _session = session;
        _http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(30) };
    }

    public Task<T?> GetAsync<T>(string url, CancellationToken ct = default)
        => SendAsync<T>(new HttpRequestMessage(HttpMethod.Get, url), ct);

    public Task<T?> PostAsync<T>(string url, object? body = null, CancellationToken ct = default)
        => SendAsync<T>(Build(HttpMethod.Post, url, body), ct);

    public Task PostAsync(string url, object? body = null, CancellationToken ct = default)
        => SendAsync<object>(Build(HttpMethod.Post, url, body), ct);

    public Task<T?> PutAsync<T>(string url, object? body = null, CancellationToken ct = default)
        => SendAsync<T>(Build(HttpMethod.Put, url, body), ct);

    public Task PutAsync(string url, object? body = null, CancellationToken ct = default)
        => SendAsync<object>(Build(HttpMethod.Put, url, body), ct);

    private static HttpRequestMessage Build(HttpMethod m, string url, object? body)
    {
        var req = new HttpRequestMessage(m, url);
        if (body is not null) req.Content = JsonContent.Create(body, options: Json);
        return req;
    }

    private async Task<T?> SendAsync<T>(HttpRequestMessage req, CancellationToken ct)
    {
        if (_session.IsLoggedIn)
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _session.Token);

        HttpResponseMessage res;
        try { res = await _http.SendAsync(req, ct); }
        catch (HttpRequestException) { throw new ApiException(0, "ERR-NET", "Không kết nối được máy chủ"); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        { throw new ApiException(0, "ERR-NET", "Máy chủ phản hồi quá chậm"); }

        if (res.IsSuccessStatusCode)
        {
            if (res.StatusCode == System.Net.HttpStatusCode.NoContent || res.Content.Headers.ContentLength == 0)
                return default;
            return await res.Content.ReadFromJsonAsync<T>(Json, ct);
        }

        // Lỗi: đọc ProblemDetails
        string? code = null, detail = null;
        try
        {
            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;
            if (root.TryGetProperty("code", out var c)) code = c.GetString();
            if (root.TryGetProperty("detail", out var d)) detail = d.GetString();
        }
        catch (JsonException) { /* body không phải JSON */ }

        // 401 khi đã đăng nhập = token hết hạn → về form đăng nhập
        if ((int)res.StatusCode == 401 && _session.IsLoggedIn)
        {
            _session.Clear();
            SessionExpired?.Invoke();
            code = "ERR-AUTH-003";
        }
        if ((int)res.StatusCode == 403)
            detail ??= "Bạn không có quyền thực hiện thao tác này.";
        throw new ApiException((int)res.StatusCode, code, detail ?? res.ReasonPhrase ?? "Lỗi");
    }
}