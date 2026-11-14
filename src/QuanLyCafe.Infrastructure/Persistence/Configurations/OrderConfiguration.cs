using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyCafe.Domain.Ordering;

namespace QuanLyCafe.Infrastructure.Persistence.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> b)
    {
        b.HasKey(x => x.OrderID);
        b.Property(x => x.TotalAmount).HasPrecision(18, 2);
        b.HasOne(x => x.Table).WithMany().HasForeignKey(x => x.TableID);
        b.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserID);
        b.HasOne(x => x.Cashier).WithMany().HasForeignKey(x => x.CashierID);

        // Chốt chặn ở DB cho quy tắc một bàn chỉ một order mở (ERR-ORDER-002).
        b.HasIndex(x => x.TableID).IsUnique().HasFilter("[Status] = 0 AND [TableID] IS NOT NULL");

        b.ToTable(t =>
        {
            t.HasCheckConstraint("CK_Orders_OrderType", "[OrderType] IN (1, 2)");
            t.HasCheckConstraint("CK_Orders_Status", "[Status] IN (0, 1, 2)");
            t.HasCheckConstraint("CK_Orders_PaymentMethod", "[PaymentMethod] IS NULL OR [PaymentMethod] IN (1, 2, 3, 4)");
            t.HasCheckConstraint("CK_Orders_TotalAmount", "[TotalAmount] >= 0");
        });
    }
}
