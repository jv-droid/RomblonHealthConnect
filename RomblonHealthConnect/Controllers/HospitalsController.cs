using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RomblonHealthConnect.Constants;
using RomblonHealthConnect.Interfaces;
using RomblonHealthConnect.Models;
using RomblonHealthConnect.Models.Enums;
using RomblonHealthConnect.ViewModels.Hospitals;

namespace RomblonHealthConnect.Controllers;

/// <summary>
/// Facility registry. Anything saved here appears on the provincial map
/// immediately, because the map reads the same records.
/// </summary>
[Authorize(Policy = Policies.CanManageHospitalData)]
public class HospitalsController : Controller
{
    private readonly IHospitalRepository _hospitals;
    private readonly IDoctorRepository _doctors;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;
    private readonly ILogger<HospitalsController> _logger;

    public HospitalsController(
        IHospitalRepository hospitals,
        IDoctorRepository doctors,
        ICurrentUserService currentUser,
        IAuditService audit,
        ILogger<HospitalsController> logger)
    {
        _hospitals = hospitals;
        _doctors = doctors;
        _currentUser = currentUser;
        _audit = audit;
        _logger = logger;
    }

    /// <summary>
    /// Server-side record scope. A hospital-scoped user may only touch their own
    /// facility, whatever id arrives in the route, form, or query string.
    /// </summary>
    private bool CanAccess(int hospitalId) => _currentUser.CanAccessHospital(hospitalId);

    /* ------------------------------------------------------------------ */
    /* List                                                                */
    /* ------------------------------------------------------------------ */

    public async Task<IActionResult> Index(
        string? searchTerm,
        string? municipality,
        FacilityType? facilityType,
        CancellationToken cancellationToken)
    {
        var all = await _hospitals.GetAllIncludingInactiveAsync(cancellationToken);

        var filtered = all.AsEnumerable();

        // Hospital-scoped roles only ever see their own facility.
        if (!_currentUser.HasProvinceWideScope && _currentUser.HospitalId.HasValue)
        {
            var ownId = _currentUser.HospitalId.Value;
            filtered = filtered.Where(h => h.Id == ownId);
        }

        if (!string.IsNullOrWhiteSpace(searchTerm))
        {
            var term = searchTerm.Trim();
            filtered = filtered.Where(h =>
                h.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || h.Municipality.Contains(term, StringComparison.OrdinalIgnoreCase)
                || h.Code.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(municipality))
        {
            filtered = filtered.Where(h => h.Municipality == municipality);
        }

        if (facilityType.HasValue)
        {
            filtered = filtered.Where(h => h.FacilityType == facilityType.Value);
        }

        var rows = new List<HospitalListItemViewModel>();
        foreach (var hospital in filtered)
        {
            var staff = await _doctors.GetByHospitalAsync(hospital.Id, cancellationToken);
            rows.Add(HospitalListItemViewModel.FromEntity(hospital, staff.Count));
        }

        ViewData["Title"] = "Hospitals";
        ViewData["Subtitle"] = "Facility registry for the provincial network";
        ViewData["ActiveNav"] = "hospitals";

        return View(new HospitalListViewModel
        {
            Hospitals = rows,
            SearchTerm = searchTerm,
            Municipality = municipality,
            FacilityType = facilityType,
            Municipalities = RomblonGeography.Municipalities
        });
    }

    /* ------------------------------------------------------------------ */
    /* Create                                                              */
    /* ------------------------------------------------------------------ */

    [Authorize(Policy = Policies.CanManageHospitals)]
    public IActionResult Create()
    {
        SetFormViewData("Add Facility", "Register a hospital, rural health unit, or private clinic");

        return View("Form", new HospitalFormViewModel
        {
            Status = FacilityStatus.Online,
            IsActive = true
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.CanManageHospitals)]
    public async Task<IActionResult> Create(HospitalFormViewModel form, CancellationToken cancellationToken)
    {
        var code = await ResolveCodeAsync(form, null, cancellationToken);

        if (!ModelState.IsValid)
        {
            SetFormViewData("Add Facility", "Register a hospital, rural health unit, or private clinic");
            return View("Form", form);
        }

        var hospital = new Hospital { Code = code };
        form.ApplyTo(hospital);

        hospital.CreatedAt = DateTime.UtcNow;
        hospital.CreatedBy = _currentUser.UserId;

        await _hospitals.AddAsync(hospital, cancellationToken);
        await _hospitals.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Facility {Code} ({Name}) registered at {Lat},{Lon}.",
            hospital.Code, hospital.Name, hospital.Latitude, hospital.Longitude);

        await _audit.LogAsync(AuditActions.HospitalCreated, nameof(Hospital),
            hospital.Id.ToString(), $"Facility '{hospital.Name}' registered.",
            newValues: new { hospital.Code, hospital.Name, hospital.Municipality },
            cancellationToken: cancellationToken);

        TempData["StatusMessage"] = $"{hospital.Name} was added and now appears on the provincial map.";
        return RedirectToAction(nameof(Index));
    }

    /* ------------------------------------------------------------------ */
    /* Edit                                                                */
    /* ------------------------------------------------------------------ */

    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var hospital = await _hospitals.GetByIdAsync(id, cancellationToken);
        if (hospital is null)
        {
            return NotFound();
        }

        // Prevents an insecure direct object reference via the route id.
        if (!CanAccess(id))
        {
            return Forbid();
        }

        SetFormViewData("Edit Facility", hospital.Name);
        return View("Form", HospitalFormViewModel.FromEntity(hospital));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, HospitalFormViewModel form, CancellationToken cancellationToken)
    {
        var hospital = await _hospitals.GetForUpdateAsync(id, cancellationToken);
        if (hospital is null)
        {
            return NotFound();
        }

        if (!CanAccess(id))
        {
            return Forbid();
        }

        form.Id = id;
        var code = await ResolveCodeAsync(form, id, cancellationToken);

        if (!ModelState.IsValid)
        {
            SetFormViewData("Edit Facility", hospital.Name);
            return View("Form", form);
        }

        var before = new { hospital.Name, hospital.Municipality, hospital.Latitude, hospital.Longitude,
                           hospital.AvailableBeds, hospital.TotalBeds, hospital.IsActive };

        hospital.Code = code;
        form.ApplyTo(hospital);

        // CreatedAt/CreatedBy are never overwritten on update.
        hospital.UpdatedAt = DateTime.UtcNow;
        hospital.UpdatedBy = _currentUser.UserId;

        await _hospitals.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(AuditActions.HospitalUpdated, nameof(Hospital),
            hospital.Id.ToString(), $"Facility '{hospital.Name}' updated.",
            oldValues: before,
            newValues: new { hospital.Name, hospital.Municipality, hospital.Latitude, hospital.Longitude,
                             hospital.AvailableBeds, hospital.TotalBeds, hospital.IsActive },
            cancellationToken: cancellationToken);

        TempData["StatusMessage"] = $"{hospital.Name} was updated. The map reflects the change.";
        return RedirectToAction(nameof(Index));
    }

    /* ------------------------------------------------------------------ */
    /* Delete                                                              */
    /* ------------------------------------------------------------------ */

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = Policies.CanManageHospitals)]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var hospital = await _hospitals.GetForUpdateAsync(id, cancellationToken);
        if (hospital is null)
        {
            return NotFound();
        }

        var (referrals, doctors) = await _hospitals.GetUsageAsync(id, cancellationToken);

        // Referral history must stay intact, so a facility in use is retired
        // rather than deleted. It leaves the map but keeps its records.
        if (referrals > 0 || doctors > 0)
        {
            hospital.IsActive = false;
            hospital.Status = FacilityStatus.Offline;
            hospital.LastUpdatedUtc = DateTime.UtcNow;
            hospital.UpdatedAt = DateTime.UtcNow;
            hospital.UpdatedBy = _currentUser.UserId;

            await _hospitals.SaveChangesAsync(cancellationToken);

            await _audit.LogAsync(AuditActions.HospitalDeactivated, nameof(Hospital),
                hospital.Id.ToString(),
                $"Facility '{hospital.Name}' deactivated ({referrals} referral(s), {doctors} doctor(s) on record).",
                cancellationToken: cancellationToken);

            TempData["StatusMessage"] =
                $"{hospital.Name} has {referrals} referral(s) and {doctors} doctor(s) on record, " +
                "so it was deactivated instead of deleted. It no longer appears on the map.";

            return RedirectToAction(nameof(Index));
        }

        // Unused facility: soft delete keeps the row auditable rather than
        // removing it outright.
        hospital.IsDeleted = true;
        hospital.DeletedAt = DateTime.UtcNow;
        hospital.IsActive = false;
        hospital.UpdatedAt = DateTime.UtcNow;
        hospital.UpdatedBy = _currentUser.UserId;

        await _hospitals.SaveChangesAsync(cancellationToken);

        await _audit.LogAsync(AuditActions.HospitalDeleted, nameof(Hospital),
            hospital.Id.ToString(), $"Facility '{hospital.Name}' soft-deleted.",
            cancellationToken: cancellationToken);

        TempData["StatusMessage"] = $"{hospital.Name} was removed from the network.";
        return RedirectToAction(nameof(Index));
    }

    /* ------------------------------------------------------------------ */
    /* Helpers                                                             */
    /* ------------------------------------------------------------------ */

    private void SetFormViewData(string title, string subtitle)
    {
        ViewData["Title"] = title;
        ViewData["Subtitle"] = subtitle;
        ViewData["ActiveNav"] = "hospitals";
    }

    /// <summary>
    /// Uses the supplied code, or derives a unique slug from the facility name.
    /// Adds a model error when an explicit code is already taken.
    /// </summary>
    private async Task<string> ResolveCodeAsync(
        HospitalFormViewModel form,
        int? excludeId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(form.Code))
        {
            var supplied = form.Code.Trim().ToLowerInvariant();

            if (await _hospitals.CodeExistsAsync(supplied, excludeId, cancellationToken))
            {
                ModelState.AddModelError(nameof(form.Code), "That facility code is already in use.");
            }

            return supplied;
        }

        var baseSlug = Slugify(form.Name);
        if (string.IsNullOrEmpty(baseSlug))
        {
            baseSlug = "facility";
        }

        var candidate = baseSlug;
        var suffix = 2;

        while (await _hospitals.CodeExistsAsync(candidate, excludeId, cancellationToken))
        {
            candidate = $"{baseSlug}-{suffix++}";
        }

        return candidate;
    }

    private static string Slugify(string value)
    {
        var lower = (value ?? string.Empty).ToLowerInvariant();
        var cleaned = Regex.Replace(lower, "[^a-z0-9]+", "-").Trim('-');

        return cleaned.Length > 60 ? cleaned[..60].Trim('-') : cleaned;
    }
}
