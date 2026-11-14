namespace QuanLyCafe.Domain.Staff;

public class User
{
    public Guid UserID { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string FullName { get; set; } = "";
    public bool IsActive { get; set; } = true;

    public int RoleID { get; set; }
    public Role Role { get; set; } = null!;
}
