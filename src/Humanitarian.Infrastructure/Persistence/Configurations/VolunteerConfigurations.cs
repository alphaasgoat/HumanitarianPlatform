using Humanitarian.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Humanitarian.Infrastructure.Persistence.Configurations;

public class VolunteerConfiguration : IEntityTypeConfiguration<Volunteer>
{
    public void Configure(EntityTypeBuilder<Volunteer> e)
    {
        e.Property(x => x.FullName).HasMaxLength(150).IsRequired();
        e.Property(x => x.Phone).HasMaxLength(30);
        e.Property(x => x.Skills).HasMaxLength(200).IsRequired();

        e.HasIndex(x => x.UserId).IsUnique();   // one volunteer profile per account
        e.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class VolunteerAvailabilityConfiguration : IEntityTypeConfiguration<VolunteerAvailability>
{
    public void Configure(EntityTypeBuilder<VolunteerAvailability> e)
    {
        e.HasIndex(x => new { x.VolunteerId, x.StartUtc });
        e.HasOne<Volunteer>().WithMany().HasForeignKey(x => x.VolunteerId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class VolunteerTaskConfiguration : IEntityTypeConfiguration<VolunteerTask>
{
    public void Configure(EntityTypeBuilder<VolunteerTask> e)
    {
        e.Property(x => x.Title).HasMaxLength(150).IsRequired();
        e.Property(x => x.Description).HasMaxLength(1000);
        e.Property(x => x.RequiredSkill).HasConversion<string>().HasMaxLength(20);
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

        e.HasIndex(x => new { x.CrisisEventId, x.Status });
        e.HasOne<CrisisEvent>().WithMany().HasForeignKey(x => x.CrisisEventId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<AssistanceRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class TaskAssignmentConfiguration : IEntityTypeConfiguration<TaskAssignment>
{
    public void Configure(EntityTypeBuilder<TaskAssignment> e)
    {
        e.Property(x => x.Note).HasMaxLength(300);
        e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);

        e.HasIndex(x => new { x.TaskId, x.VolunteerId });
        e.HasIndex(x => new { x.VolunteerId, x.Status });
        e.HasOne<VolunteerTask>().WithMany().HasForeignKey(x => x.TaskId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<Volunteer>().WithMany().HasForeignKey(x => x.VolunteerId).OnDelete(DeleteBehavior.Restrict);
    }
}
