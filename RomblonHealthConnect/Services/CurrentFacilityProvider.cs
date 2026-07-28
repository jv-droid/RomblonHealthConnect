using Microsoft.EntityFrameworkCore;
using RomblonHealthConnect.Data;
using RomblonHealthConnect.Interfaces;

namespace RomblonHealthConnect.Services;

/// <summary>
/// Resolves the facility the request is acting for.
///
/// Phase 1 made the signed-in user the authority: a hospital-scoped account
/// always acts for its assigned facility and cannot change that. Province-wide
/// roles have no fixed facility, so they may select one, which is kept in
/// session purely as a UI preference.
/// </summary>
public class CurrentFacilityProvider : ICurrentFacilityProvider
{
    private const string SessionKey = "rhc.currentHospitalId";
    private const string DefaultHospitalCode = "rph-romblon";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ICurrentUserService _currentUser;
    private readonly ApplicationDbContext _context;

    public CurrentFacilityProvider(
        IHttpContextAccessor httpContextAccessor,
        ICurrentUserService currentUser,
        ApplicationDbContext context)
    {
        _httpContextAccessor = httpContextAccessor;
        _currentUser = currentUser;
        _context = context;
    }

    public async Task<int> GetHospitalIdAsync(CancellationToken cancellationToken = default)
    {
        // A hospital-scoped user is pinned to their own facility. This is the
        // security-relevant branch and must ignore session and request values.
        if (_currentUser.IsAuthenticated
            && !_currentUser.HasProvinceWideScope
            && _currentUser.HospitalId.HasValue)
        {
            return _currentUser.HospitalId.Value;
        }

        // Province-wide users may focus on a facility; that choice is cosmetic.
        var session = _httpContextAccessor.HttpContext?.Session;

        if (session is not null)
        {
            var stored = session.GetInt32(SessionKey);
            if (stored.HasValue)
            {
                return stored.Value;
            }
        }

        var hospitalId = await _context.Hospitals
            .Where(h => h.Code == DefaultHospitalCode && h.IsActive && !h.IsDeleted)
            .Select(h => h.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (hospitalId == 0)
        {
            hospitalId = await _context.Hospitals
                .Where(h => h.IsActive && !h.IsDeleted)
                .OrderBy(h => h.Id)
                .Select(h => h.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        session?.SetInt32(SessionKey, hospitalId);
        return hospitalId;
    }

    /// <summary>
    /// Only province-wide roles may switch focus; for everyone else the call is
    /// ignored so a request cannot move a user out of their own facility.
    /// </summary>
    public void SetHospitalId(int hospitalId)
    {
        if (_currentUser.IsAuthenticated && !_currentUser.HasProvinceWideScope)
        {
            return;
        }

        _httpContextAccessor.HttpContext?.Session.SetInt32(SessionKey, hospitalId);
    }
}
