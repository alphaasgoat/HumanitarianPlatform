using Humanitarian.Application.Common;

namespace Humanitarian.Application.Volunteers;

public static class VolunteerRoles
{
    /// <summary>Verify volunteers, create tasks and assign people.</summary>
    public const string Coordinate = Roles.VolunteerCoordinator + "," + Roles.EmergencyCoordinator;

    public const string View = Roles.VolunteerCoordinator + "," + Roles.EmergencyCoordinator + "," + Roles.CaseManager;
}
