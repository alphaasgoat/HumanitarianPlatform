using Humanitarian.Domain.Common;
using Humanitarian.Domain.Enums;

namespace Humanitarian.Domain.Entities;

public class User : BaseEntity
{
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string FullName { get; set; } = "";
    public string? Phone { get; set; }
    public string PreferredLanguage { get; set; } = "en";
    public UserRole Role { get; set; }
    public Guid? OrganisationId { get; set; }
    public Organisation? Organisation { get; set; }
    public bool IsActive { get; set; } = true;

    // Account lockout (brute-force protection)
    public int FailedLoginCount { get; set; }
    public DateTime? LockoutEndUtc { get; set; }

    public bool IsLockedOut(DateTime nowUtc) => LockoutEndUtc.HasValue && LockoutEndUtc.Value > nowUtc;
}
