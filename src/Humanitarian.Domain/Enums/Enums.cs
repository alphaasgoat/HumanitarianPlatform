namespace Humanitarian.Domain.Enums;

public enum UserRole
{
    Citizen = 1,
    EmergencyCoordinator,
    CaseManager,
    LogisticsManager,
    ShelterAdministrator,
    VolunteerCoordinator,
    Donor,
    PartnerOfficer,
    SystemAdministrator
}

public enum OrganisationType { International, Government, Ngo, Medical, Logistics }
public enum CrisisType { Flood, Landslide, Earthquake, Storm, DiseaseOutbreak, Displacement, Other }
public enum CrisisStatus { Active, Monitoring, Closed }
