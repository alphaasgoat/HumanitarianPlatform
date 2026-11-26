namespace Humanitarian.Application.Metrics;

public record RequestMetricsDto(
    int Total, int Open, int Unresolved, int Overdue, int Escalated, int Closed,
    IReadOnlyDictionary<string, int> ByStatus, IReadOnlyDictionary<string, int> OpenByUrgency,
    IReadOnlyDictionary<string, int> OpenByCategory);

public record ResponseTimeDto(int RequestsWithDecision, double? AverageFirstDecisionMinutes, double? AverageResolutionHours);

public record AiMetricsDto(
    int Recommendations, int Decided, int Approved, int Modified, int Rejected, int Deferred,
    double OverrideRatePercent, double FallbackRatePercent, double AverageConfidence);

public record ResourceCategoryMetricsDto(
    string Category, int Items, int QuantityTotal, int QuantityAvailable, double UtilisationPercent, int LowStockItems);

public record ShelterMetricsDto(int Shelters, int Open, int Capacity, int Occupancy, double OccupancyPercent, int Full);

public record VolunteerMetricsDto(
    int Total, int Verified, int Active, int OpenTasks, int FilledTasks, int CompletedTasks,
    double AcceptanceRatePercent, double HoursGiven);

public record CoverageDto(int OpenRequestsWithLocation, int WithinRadius, double CoveragePercent, double? AverageDistanceKm, double RadiusKm);

public record DeliveryMetricsDto(int Pending, int Sent, int Failed);

public record StaffWorkloadDto(string Email, int Decisions, int Overrides);

public record OperationalMetricsDto(
    DateTime GeneratedAtUtc, Guid? CrisisEventId, RequestMetricsDto Requests, ResponseTimeDto ResponseTimes, AiMetricsDto Ai,
    IReadOnlyList<ResourceCategoryMetricsDto> Resources, ShelterMetricsDto Shelters, VolunteerMetricsDto Volunteers,
    CoverageDto Coverage, DeliveryMetricsDto Deliveries, IReadOnlyList<StaffWorkloadDto> Workload);
