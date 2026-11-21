using Humanitarian.Application.Common;

namespace Humanitarian.Application.Resources;

public static class ResourceRoles
{
    /// <summary>Manage inventory and allocate resources.</summary>
    public const string Manage = Roles.LogisticsManager + "," + Roles.EmergencyCoordinator;

    /// <summary>Read-only access to inventory and allocations.</summary>
    public const string View = Roles.LogisticsManager + "," + Roles.EmergencyCoordinator + "," + Roles.CaseManager;
}
