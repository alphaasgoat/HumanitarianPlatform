using Humanitarian.Domain.Common;
using Humanitarian.Domain.Enums;

namespace Humanitarian.Domain.Entities;

public class Organisation : BaseEntity
{
    public string Name { get; set; } = "";
    public OrganisationType Type { get; set; }
    public string Country { get; set; } = "";
    public string? ContactEmail { get; set; }
    public bool IsActive { get; set; } = true;
    public ICollection<User> Users { get; set; } = new List<User>();
}
