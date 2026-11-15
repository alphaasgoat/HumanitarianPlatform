using Humanitarian.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Humanitarian.Infrastructure.Persistence;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Organisation> Organisations => Set<Organisation>();
    public DbSet<User> Users => Set<User>();
    public DbSet<CrisisEvent> CrisisEvents => Set<CrisisEvent>();
    public DbSet<OperationalArea> OperationalAreas => Set<OperationalArea>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<AssistanceRequest> AssistanceRequests => Set<AssistanceRequest>();
    public DbSet<AiRecommendation> AiRecommendations => Set<AiRecommendation>();
    public DbSet<RequestHistory> RequestHistories => Set<RequestHistory>();
    public DbSet<DuplicateLink> DuplicateLinks => Set<DuplicateLink>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Organisation>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Country).HasMaxLength(100).IsRequired();
            e.Property(x => x.ContactEmail).HasMaxLength(256);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
        });

        b.Entity<User>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.Property(x => x.PasswordHash).HasMaxLength(200).IsRequired();
            e.Property(x => x.FullName).HasMaxLength(150).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(30);
            e.Property(x => x.PreferredLanguage).HasMaxLength(10);
            e.Property(x => x.Role).HasConversion<string>().HasMaxLength(40);
            e.HasOne(x => x.Organisation).WithMany(o => o.Users)
                .HasForeignKey(x => x.OrganisationId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<CrisisEvent>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Country).HasMaxLength(100).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(30);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Status);
            e.HasMany(x => x.Areas).WithOne(a => a.CrisisEvent)
                .HasForeignKey(a => a.CrisisEventId).OnDelete(DeleteBehavior.Cascade);
            e.Navigation(x => x.Areas).AutoInclude();
        });

        b.Entity<OperationalArea>(e => e.Property(x => x.Name).HasMaxLength(150).IsRequired());

        b.Entity<AuditEvent>(e =>
        {
            e.Property(x => x.Action).HasMaxLength(100).IsRequired();
            e.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
            e.Property(x => x.EntityId).HasMaxLength(100);
            e.Property(x => x.ActorEmail).HasMaxLength(256);
            e.Property(x => x.IpAddress).HasMaxLength(64);
            e.HasIndex(x => x.CreatedAtUtc);
            e.HasIndex(x => new { x.EntityType, x.EntityId });
        });

        // Module configurations (requests, ...) live in Persistence/Configurations.
        b.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
