using Humanitarian.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Humanitarian.Infrastructure.Persistence.Configurations;

public class ResourceItemConfiguration : IEntityTypeConfiguration<ResourceItem>
{
    public void Configure(EntityTypeBuilder<ResourceItem> e)
    {
        e.Property(x => x.Name).HasMaxLength(150).IsRequired();
        e.Property(x => x.Unit).HasMaxLength(30).IsRequired();
        e.Property(x => x.LocationName).HasMaxLength(200);
        e.Property(x => x.Category).HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.RowVersion).IsRowVersion(); // optimistic concurrency

        e.HasIndex(x => new { x.Category, x.IsActive });
        e.HasOne<Organisation>().WithMany().HasForeignKey(x => x.OrganisationId).OnDelete(DeleteBehavior.Restrict);

        // Last line of defence in the database: stock can never be negative or exceed the total.
        e.ToTable(t => t.HasCheckConstraint("CK_ResourceItems_Quantities",
            "[QuantityAvailable] >= 0 AND [QuantityAvailable] <= [QuantityTotal]"));
    }
}

public class ResourceAllocationConfiguration : IEntityTypeConfiguration<ResourceAllocation>
{
    public void Configure(EntityTypeBuilder<ResourceAllocation> e)
    {
        e.Property(x => x.Rationale).HasMaxLength(1500).IsRequired();
        e.Property(x => x.ApprovedByEmail).HasMaxLength(256);
        e.Property(x => x.CancelReason).HasMaxLength(300);
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

        e.HasIndex(x => x.RequestId);
        e.HasIndex(x => new { x.CrisisEventId, x.Status });

        e.HasOne<ResourceItem>().WithMany().HasForeignKey(x => x.ResourceItemId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<AssistanceRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<CrisisEvent>().WithMany().HasForeignKey(x => x.CrisisEventId).OnDelete(DeleteBehavior.Restrict);
    }
}
