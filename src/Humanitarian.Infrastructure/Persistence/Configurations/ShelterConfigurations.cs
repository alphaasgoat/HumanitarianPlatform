using Humanitarian.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Humanitarian.Infrastructure.Persistence.Configurations;

public class ShelterConfiguration : IEntityTypeConfiguration<Shelter>
{
    public void Configure(EntityTypeBuilder<Shelter> e)
    {
        e.Property(x => x.Name).HasMaxLength(150).IsRequired();
        e.Property(x => x.Address).HasMaxLength(300).IsRequired();
        e.Property(x => x.Notes).HasMaxLength(500);
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.RowVersion).IsRowVersion();

        e.HasIndex(x => new { x.CrisisEventId, x.Status });
        e.HasOne<CrisisEvent>().WithMany().HasForeignKey(x => x.CrisisEventId).OnDelete(DeleteBehavior.Restrict);

        // Database-level guard against over-allocation.
        e.ToTable(t => t.HasCheckConstraint("CK_Shelters_Occupancy", "[Occupancy] >= 0 AND [Occupancy] <= [Capacity]"));
    }
}

public class ShelterAllocationConfiguration : IEntityTypeConfiguration<ShelterAllocation>
{
    public void Configure(EntityTypeBuilder<ShelterAllocation> e)
    {
        e.Property(x => x.AllocatedByEmail).HasMaxLength(256);
        e.Property(x => x.Note).HasMaxLength(500);
        e.Property(x => x.ReleaseReason).HasMaxLength(300);
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

        e.HasIndex(x => new { x.ShelterId, x.Status });
        e.HasIndex(x => x.RequestId);

        e.HasOne<Shelter>().WithMany().HasForeignKey(x => x.ShelterId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<AssistanceRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<CrisisEvent>().WithMany().HasForeignKey(x => x.CrisisEventId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class ShelterSupplyRequestConfiguration : IEntityTypeConfiguration<ShelterSupplyRequest>
{
    public void Configure(EntityTypeBuilder<ShelterSupplyRequest> e)
    {
        e.Property(x => x.Note).HasMaxLength(300);
        e.Property(x => x.RequestedByEmail).HasMaxLength(256);
        e.Property(x => x.ResolutionNote).HasMaxLength(300);
        e.Property(x => x.Category).HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

        e.HasIndex(x => x.Status);
        e.HasOne<Shelter>().WithMany().HasForeignKey(x => x.ShelterId).OnDelete(DeleteBehavior.Restrict);
    }
}
