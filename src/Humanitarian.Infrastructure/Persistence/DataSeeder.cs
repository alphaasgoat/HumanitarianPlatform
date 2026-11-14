using Humanitarian.Application.Common;
using Humanitarian.Domain.Entities;
using Humanitarian.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Humanitarian.Infrastructure.Persistence;

/// <summary>Development-only demo data. Credentials are listed in the README.</summary>
public static class DataSeeder
{
    public static async Task SeedAsync(AppDbContext db, IPasswordHasher hasher)
    {
        if (await db.Users.AnyAsync()) return;

        var org = new Organisation
        {
            Name = "Global Relief Coordination (Demo)", Type = OrganisationType.International,
            Country = "Sri Lanka", ContactEmail = "ops@demo.org"
        };
        db.Organisations.Add(org);

        User U(string email, string name, UserRole role, string pw, bool withOrg = true) => new()
        {
            Email = email, FullName = name, Role = role, PasswordHash = hasher.Hash(pw),
            OrganisationId = withOrg ? org.Id : null
        };

        db.Users.AddRange(
            U("admin@demo.org", "System Admin", UserRole.SystemAdministrator, "Admin#12345"),
            U("coordinator@demo.org", "Emergency Coordinator", UserRole.EmergencyCoordinator, "Coord#12345"),
            U("casemanager@demo.org", "Case Manager", UserRole.CaseManager, "Case#123456"),
            U("logistics@demo.org", "Logistics Manager", UserRole.LogisticsManager, "Logi#123456"),
            U("shelter@demo.org", "Shelter Administrator", UserRole.ShelterAdministrator, "Shel#123456"),
            U("volunteers@demo.org", "Volunteer Coordinator", UserRole.VolunteerCoordinator, "Volu#123456"),
            U("citizen@demo.org", "Demo Citizen", UserRole.Citizen, "Citizen#12345", withOrg: false));

        await db.SaveChangesAsync();
    }
}
