namespace RomblonHealthConnect.Interfaces;

/// <summary>
/// The signed-in user, as read from the authentication cookie.
/// Server-side authority for audit stamping and hospital-level data scoping.
/// </summary>
public interface ICurrentUserService
{
    bool IsAuthenticated { get; }

    string? UserId { get; }

    string? UserName { get; }

    string? DisplayName { get; }

    string? PositionTitle { get; }

    /// <summary>Assigned facility, or null for province-wide roles.</summary>
    int? HospitalId { get; }

    IReadOnlyList<string> Roles { get; }

    bool IsInRole(string role);

    /// <summary>True for roles whose remit is the whole province.</summary>
    bool HasProvinceWideScope { get; }

    /// <summary>Server-side scope check. Never infer access from the request.</summary>
    bool CanAccessHospital(int hospitalId);

    string? IpAddress { get; }

    string? UserAgent { get; }
}

/// <summary>Writes entries to the audit trail.</summary>
public interface IAuditService
{
    Task LogAsync(
        string action,
        string entityName,
        string? entityId,
        string description,
        object? oldValues = null,
        object? newValues = null,
        CancellationToken cancellationToken = default);
}
