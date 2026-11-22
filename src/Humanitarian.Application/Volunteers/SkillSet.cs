using Humanitarian.Domain.Enums;

namespace Humanitarian.Application.Volunteers;

public static class SkillSet
{
    public static string Format(IEnumerable<VolunteerSkill> skills) =>
        string.Join(",", skills.Distinct().OrderBy(s => s));

    public static IReadOnlyList<VolunteerSkill> Parse(string? csv)
    {
        var list = new List<VolunteerSkill>();
        if (string.IsNullOrWhiteSpace(csv)) return list;
        foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            if (Enum.TryParse<VolunteerSkill>(part, out var skill)) list.Add(skill);
        return list;
    }

    public static IReadOnlyList<string> Names(string? csv) => Parse(csv).Select(s => s.ToString()).ToList();
}

public static class TimeRules
{
    /// <summary>Times from the client are treated as UTC unless they carry another offset.</summary>
    public static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(value, DateTimeKind.Utc) : value.ToUniversalTime();
}
