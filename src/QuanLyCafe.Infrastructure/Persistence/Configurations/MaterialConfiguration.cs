using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyCafe.Domain.Inventory;

namespace QuanLyCafe.Infrastructure.Persistence.Configurations;

public class MaterialConfiguration : IEntityTypeConfiguration<Material>
{
    public void Configure(EntityTypeBuilder<Material> b)
    {
        b.HasKey(x => x.MaterialID);
        b.Property(x => x.MaterialName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Unit).HasMaxLength(20).IsRequired();
        // Concurrency token: hai phiên cùng trừ kho → DbUpdateConcurrencyException → ERR-CON-001.
        b.Property(x => x.StockQuantity).HasPrecision(18, 3).IsConcurrencyToken();
        b.Property(x => x.MinQuantity).HasPrecision(18, 3);
        b.ToTable(t => t.HasCheckConstraint("CK_Materials_StockQuantity", "[StockQuantity] >= 0"));
    }
}
