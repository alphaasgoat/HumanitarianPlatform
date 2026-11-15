using Humanitarian.Application.Common;

namespace Humanitarian.Application.Requests;

public static class RequestRoles
{
    /// <summary>Roles allowed to review requests and AI recommendations.</summary>
    public const string Staff = Roles.CaseManager + "," + Roles.EmergencyCoordinator;
}
