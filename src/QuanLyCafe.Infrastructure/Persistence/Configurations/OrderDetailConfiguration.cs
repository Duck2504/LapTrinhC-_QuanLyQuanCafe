using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyCafe.Domain.Ordering;

namespace QuanLyCafe.Infrastructure.Persistence.Configurations;

public class OrderDetailConfiguration : IEntityTypeConfiguration<OrderDetail>
{
    public void Configure(EntityTypeBuilder<OrderDetail> b)
    {
        b.HasKey(x => x.OrderDetailID);
        b.Property(x => x.UnitPrice).HasPrecision(18, 2);
        b.Property(x => x.Note).HasMaxLength(200);
        b.HasOne(x => x.Order).WithMany(o => o.OrderDetails).HasForeignKey(x => x.OrderID);
        b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductID);
        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_OrderDetails_Quantity", "[Quantity] > 0");
            t.HasCheckConstraint("CK_OrderDetails_UnitPrice", "[UnitPrice] >= 0");
            t.HasCheckConstraint("CK_OrderDetails_Status", "[Status] IN (0, 1, 2, 3)");
        });
    }
}
