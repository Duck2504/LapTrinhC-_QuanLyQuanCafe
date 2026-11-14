using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyCafe.Domain.Inventory;

namespace QuanLyCafe.Infrastructure.Persistence.Configurations;

public class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> b)
    {
        b.HasKey(x => x.MovementID);
        b.Property(x => x.Quantity).HasPrecision(18, 3);
        b.Property(x => x.Note).HasMaxLength(200);
        b.HasOne(x => x.Material).WithMany().HasForeignKey(x => x.MaterialID);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserID);
        b.HasOne(x => x.Order).WithMany().HasForeignKey(x => x.OrderID);
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_StockMovements_Quantity", "[Quantity] > 0");
            t.HasCheckConstraint("CK_StockMovements_MovementType", "[MovementType] IN (1, 2, 3, 4)");
        });
    }
}
