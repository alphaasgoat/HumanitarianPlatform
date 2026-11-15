using Humanitarian.Domain.Enums;

namespace Humanitarian.Application.Requests.Ai;

public static class PriorityScoring
{
    public static int BaseScore(UrgencyLevel urgency) => urgency switch
    {
        UrgencyLevel.Low => 20,
        UrgencyLevel.Medium => 40,
        UrgencyLevel.High => 65,
        UrgencyLevel.Critical => 85,
        _ => 40
    };

    public static UrgencyLevel ToUrgency(int score) =>
        score >= 85 ? UrgencyLevel.Critical :
        score >= 60 ? UrgencyLevel.High :
        score >= 35 ? UrgencyLevel.Medium :
        UrgencyLevel.Low;
}
