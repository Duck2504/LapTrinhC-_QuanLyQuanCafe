using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyCafe.Domain.Catalog;

namespace QuanLyCafe.Infrastructure.Persistence.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> b)
    {
        b.HasKey(x => x.CategoryID);
        b.Property(x => x.CategoryName).HasMaxLength(50).IsRequired();
        b.Property(x => x.IsActive).HasDefaultValue(true);
        b.HasIndex(x => x.CategoryName).IsUnique();
    }
}
