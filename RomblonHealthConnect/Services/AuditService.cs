using System.Text.Json;
using RomblonHealthConnect.Data;
using RomblonHealthConnect.Interfaces;
using RomblonHealthConnect.Models;

namespace RomblonHealthConnect.Services;

/// <summary>
/// Appends entries to the audit trail.
///
/// Values are serialised through a redaction pass so a caller cannot record a
/// password, hash, token, or connection string even by mistake.
/// </summary>
public class AuditService : IAuditService
{
    /// <summary>Property names never written to the audit log, matched case-insensitively.</summary>
    private static readonly string[] ForbiddenKeys =
    [
        "password", "passwordhash", "hash", "token", "secret", "connectionstring",
        "securitystamp", "concurrencystamp", "twofactor", "apikey"
    ];

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<AuditService> _logger;

    public AuditService(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        ILogger<AuditService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task LogAsync(
        string action,
        string entityName,
        string? entityId,
        string description,
        object? oldValues = null,
        object? newValues = null,
        CancellationToken cancellationToken = default)
    {
        var entry = new AuditLog
        {
            UserId = _currentUser.UserId,
            UserDisplayName = _currentUser.DisplayName,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            Description = Truncate(description, 1000),
            OldValuesJson = Serialize(oldValues),
            NewValuesJson = Serialize(newValues),
            IpAddress = _currentUser.IpAddress,
            UserAgent = _currentUser.UserAgent,
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            _context.AuditLogs.Add(entry);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Auditing must never break the operation being audited; the failure
            // is surfaced in the application log instead.
            _logger.LogError(ex, "Failed to write audit entry {Action} for {Entity} {EntityId}.",
                action, entityName, entityId);
        }
    }

    /// <summary>Serialises to JSON with sensitive properties removed.</summary>
    private static string? Serialize(object? value)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            var element = JsonSerializer.SerializeToElement(value, SerializerOptions);

            if (element.ValueKind != JsonValueKind.Object)
            {
                return Truncate(element.ToString(), 4000);
            }

            var safe = new Dictionary<string, object?>();

            foreach (var property in element.EnumerateObject())
            {
                var isForbidden = ForbiddenKeys.Any(key =>
                    property.Name.Contains(key, StringComparison.OrdinalIgnoreCase));

                safe[property.Name] = isForbidden ? "[redacted]" : property.Value.ToString();
            }

            return Truncate(JsonSerializer.Serialize(safe, SerializerOptions), 4000);
        }
        catch
        {
            return null;
        }
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
