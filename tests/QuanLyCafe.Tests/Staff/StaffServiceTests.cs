using Microsoft.Extensions.Logging.Abstractions;
using QuanLyCafe.Application.Staff;
using QuanLyCafe.Domain.Common;
using QuanLyCafe.Domain.Staff;

namespace QuanLyCafe.Tests.Staff;

public class StaffServiceTests
{
    private static readonly List<Role> Roles =
    [
        new() { RoleID = 1, RoleName = "Manager" },
        new() { RoleID = 2, RoleName = "Cashier" },
        new() { RoleID = 3, RoleName = "Waiter" },
        new() { RoleID = 4, RoleName = "Barista" },
    ];

    private readonly User _manager = new() { Username = "manager", FullName = "Quản lý", RoleID = 1, Role = Roles[0] };
    private readonly FakeUsers _repo;
    private readonly StaffService _staff;
    private readonly AuthService _auth;

    public StaffServiceTests()
    {
        _repo = new FakeUsers(_manager);
        _staff = new StaffService(_repo, NullLogger<StaffService>.Instance);
        _auth = new AuthService(_repo, new FakeTokens(), NullLogger<AuthService>.Instance);
    }

    [Fact]
    public async Task Tao_tai_khoan_thi_dang_nhap_duoc_dung_vai_tro()
    {
        var dto = await _staff.CreateAsync(new CreateUserRequest(" waiter1 ", "123456", "Phục vụ 1", 3), _manager.UserID);

        Assert.Equal("waiter1", dto.Username);
        Assert.Equal("Waiter", dto.Role);
        Assert.True(dto.IsActive);
        Assert.Equal("Waiter", (await _auth.LoginAsync(new LoginRequest("waiter1", "123456"))).Role);
    }

    [Theory]
    [InlineData("manager", "123456", "Trùng", 3)] // trùng username
    [InlineData("", "123456", "A", 3)]
    [InlineData("u2", "123", "A", 3)]            // mật khẩu ngắn
    [InlineData("u2", "123456", " ", 3)]
    [InlineData("u2", "123456", "A", 99)]         // vai trò không tồn tại
    public async Task Tao_tai_khoan_du_lieu_sai_tra_400(string username, string password, string fullName, int roleId)
    {
        await Assert.ThrowsAsync<ValidationException>(() =>
            _staff.CreateAsync(new CreateUserRequest(username, password, fullName, roleId), _manager.UserID));
        Assert.Equal(0, _repo.Saves);
    }

    [Fact]
    public async Task Sua_thong_tin_va_gan_vai_tro()
    {
        var u = await _staff.CreateAsync(new CreateUserRequest("u1", "123456", "A", 3), _manager.UserID);

        var dto = await _staff.UpdateAsync(u.Id, new UpdateUserRequest("B", 4), _manager.UserID);

        Assert.Equal(("B", 4, "Barista"), (dto.FullName, dto.RoleId, dto.Role));
    }

    [Fact]
    public async Task Vo_hieu_hoa_thi_khong_dang_nhap_duoc_ERR_AUTH_002()
    {
        var u = await _staff.CreateAsync(new CreateUserRequest("u1", "123456", "A", 3), _manager.UserID);

        await _staff.DeactivateAsync(u.Id, _manager.UserID);

        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => _auth.LoginAsync(new LoginRequest("u1", "123456")));
        Assert.Equal(ErrorCodes.AuthAccountLocked, ex.Code);
    }

    [Fact]
    public async Task Khong_tu_vo_hieu_hoa_hay_tu_doi_vai_tro_chinh_minh()
    {
        await Assert.ThrowsAsync<BusinessRuleException>(() => _staff.DeactivateAsync(_manager.UserID, _manager.UserID));
        await Assert.ThrowsAsync<BusinessRuleException>(() =>
            _staff.UpdateAsync(_manager.UserID, new UpdateUserRequest("Quản lý", 2), _manager.UserID));
        Assert.True(_manager.IsActive);
        Assert.Equal(1, _manager.RoleID);
    }

    [Fact]
    public async Task Cap_lai_mat_khau_thi_mat_khau_cu_het_hieu_luc()
    {
        var u = await _staff.CreateAsync(new CreateUserRequest("u1", "123456", "A", 3), _manager.UserID);

        await _staff.ResetPasswordAsync(u.Id, new ResetPasswordRequest("654321"), _manager.UserID);

        await _auth.LoginAsync(new LoginRequest("u1", "654321"));
        await Assert.ThrowsAsync<UnauthorizedException>(() => _auth.LoginAsync(new LoginRequest("u1", "123456")));
    }

    [Fact]
    public async Task Tai_khoan_khong_ton_tai_tra_404()
    {
        await Assert.ThrowsAsync<NotFoundException>(() => _staff.DeactivateAsync(Guid.NewGuid(), _manager.UserID));
    }

    private class FakeUsers(params User[] seed) : IUserRepository
    {
        private readonly List<User> _users = [.. seed];
        public int Saves;
        public Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default) =>
            Task.FromResult(_users.FirstOrDefault(u => u.Username == username));
        public Task<User?> GetByIdAsync(Guid userId, CancellationToken ct = default) =>
            Task.FromResult(_users.FirstOrDefault(u => u.UserID == userId));
        public Task<List<User>> GetAllAsync(CancellationToken ct = default) => Task.FromResult(_users.ToList());
        public Task<List<Role>> GetRolesAsync(CancellationToken ct = default) => Task.FromResult(Roles);
        public void Add(User user) => _users.Add(user);
        public Task SaveChangesAsync(CancellationToken ct = default) { Saves++; return Task.CompletedTask; }
    }

    private class FakeTokens : IJwtTokenGenerator
    {
        public (string Token, DateTime ExpiresAt) Generate(User user) => ("t", DateTime.UtcNow.AddHours(8));
    }
}
