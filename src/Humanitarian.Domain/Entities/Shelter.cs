using Humanitarian.Domain.Common;
using Humanitarian.Domain.Enums;

namespace Humanitarian.Domain.Entities;

public class Shelter : BaseEntity
{
    /// <summary>Null = a permanent facility that serves every crisis.</summary>
    public Guid? CrisisEventId { get; set; }

    public string Name { get; set; } = "";
    public string Address { get; set; } = "";
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public int Capacity { get; set; }
    public int Occupancy { get; set; }

    public bool HasMedicalSupport { get; set; }
    public bool WheelchairAccessible { get; set; }
    public bool ProvidesFood { get; set; }
    public bool ProvidesWater { get; set; }

    public ShelterStatus Status { get; set; } = ShelterStatus.Open;

    /// <summary>Approved for the public list that affected people can see.</summary>
    public bool IsPublic { get; set; } = true;

    public string? Notes { get; set; }

    /// <summary>Optimistic-concurrency token: two people cannot fill the last bed at the same time.</summary>
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public int AvailableSpaces => Math.Max(0, Capacity - Occupancy);
}
