using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyCafe.Domain.Catalog;

namespace QuanLyCafe.Infrastructure.Persistence.Configurations;

public class ProductBOMConfiguration : IEntityTypeConfiguration<ProductBOM>
{
    public void Configure(EntityTypeBuilder<ProductBOM> b)
    {
        b.ToTable("Product_BOMs", t => t.HasCheckConstraint("CK_Product_BOMs_QuantityRequired", "[QuantityRequired] > 0"));
        b.HasKey(x => new { x.ProductID, x.MaterialID });
        b.Property(x => x.QuantityRequired).HasPrecision(18, 3);
        b.HasOne(x => x.Product).WithMany(p => p.ProductBOMs).HasForeignKey(x => x.ProductID);
        b.HasOne(x => x.Material).WithMany().HasForeignKey(x => x.MaterialID);
    }
}
