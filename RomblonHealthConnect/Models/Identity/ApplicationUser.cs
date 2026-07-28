using Microsoft.AspNetCore.Identity;

namespace RomblonHealthConnect.Models.Identity;

/// <summary>
/// Application user. Optionally assigned to one existing <see cref="Hospital"/>;
/// provincial-level users (PHO, executives, auditors) leave HospitalId null and
/// are scoped province-wide instead.
/// </summary>
public class ApplicationUser : IdentityUser<string>
{
    public ApplicationUser()
    {
        // Identity's parameterless base leaves Id empty for manually constructed users.
        Id = Guid.NewGuid().ToString();
        CreatedAt = DateTime.UtcNow;
    }

    public string FirstName { get; set; } = string.Empty;

    public string? MiddleName { get; set; }

    public string LastName { get; set; } = string.Empty;

    /// <summary>Shown in the application shell; falls back to the full name.</summary>
    public string DisplayName { get; set; } = string.Empty;

    public string? PositionTitle { get; set; }

    /// <summary>
    /// Assigned facility. Null means province-wide scope.
    /// Matches the existing Hospital primary key type (int).
    /// </summary>
    public int? HospitalId { get; set; }

    public Hospital? Hospital { get; set; }

    /// <summary>Deactivated accounts are refused at sign-in.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime? LastLoginAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    public string FullName => string.IsNullOrWhiteSpace(MiddleName)
        ? $"{FirstName} {LastName}".Trim()
        : $"{FirstName} {MiddleName} {LastName}".Trim();
}
