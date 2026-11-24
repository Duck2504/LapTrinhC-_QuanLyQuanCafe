using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyCafe.Domain.Catalog;

namespace QuanLyCafe.Infrastructure.Persistence.Configurations;

public class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> b)
    {
        b.HasKey(x => x.ProductID);
        b.Property(x => x.ProductName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Price).HasPrecision(18, 2);
        b.Property(x => x.Description).HasMaxLength(500);
        b.Property(x => x.ImageUrl).HasMaxLength(300);
        b.HasOne(x => x.Category).WithMany().HasForeignKey(x => x.CategoryID);
        b.ToTable(t => t.HasCheckConstraint("CK_Products_Price", "[Price] >= 0"));
    }
}
