namespace QuanLyCafe.Desktop.Services;

public class SessionStore
{
    public string? Token { get; private set; }
    public string FullName { get; private set; } = "";
    public string Role { get; private set; } = "";
    public bool IsLoggedIn => !string.IsNullOrEmpty(Token);

    public void Set(string token, string fullName, string role)
        => (Token, FullName, Role) = (token, fullName, role);

    public void Clear() => (Token, FullName, Role) = (null, "", "");
}