using Humanitarian.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Humanitarian.Infrastructure.Persistence.Configurations;

public class AssistanceRequestConfiguration : IEntityTypeConfiguration<AssistanceRequest>
{
    public void Configure(EntityTypeBuilder<AssistanceRequest> e)
    {
        e.Property(x => x.Description).HasMaxLength(2000).IsRequired();
        e.Property(x => x.AddressText).HasMaxLength(300);
        e.Property(x => x.IdempotencyKey).HasMaxLength(100);
        e.Property(x => x.Category).HasConversion<string>().HasMaxLength(30);
        e.Property(x => x.DeclaredUrgency).HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.Urgency).HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

        e.HasIndex(x => new { x.CrisisEventId, x.Status });
        // A retried submission with the same key can never create a second row.
        e.HasIndex(x => new { x.RequesterId, x.IdempotencyKey })
            .IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");

        e.HasOne<CrisisEvent>().WithMany().HasForeignKey(x => x.CrisisEventId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<User>().WithMany().HasForeignKey(x => x.RequesterId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class AiRecommendationConfiguration : IEntityTypeConfiguration<AiRecommendation>
{
    public void Configure(EntityTypeBuilder<AiRecommendation> e)
    {
        e.Property(x => x.Rationale).HasMaxLength(1500).IsRequired();
        e.Property(x => x.ModelName).HasMaxLength(100).IsRequired();
        e.Property(x => x.DecidedByEmail).HasMaxLength(256);
        e.Property(x => x.DecisionNote).HasMaxLength(500);
        e.Property(x => x.SuggestedCategory).HasConversion<string>().HasMaxLength(30);
        e.Property(x => x.SuggestedUrgency).HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.HumanAction).HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.FinalCategory).HasConversion<string>().HasMaxLength(30);
        e.Property(x => x.FinalUrgency).HasConversion<string>().HasMaxLength(20);

        e.HasIndex(x => new { x.RequestId, x.Status });
        e.HasOne<AssistanceRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class RequestHistoryConfiguration : IEntityTypeConfiguration<RequestHistory>
{
    public void Configure(EntityTypeBuilder<RequestHistory> e)
    {
        e.Property(x => x.Action).HasMaxLength(100).IsRequired();
        e.Property(x => x.ActorEmail).HasMaxLength(256);
        e.Property(x => x.Note).HasMaxLength(1000);
        e.Property(x => x.FromStatus).HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.ToStatus).HasConversion<string>().HasMaxLength(20);

        e.HasIndex(x => new { x.RequestId, x.CreatedAtUtc });
        e.HasOne<AssistanceRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class DuplicateLinkConfiguration : IEntityTypeConfiguration<DuplicateLink>
{
    public void Configure(EntityTypeBuilder<DuplicateLink> e)
    {
        e.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

        e.HasIndex(x => new { x.RequestId, x.DuplicateOfRequestId }).IsUnique();
        // Restrict (not cascade) on both keys: SQL Server forbids two cascade paths to one table,
        // and requests are never deleted anyway.
        e.HasOne<AssistanceRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<AssistanceRequest>().WithMany().HasForeignKey(x => x.DuplicateOfRequestId).OnDelete(DeleteBehavior.Restrict);
    }
}
