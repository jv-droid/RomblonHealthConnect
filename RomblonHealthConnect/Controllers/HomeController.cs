using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RomblonHealthConnect.Data;
using RomblonHealthConnect.Models;
using RomblonHealthConnect.Models.Enums;
using RomblonHealthConnect.ViewModels.Referrals;

namespace RomblonHealthConnect.Controllers;

[Authorize]
public class HomeController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<HomeController> _logger;

    public HomeController(ApplicationDbContext context, ILogger<HomeController> logger)
    {
        _context = context;
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    [AllowAnonymous]
    public IActionResult Privacy()
    {
        return View();
    }

    /// <summary>Friendly page for 404 and other status-only responses.</summary>
    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult StatusCode(int? code)
    {
        ViewData["StatusCode"] = code ?? 404;
        return View("StatusCode");
    }

    /// <summary>
    /// Everything the provincial dashboard renders, in one call: facilities for
    /// the map, the recent referral feed, the on-duty roster, and the summary
    /// figures. Reading straight from the database is what makes a newly
    /// registered facility appear on the map without any further wiring.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> NetworkData(CancellationToken cancellationToken)
    {
        var todayUtc = DateTime.UtcNow.Date;
        var tomorrowUtc = todayUtc.AddDays(1);

        var hospitals = await _context.Hospitals
            .Where(h => h.IsActive)
            .OrderBy(h => h.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var doctors = await _context.Doctors
            .Where(d => d.IsActive)
            .Include(d => d.PrimarySpecialization)
            .Include(d => d.Hospital)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        var referrals = await _context.Referrals
            .Include(r => r.Patient)
            .Include(r => r.OriginHospital)
            .Include(r => r.DestinationHospital)
            .OrderByDescending(r => r.CreatedUtc)
            .Take(120)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Per-facility rollups computed once, then attached to each record.
        var doctorsByHospital = doctors
            .GroupBy(d => d.HospitalId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var facilities = hospitals.Select(h =>
        {
            doctorsByHospital.TryGetValue(h.Id, out var staff);
            staff ??= [];

            var incoming = referrals.Count(r => r.DestinationHospitalId == h.Id && !r.IsArchived);
            var outgoing = referrals.Count(r => r.OriginHospitalId == h.Id && !r.IsArchived);

            return new
            {
                id = h.Code,
                hospitalId = h.Id,
                name = h.Name,
                type = ReferralDisplay.FacilityTypeKey(h.FacilityType),
                typeLabel = h.FacilityTypeLabel,
                municipality = h.Municipality,
                address = h.Address,
                contact = h.ContactNumber ?? string.Empty,
                coordinates = new[] { h.Longitude, h.Latitude },
                status = h.Status.ToString().ToLowerInvariant(),
                emergency = h.HasEmergency,
                doctorsAvailable = staff.Count(d => d.Availability != DoctorAvailability.OffDuty),
                bedsAvailable = h.AvailableBeds,
                bedsTotal = h.TotalBeds,
                specializations = staff
                    .Select(d => d.PrimarySpecialization.Name)
                    .Distinct()
                    .OrderBy(n => n)
                    .ToList(),
                incomingReferrals = incoming,
                outgoingReferrals = outgoing,
                patientsToday = referrals.Count(r =>
                    r.DestinationHospitalId == h.Id && r.CreatedUtc >= todayUtc && r.CreatedUtc < tomorrowUtc),
                admissionsToday = referrals.Count(r =>
                    r.DestinationHospitalId == h.Id && r.Status == ReferralStatus.Accepted
                    && r.RespondedUtc >= todayUtc && r.RespondedUtc < tomorrowUtc),
                updatedMinutesAgo = (int)Math.Max(0, (DateTime.UtcNow - h.LastUpdatedUtc).TotalMinutes)
            };
        }).ToList();

        var recentReferrals = referrals.Take(8).Select(r => new
        {
            reference = r.ReferralNumber,
            origin = r.OriginHospital.Code,
            destination = r.DestinationHospital.Code,
            originName = r.OriginHospital.Name,
            destinationName = r.DestinationHospital.Name,
            status = MapReferralStatus(r.Status),
            time = r.CreatedUtc.ToLocalTime().ToString("HH:mm")
        }).ToList();

        var roster = doctors
            .Where(d => d.Availability != DoctorAvailability.OffDuty)
            .OrderBy(d => d.Availability)
            .ThenBy(d => d.LastName)
            .Take(8)
            .Select(d => new
            {
                name = d.FullName,
                specialty = d.PrimarySpecialization.Name,
                hospital = d.Hospital.Name,
                availability = MapAvailability(d.Availability)
            }).ToList();

        var overview = new
        {
            lastSyncMinutesAgo = 0,
            activity = new
            {
                created = referrals.Count(r => r.CreatedUtc >= todayUtc && r.CreatedUtc < tomorrowUtc),
                accepted = referrals.Count(r =>
                    r.RespondedUtc >= todayUtc && r.RespondedUtc < tomorrowUtc
                    && r.Status == ReferralStatus.Accepted),
                patients = referrals.Count(r => r.CreatedUtc >= todayUtc && r.CreatedUtc < tomorrowUtc)
            },
            availability = new
            {
                available = doctors.Count(d => d.Availability == DoctorAvailability.Available),
                onDuty = doctors.Count(d =>
                    d.Availability == DoctorAvailability.OnCall || d.Availability == DoctorAvailability.InSurgery),
                unavailable = doctors.Count(d => d.Availability == DoctorAvailability.OffDuty)
            }
        };

        return Json(new
        {
            facilities,
            referrals = recentReferrals,
            doctors = roster,
            overview
        });
    }

    /// <summary>Maps the persisted status onto the keys the dashboard renders.</summary>
    private static string MapReferralStatus(ReferralStatus status) => status switch
    {
        ReferralStatus.Draft => "pending",
        ReferralStatus.Submitted => "pending",
        ReferralStatus.Accepted => "accepted",
        ReferralStatus.Rejected => "declined",
        ReferralStatus.Cancelled => "declined",
        ReferralStatus.Completed => "completed",
        ReferralStatus.Expired => "declined",
        _ => "pending"
    };

    private static string MapAvailability(DoctorAvailability availability) => availability switch
    {
        DoctorAvailability.Available => "available",
        DoctorAvailability.OnCall => "on-call",
        DoctorAvailability.InSurgery => "in-surgery",
        _ => "off-duty"
    };

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
