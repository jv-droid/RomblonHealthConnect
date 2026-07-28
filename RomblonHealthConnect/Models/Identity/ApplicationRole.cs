using Microsoft.AspNetCore.Identity;

namespace RomblonHealthConnect.Models.Identity;

/// <summary>
/// Application role. System roles are seeded by the platform and must not be
/// renamed or deleted through the administration UI.
/// </summary>
public class ApplicationRole : IdentityRole<string>
{
    public ApplicationRole()
    {
        Id = Guid.NewGuid().ToString();
        CreatedAt = DateTime.UtcNow;
    }

    public ApplicationRole(string roleName, string? description, bool isSystemRole = true)
        : this()
    {
        Name = roleName;
        NormalizedName = roleName.ToUpperInvariant();
        Description = description;
        IsSystemRole = isSystemRole;
    }

    public string? Description { get; set; }

    public bool IsSystemRole { get; set; }

    public DateTime CreatedAt { get; set; }
}
