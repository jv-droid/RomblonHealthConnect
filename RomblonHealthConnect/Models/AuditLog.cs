namespace RomblonHealthConnect.Models;

/// <summary>
/// Append-only record of security-relevant and master-data actions.
///
/// Deliberately excluded: passwords, password hashes, tokens, connection
/// strings, attachment contents, and clinical patient detail. Only the entity
/// identifier and a short description are kept for patient-adjacent records.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }

    public string? UserId { get; set; }

    public string? UserDisplayName { get; set; }

    /// <summary>For example "Login", "HospitalUpdated", "RoleAssigned".</summary>
    public string Action { get; set; } = string.Empty;

    public string EntityName { get; set; } = string.Empty;

    public string? EntityId { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? OldValuesJson { get; set; }

    public string? NewValuesJson { get; set; }

    public string? IpAddress { get; set; }

    public string? UserAgent { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>Canonical action names, so audit queries do not depend on free text.</summary>
public static class AuditActions
{
    public const string LoginSucceeded = "LoginSucceeded";
    public const string LoginFailed = "LoginFailed";
    public const string LoginBlockedInactive = "LoginBlockedInactive";
    public const string LoginLockedOut = "LoginLockedOut";
    public const string Logout = "Logout";
    public const string PasswordChanged = "PasswordChanged";

    public const string HospitalCreated = "HospitalCreated";
    public const string HospitalUpdated = "HospitalUpdated";
    public const string HospitalActivated = "HospitalActivated";
    public const string HospitalDeactivated = "HospitalDeactivated";
    public const string HospitalDeleted = "HospitalDeleted";

    public const string UserCreated = "UserCreated";
    public const string UserActivated = "UserActivated";
    public const string UserDeactivated = "UserDeactivated";
    public const string RoleAssignmentChanged = "RoleAssignmentChanged";
    public const string AccessDenied = "AccessDenied";
}
