using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using QuanLyCafe.Application.Staff;
using QuanLyCafe.Domain.Common;
using QuanLyCafe.Domain.Staff;

namespace QuanLyCafe.Tests.Staff;

public class AuthServiceTests
{
    private const string Password = "secret123";
    private readonly User _user;
    private readonly FakeUsers _repo;
    private readonly AuthService _auth;

    public AuthServiceTests()
    {
        _user = new User { Username = "waiter1", FullName = "Phục vụ 1", Role = new Role { RoleID = 3, RoleName = "Waiter" } };
        _user.PasswordHash = new PasswordHasher<User>().HashPassword(_user, Password);
        _repo = new FakeUsers(_user);
        _auth = new AuthService(_repo, new FakeTokens(), NullLogger<AuthService>.Instance);
    }

    [Fact]
    public async Task Login_dung_mat_khau_tra_token_va_vai_tro()
    {
        var res = await _auth.LoginAsync(new LoginRequest(" waiter1 ", Password));

        Assert.Equal("token-for-" + _user.UserID, res.Token);
        Assert.Equal("Waiter", res.Role);
        Assert.Equal(_user.UserID, res.UserId);
    }

    [Theory]
    [InlineData("waiter1", "sai")]
    [InlineData("waiter1", "")]
    [InlineData("khong-co", Password)]
    [InlineData("", Password)]
    public async Task Login_sai_tra_ERR_AUTH_001(string username, string password)
    {
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => _auth.LoginAsync(new LoginRequest(username, password)));
        Assert.Equal(ErrorCodes.AuthInvalidCredentials, ex.Code);
    }

    [Fact]
    public async Task Login_tai_khoan_bi_khoa_tra_ERR_AUTH_002()
    {
        _user.IsActive = false;
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => _auth.LoginAsync(new LoginRequest("waiter1", Password)));
        Assert.Equal(ErrorCodes.AuthAccountLocked, ex.Code);
    }

    [Fact]
    public async Task Doi_mat_khau_thanh_cong_thi_dang_nhap_bang_mat_khau_moi()
    {
        await _auth.ChangePasswordAsync(_user.UserID, new ChangePasswordRequest(Password, "moi123456"));

        Assert.Equal(1, _repo.Saves);
        await _auth.LoginAsync(new LoginRequest("waiter1", "moi123456"));
        await Assert.ThrowsAsync<UnauthorizedException>(() => _auth.LoginAsync(new LoginRequest("waiter1", Password)));
    }

    [Fact]
    public async Task Doi_mat_khau_sai_mat_khau_cu_tra_400_ERR_AUTH_001()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _auth.ChangePasswordAsync(_user.UserID, new ChangePasswordRequest("sai", "moi123456")));
        Assert.Equal(ErrorCodes.AuthInvalidCredentials, ex.Code);
        Assert.Equal(0, _repo.Saves);
    }

    [Fact]
    public async Task Doi_mat_khau_moi_qua_ngan_bi_tu_choi()
    {
        var ex = await Assert.ThrowsAsync<ValidationException>(() =>
            _auth.ChangePasswordAsync(_user.UserID, new ChangePasswordRequest(Password, "123")));
        Assert.Equal(ErrorCodes.ValidationFailed, ex.Code);
    }

    private class FakeUsers(User user) : IUserRepository
    {
        public int Saves;
        public Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default) =>
            Task.FromResult(user.Username == username ? user : null);
        public Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(user.UserID == userId ? user : null);
        public Task<List<User>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(new List<User> { user });
        public Task<List<Role>> GetRolesAsync(CancellationToken ct = default) => Task.FromResult(new List<Role> { user.Role });
        public void Add(User u) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken ct = default) { Saves++; return Task.CompletedTask; }
    }

    private class FakeTokens : IJwtTokenGenerator
    {
        public (string Token, DateTime ExpiresAt) Generate(User user) => ("token-for-" + user.UserID, DateTime.UtcNow.AddHours(8));
    }
}
