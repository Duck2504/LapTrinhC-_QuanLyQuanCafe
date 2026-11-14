using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using QuanLyCafe.Domain.Ordering;

namespace QuanLyCafe.Infrastructure.Persistence.Configurations;

public class TableConfiguration : IEntityTypeConfiguration<Table>
{
    public void Configure(EntityTypeBuilder<Table> b)
    {
        b.HasKey(x => x.TableID);
        b.Property(x => x.TableName).HasMaxLength(50).IsRequired();
        b.HasIndex(x => x.TableName).IsUnique();
        b.ToTable(t => t.HasCheckConstraint("CK_Tables_Status", "[Status] IN (0, 1)"));
    }
}
