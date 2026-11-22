using Humanitarian.Application.Common;

namespace Humanitarian.Application.Shelters;

public static class ShelterRoles
{
    /// <summary>Manage shelters and place households.</summary>
    public const string Manage = Roles.ShelterAdministrator + "," + Roles.EmergencyCoordinator;

    public const string View = Roles.ShelterAdministrator + "," + Roles.EmergencyCoordinator + "," + Roles.CaseManager;

    /// <summary>Supply requests are raised by shelters and answered by logistics.</summary>
    public const string Supplies = Roles.ShelterAdministrator + "," + Roles.EmergencyCoordinator + "," + Roles.LogisticsManager;
}
