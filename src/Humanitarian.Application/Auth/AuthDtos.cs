using System.ComponentModel.DataAnnotations;
using Humanitarian.Domain.Enums;

namespace Humanitarian.Application.Auth;

public record RegisterCitizenRequest(
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).{10,100}$",
        ErrorMessage = "Password must be 10-100 characters and contain a letter and a digit.")] string Password,
    [Required, StringLength(150, MinimumLength = 2)] string FullName,
    [Phone, StringLength(30)] string? Phone,
    [StringLength(10)] string? PreferredLanguage);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password);

public record UserDto(Guid Id, string Email, string FullName, string Role, Guid? OrganisationId, bool IsActive);

public record AuthResponse(string AccessToken, DateTime ExpiresAtUtc, UserDto User);

public record CreateStaffRequest(
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, RegularExpression(@"^(?=.*[A-Za-z])(?=.*\d).{10,100}$",
        ErrorMessage = "Password must be 10-100 characters and contain a letter and a digit.")] string Password,
    [Required, StringLength(150, MinimumLength = 2)] string FullName,
    [EnumDataType(typeof(UserRole))] UserRole Role,
    Guid? OrganisationId);

