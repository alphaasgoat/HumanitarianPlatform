using Humanitarian.Domain.Entities;
using Humanitarian.Domain.Enums;

namespace Humanitarian.Application.Resources;

/// <summary>Business rules shared by matching, planning and allocation.</summary>
public static class ResourceRules
{
    /// <summary>Assumption (documented in the report): food and water are issued for three days per person.</summary>
    public const int SupplyDays = 3;

    /// <summary>Which kind of stock can satisfy a request. Shelter requests are handled by the shelter module.</summary>
    public static ResourceCategory? ResourceCategoryFor(RequestCategory category) => category switch
    {
        RequestCategory.Food => ResourceCategory.Food,
        RequestCategory.Water => ResourceCategory.Water,
        RequestCategory.Medicine => ResourceCategory.Medicine,
        RequestCategory.Transportation => ResourceCategory.Vehicle,
        RequestCategory.Rescue => ResourceCategory.Vehicle,
        RequestCategory.Accessibility => ResourceCategory.Equipment,
        RequestCategory.Other => ResourceCategory.Other,
        _ => null
    };

    /// <summary>Estimated quantity a request needs. Staff can still allocate less, never more.</summary>
    public static int EstimateQuantity(AssistanceRequest request) => request.Category switch
    {
        RequestCategory.Food or RequestCategory.Water => Math.Max(1, request.PeopleAffected) * SupplyDays,
        _ => 1
    };
}
