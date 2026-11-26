using Humanitarian.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Humanitarian.Infrastructure.Persistence.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> e)
    {
        e.Property(x => x.Title).HasMaxLength(200).IsRequired();
        e.Property(x => x.Body).HasMaxLength(2000).IsRequired();
        e.Property(x => x.Language).HasMaxLength(10).IsRequired();
        e.Property(x => x.DedupeKey).HasMaxLength(200).IsRequired();
        e.Property(x => x.RelatedEntityType).HasMaxLength(60);
        e.Property(x => x.RelatedEntityId).HasMaxLength(100);
        e.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);

        // The same event can never notify the same person twice, even if it is processed again.
        e.HasIndex(x => new { x.RecipientUserId, x.DedupeKey }).IsUnique();
        e.HasIndex(x => new { x.RecipientUserId, x.CreatedAtUtc });
        e.HasOne<User>().WithMany().HasForeignKey(x => x.RecipientUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class NotificationDeliveryConfiguration : IEntityTypeConfiguration<NotificationDelivery>
{
    public void Configure(EntityTypeBuilder<NotificationDelivery> e)
    {
        e.Property(x => x.Destination).HasMaxLength(256).IsRequired();
        e.Property(x => x.Channel).HasConversion<string>().HasMaxLength(10);
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(15);
        e.Property(x => x.Provider).HasMaxLength(30);
        e.Property(x => x.LastError).HasMaxLength(500);
        e.Property(x => x.RowVersion).IsRowVersion();

        e.HasIndex(x => new { x.Status, x.NextAttemptUtc });
        e.HasIndex(x => x.NotificationId);
        e.HasOne<Notification>().WithMany().HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class GeocodeCacheEntryConfiguration : IEntityTypeConfiguration<GeocodeCacheEntry>
{
    public void Configure(EntityTypeBuilder<GeocodeCacheEntry> e)
    {
        e.Property(x => x.Query).HasMaxLength(300).IsRequired();
        e.Property(x => x.DisplayName).HasMaxLength(500);
        e.Property(x => x.Provider).HasMaxLength(40).IsRequired();
        e.HasIndex(x => x.Query);
    }
}
