namespace QuanLyCafe.Desktop.Services;

public record LoginRequest(string Username, string Password);
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);
public record LoginResponse(string Token, string FullName, string Role);

public interface IAuthApi
{
    Task<LoginResponse> LoginAsync(string username, string password);
    Task ChangePasswordAsync(string currentPassword, string newPassword);
}

// Bản giả: tài khoản manager / cashier / waiter / barista, mật khẩu 123
public class FakeAuthApi : IAuthApi
{
    private static readonly Dictionary<string, (string Name, string Role)> Users = new()
    {
        ["manager"] = ("Quản lý Demo", "Manager"),
        ["cashier"] = ("Thu ngân Demo", "Cashier"),
        ["waiter"]  = ("Phục vụ Demo", "Waiter"),
        ["barista"] = ("Pha chế Demo", "Barista"),
    };

    public async Task<LoginResponse> LoginAsync(string username, string password)
    {
        await Task.Delay(300); // giả lập độ trễ mạng
        if (!Users.TryGetValue(username.Trim().ToLower(), out var u) || password != "123")
            throw new ApiException(401, "ERR-AUTH-001", "Sai tài khoản hoặc mật khẩu");
        return new LoginResponse("fake-token", u.Name, u.Role);
    }

    public async Task ChangePasswordAsync(string currentPassword, string newPassword)
    {
        await Task.Delay(300);
        if (currentPassword != "123")
            throw new ApiException(400, "ERR-AUTH-001", "Mật khẩu cũ không đúng");
    }
}

// Bản thật: dùng từ Ngày 3
public class ApiAuthApi : IAuthApi
{
    private readonly ApiClient _api;
    public ApiAuthApi(ApiClient api) => _api = api;

    public async Task<LoginResponse> LoginAsync(string username, string password)
        => (await _api.PostAsync<LoginResponse>("api/auth/login", new LoginRequest(username, password)))!;

    public Task ChangePasswordAsync(string currentPassword, string newPassword)
        => _api.PostAsync("api/auth/change-password", new ChangePasswordRequest(currentPassword, newPassword));
}