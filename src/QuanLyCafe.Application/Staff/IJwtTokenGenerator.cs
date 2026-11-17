using QuanLyCafe.Domain.Staff;

namespace QuanLyCafe.Application.Staff;

public interface IJwtTokenGenerator
{
    (string Token, DateTime ExpiresAt) Generate(User user);
}
