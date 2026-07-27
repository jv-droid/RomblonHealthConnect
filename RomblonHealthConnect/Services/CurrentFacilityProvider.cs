using Microsoft.EntityFrameworkCore;
using RomblonHealthConnect.Data;
using RomblonHealthConnect.Interfaces;

namespace RomblonHealthConnect.Services;

public class CurrentFacilityProvider : ICurrentFacilityProvider
{
    private const string SessionKey = "rhc.currentHospitalId";
    private const string DefaultHospitalCode = "rph-romblon";

    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ApplicationDbContext _context;

    public CurrentFacilityProvider(IHttpContextAccessor httpContextAccessor, ApplicationDbContext context)
    {
        _httpContextAccessor = httpContextAccessor;
        _context = context;
    }

    public async Task<int> GetHospitalIdAsync(CancellationToken cancellationToken = default)
    {
        var session = _httpContextAccessor.HttpContext?.Session;

        if (session is not null && session.TryGetValue(SessionKey, out _))
        {
            var stored = session.GetInt32(SessionKey);
            if (stored.HasValue)
            {
                return stored.Value;
            }
        }

        var hospitalId = await _context.Hospitals
            .Where(h => h.Code == DefaultHospitalCode)
            .Select(h => h.Id)
            .FirstOrDefaultAsync(cancellationToken);

        // Fall back to any facility so a partially seeded database still renders.
        if (hospitalId == 0)
        {
            hospitalId = await _context.Hospitals
                .OrderBy(h => h.Id)
                .Select(h => h.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        session?.SetInt32(SessionKey, hospitalId);
        return hospitalId;
    }

    public void SetHospitalId(int hospitalId)
    {
        _httpContextAccessor.HttpContext?.Session.SetInt32(SessionKey, hospitalId);
    }
}
