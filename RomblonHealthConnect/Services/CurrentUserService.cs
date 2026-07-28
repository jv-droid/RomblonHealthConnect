using System.Security.Claims;
using RomblonHealthConnect.Constants;
using RomblonHealthConnect.Interfaces;

namespace RomblonHealthConnect.Services;

/// <summary>
/// Reads the signed-in user from the authentication cookie.
///
/// HospitalId comes from a claim issued at sign-in, never from a form field,
/// route value, or JavaScript, so a hospital-scoped user cannot widen their own
/// scope by editing a request.
/// </summary>
public class CurrentUserService : ICurrentUserService
{
    /// <summary>Claim carrying the assigned facility; absent for province-wide users.</summary>
    public const string HospitalIdClaim = "rhc:hospitalId";
    public const string DisplayNameClaim = "rhc:displayName";
    public const string PositionTitleClaim = "rhc:positionTitle";

    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public string? UserId => Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

    public string? UserName => Principal?.Identity?.Name;

    public string? DisplayName =>
        Principal?.FindFirstValue(DisplayNameClaim) ?? UserName;

    public string? PositionTitle => Principal?.FindFirstValue(PositionTitleClaim);

    public int? HospitalId
    {
        get
        {
            var raw = Principal?.FindFirstValue(HospitalIdClaim);
            return int.TryParse(raw, out var id) ? id : null;
        }
    }

    public IReadOnlyList<string> Roles =>
        Principal?.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList() ?? [];

    public bool IsInRole(string role) => Principal?.IsInRole(role) ?? false;

    public bool HasProvinceWideScope =>
        Constants.Roles.ProvinceWide.Any(role => IsInRole(role));

    /// <summary>
    /// Whether the user may act on data belonging to the given facility.
    /// Province-wide roles may act anywhere; everyone else is confined to their
    /// assigned facility.
    /// </summary>
    public bool CanAccessHospital(int hospitalId)
    {
        if (!IsAuthenticated)
        {
            return false;
        }

        if (HasProvinceWideScope)
        {
            return true;
        }

        return HospitalId.HasValue && HospitalId.Value == hospitalId;
    }

    public string? IpAddress =>
        _httpContextAccessor.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public string? UserAgent
    {
        get
        {
            var agent = _httpContextAccessor.HttpContext?.Request.Headers.UserAgent.ToString();
            if (string.IsNullOrWhiteSpace(agent))
            {
                return null;
            }

            return agent.Length > 400 ? agent[..400] : agent;
        }
    }
}
