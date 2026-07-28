using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RomblonHealthConnect.Data;
using RomblonHealthConnect.ViewModels.Referrals;

namespace RomblonHealthConnect.Controllers.Api;

/// <summary>
/// Read-only facility data for the GIS map.
///
/// Returns only what the map draws. Audit metadata, contact details, licence
/// numbers, and accreditation numbers are deliberately excluded.
/// </summary>
[ApiController]
[Route("api/hospitals")]
[Authorize]
public class HospitalsApiController : ControllerBase
{
    private readonly ApplicationDbContext _context;

    public HospitalsApiController(ApplicationDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Active, non-deleted facilities that carry usable coordinates.
    /// </summary>
    [HttpGet("map")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Map(CancellationToken cancellationToken)
    {
        var facilities = await _context.Hospitals
            .AsNoTracking()
            .Where(h => h.IsActive
                        && !h.IsDeleted
                        // A facility without a real position cannot be mapped.
                        && h.Latitude != 0
                        && h.Longitude != 0
                        && h.Latitude >= -90 && h.Latitude <= 90
                        && h.Longitude >= -180 && h.Longitude <= 180)
            .OrderBy(h => h.Name)
            .Select(h => new HospitalMapPointDto(
                h.Id,
                h.Code,
                h.Name,
                h.FacilityType.ToString(),
                ReferralDisplay.FacilityTypeKey(h.FacilityType),
                h.Municipality,
                h.Latitude,
                h.Longitude,
                h.Status.ToString(),
                h.HasEmergency,
                h.TotalBeds,
                h.AvailableBeds,
                h.LastUpdatedUtc))
            .ToListAsync(cancellationToken);

        return Ok(facilities);
    }
}

/// <summary>Minimal projection for map rendering.</summary>
public record HospitalMapPointDto(
    int Id,
    string FacilityCode,
    string Name,
    string FacilityType,
    string FacilityTypeKey,
    string Municipality,
    double Latitude,
    double Longitude,
    string NetworkStatus,
    bool IsEmergencyCapable,
    int BedCapacity,
    int AvailableBeds,
    DateTime LastReportedAt);
