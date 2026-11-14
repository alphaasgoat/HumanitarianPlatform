namespace Humanitarian.Application.Common;

/// <summary>Role names used in JWT claims and [Authorize(Roles=...)]. Must equal UserRole enum names.</summary>
public static class Roles
{
    public const string Citizen = "Citizen";
    public const string EmergencyCoordinator = "EmergencyCoordinator";
    public const string CaseManager = "CaseManager";
    public const string LogisticsManager = "LogisticsManager";
    public const string ShelterAdministrator = "ShelterAdministrator";
    public const string VolunteerCoordinator = "VolunteerCoordinator";
    public const string Donor = "Donor";
    public const string PartnerOfficer = "PartnerOfficer";
    public const string SystemAdministrator = "SystemAdministrator";

    public const string CoordinatorOrAdmin = EmergencyCoordinator + "," + SystemAdministrator;
}
