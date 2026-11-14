using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyCafe.Domain.Staff;

namespace QuanLyCafe.Infrastructure.Persistence.Configurations;

public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> b)
    {
        b.HasKey(x => x.RoleID);
        b.Property(x => x.RoleName).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.RoleName).IsUnique();

        b.HasData(
            new Role { RoleID = 1, RoleName = "Manager" },
            new Role { RoleID = 2, RoleName = "Cashier" },
            new Role { RoleID = 3, RoleName = "Waiter" },
            new Role { RoleID = 4, RoleName = "Barista" });
    }
}
