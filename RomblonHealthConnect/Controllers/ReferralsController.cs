using Microsoft.AspNetCore.Mvc;
using RomblonHealthConnect.Interfaces;
using RomblonHealthConnect.Models.Enums;
using RomblonHealthConnect.ViewModels.Referrals;

namespace RomblonHealthConnect.Controllers;

/// <summary>
/// Smart Referral Engine. Serves the referral dashboard, the queues, the
/// create-referral wizard, and the JSON endpoints those views call.
/// </summary>
public class ReferralsController : Controller
{
    private readonly IReferralService _referralService;
    private readonly IHospitalRepository _hospitals;
    private readonly IDoctorRepository _doctors;
    private readonly ISpecializationRepository _specializations;
    private readonly IPatientRepository _patients;
    private readonly INotificationService _notifications;
    private readonly IFileStorageService _fileStorage;
    private readonly ICurrentFacilityProvider _currentFacility;
    private readonly ILogger<ReferralsController> _logger;

    public ReferralsController(
        IReferralService referralService,
        IHospitalRepository hospitals,
        IDoctorRepository doctors,
        ISpecializationRepository specializations,
        IPatientRepository patients,
        INotificationService notifications,
        IFileStorageService fileStorage,
        ICurrentFacilityProvider currentFacility,
        ILogger<ReferralsController> logger)
    {
        _referralService = referralService;
        _hospitals = hospitals;
        _doctors = doctors;
        _specializations = specializations;
        _patients = patients;
        _notifications = notifications;
        _fileStorage = fileStorage;
        _currentFacility = currentFacility;
        _logger = logger;
    }

    /* ------------------------------------------------------------------ */
    /* Dashboard and queues                                                */
    /* ------------------------------------------------------------------ */

    public async Task<IActionResult> Index(ReferralFilter filter, CancellationToken cancellationToken)
    {
        filter.CurrentHospitalId = await _currentFacility.GetHospitalIdAsync(cancellationToken);
        filter.Scope = ReferralScope.All;

        var model = await _referralService.GetDashboardAsync(filter, cancellationToken);

        ViewData["Title"] = "Referral Dashboard";
        ViewData["Subtitle"] = "Provincial referral activity and status monitoring";
        ViewData["ActiveNav"] = "referrals-dashboard";

        return View(model);
    }

    public Task<IActionResult> Incoming(ReferralFilter filter, CancellationToken cancellationToken) =>
        QueueAsync(filter, ReferralScope.Incoming, "Incoming Referrals",
            "Referrals sent to this facility awaiting a response", "referrals-incoming", cancellationToken);

    public Task<IActionResult> Outgoing(ReferralFilter filter, CancellationToken cancellationToken) =>
        QueueAsync(filter, ReferralScope.Outgoing, "Outgoing Referrals",
            "Referrals this facility has sent to others", "referrals-outgoing", cancellationToken);

    public Task<IActionResult> Pending(ReferralFilter filter, CancellationToken cancellationToken) =>
        QueueAsync(filter, ReferralScope.Pending, "Pending Referrals",
            "Submitted and accepted referrals still in progress", "referrals-pending", cancellationToken);

    public Task<IActionResult> Completed(ReferralFilter filter, CancellationToken cancellationToken) =>
        QueueAsync(filter, ReferralScope.Completed, "Completed Referrals",
            "Referrals closed after the patient was seen", "referrals-completed", cancellationToken);

    public Task<IActionResult> Archive(ReferralFilter filter, CancellationToken cancellationToken) =>
        QueueAsync(filter, ReferralScope.Archive, "Referral Archive",
            "Historical referrals retained for reporting", "referrals-archive", cancellationToken);

    private async Task<IActionResult> QueueAsync(
        ReferralFilter filter,
        ReferralScope scope,
        string title,
        string subtitle,
        string activeNav,
        CancellationToken cancellationToken)
    {
        filter.CurrentHospitalId = await _currentFacility.GetHospitalIdAsync(cancellationToken);
        filter.Scope = scope;

        var referrals = await _referralService.GetListAsync(filter, cancellationToken);
        var hospitals = await _hospitals.GetAllAsync(cancellationToken);
        var municipalities = await _hospitals.GetMunicipalitiesAsync(cancellationToken);

        ViewData["Title"] = title;
        ViewData["Subtitle"] = subtitle;
        ViewData["ActiveNav"] = activeNav;

        return View("List", new ReferralListViewModel
        {
            Title = title,
            Subtitle = subtitle,
            Scope = scope,
            Referrals = referrals,
            Filter = filter,
            Hospitals = hospitals.Select(h => new FilterOption(h.Id, h.Name)).ToList(),
            Municipalities = municipalities
        });
    }

    /* ------------------------------------------------------------------ */
    /* Details and transitions                                             */
    /* ------------------------------------------------------------------ */

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var model = await _referralService.GetDetailsAsync(id, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        ViewData["Title"] = model.Referral.ReferralNumber;
        ViewData["Subtitle"] = $"{model.Referral.OriginHospital.Name} to {model.Referral.DestinationHospital.Name}";
        ViewData["ActiveNav"] = "referrals-dashboard";

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Submit(int id, CancellationToken cancellationToken) =>
        await TransitionAsync(() => _referralService.SubmitAsync(id, cancellationToken), id);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept(int id, int? assignedDoctorId, string? notes,
        CancellationToken cancellationToken) =>
        await TransitionAsync(() => _referralService.AcceptAsync(id, assignedDoctorId, notes, cancellationToken), id);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string reason, CancellationToken cancellationToken) =>
        await TransitionAsync(() => _referralService.RejectAsync(id, reason, cancellationToken), id);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestInformation(int id, string question, CancellationToken cancellationToken) =>
        await TransitionAsync(() => _referralService.RequestInformationAsync(id, question, cancellationToken), id);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id, string? notes, CancellationToken cancellationToken) =>
        await TransitionAsync(() => _referralService.CompleteAsync(id, notes, cancellationToken), id);

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, string reason, CancellationToken cancellationToken) =>
        await TransitionAsync(() => _referralService.CancelAsync(id, reason, cancellationToken), id);

    /// <summary>Runs a transition and funnels both outcomes through the same feedback path.</summary>
    private async Task<IActionResult> TransitionAsync(Func<Task<ReferralOperationResult>> operation, int id)
    {
        var result = await operation();

        if (result.Success)
        {
            TempData["StatusMessage"] = $"{result.ReferralNumber} is now {result.Status}.";
        }
        else
        {
            TempData["ErrorMessage"] = result.Error;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    /* ------------------------------------------------------------------ */
    /* Create wizard                                                       */
    /* ------------------------------------------------------------------ */

    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        ViewData["Title"] = "Create Referral";
        ViewData["Subtitle"] = "Refer a patient to another facility in the provincial network";
        ViewData["ActiveNav"] = "referrals-create";

        return View(await BuildCreatePageAsync(new CreateReferralViewModel(), cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(60 * 1024 * 1024)]
    public async Task<IActionResult> Create(CreateReferralViewModel form, CancellationToken cancellationToken)
    {
        if (form.OriginHospitalId == form.DestinationHospitalId)
        {
            ModelState.AddModelError(
                nameof(form.DestinationHospitalId),
                "The destination must differ from the origin facility.");
        }

        if (!ModelState.IsValid)
        {
            ViewData["Title"] = "Create Referral";
            ViewData["Subtitle"] = "Refer a patient to another facility in the provincial network";
            ViewData["ActiveNav"] = "referrals-create";

            return View(await BuildCreatePageAsync(form, cancellationToken));
        }

        var result = await _referralService.CreateAsync(form, submit: !form.SaveAsDraft, cancellationToken);

        if (!result.Success)
        {
            TempData["ErrorMessage"] = result.Error;
            return RedirectToAction(nameof(Create));
        }

        TempData["StatusMessage"] = form.SaveAsDraft
            ? $"{result.ReferralNumber} saved as a draft."
            : $"{result.ReferralNumber} sent to the receiving facility.";

        return RedirectToAction(nameof(Details), new { id = result.ReferralId });
    }

    private async Task<CreateReferralPageViewModel> BuildCreatePageAsync(
        CreateReferralViewModel form,
        CancellationToken cancellationToken)
    {
        var currentHospitalId = await _currentFacility.GetHospitalIdAsync(cancellationToken);

        var patients = await _patients.SearchAsync(null, 100, cancellationToken);
        var hospitals = await _hospitals.GetAllAsync(cancellationToken);
        var specializations = await _specializations.GetAllAsync(cancellationToken);

        form.OriginHospitalId ??= currentHospitalId;

        return new CreateReferralPageViewModel
        {
            Form = form,
            DefaultOriginHospitalId = currentHospitalId,
            MaxFileSizeBytes = _fileStorage.MaxFileSizeBytes,
            AllowedExtensions = _fileStorage.AllowedExtensions,
            Patients = patients.Select(p => new PatientOption(
                p.Id, p.PatientNumber, p.FullName, p.Age, p.Sex.ToString(),
                p.Municipality, p.BloodType, p.ContactNumber)).ToList(),
            Hospitals = hospitals.Select(h => new HospitalOption(
                h.Id, h.Code, h.Name,
                ReferralDisplay.FacilityTypeKey(h.FacilityType), h.FacilityTypeLabel,
                h.Municipality, h.Address, h.Latitude, h.Longitude,
                h.Status.ToString(), ReferralDisplay.FacilityStatusBadgeClass(h.Status),
                h.HasEmergency, h.AvailableBeds, h.TotalBeds, h.Services)).ToList(),
            Specializations = specializations.Select(s => new FilterOption(s.Id, s.Name)).ToList()
        };
    }

    /* ------------------------------------------------------------------ */
    /* JSON endpoints used by the wizard and the real-time layer           */
    /* ------------------------------------------------------------------ */

    /// <summary>Capability snapshot for the destination hospital card.</summary>
    [HttpGet]
    public async Task<IActionResult> HospitalCapability(
        int hospitalId,
        int? specializationId,
        CancellationToken cancellationToken)
    {
        var capability = await _referralService.GetHospitalCapabilityAsync(
            hospitalId, specializationId, cancellationToken);

        return capability is null ? NotFound() : Json(capability);
    }

    /// <summary>Doctors at a facility, optionally narrowed to the requested specialty.</summary>
    [HttpGet]
    public async Task<IActionResult> AvailableDoctors(
        int hospitalId,
        int? specializationId,
        CancellationToken cancellationToken)
    {
        var doctors = await _doctors.GetAvailableAsync(hospitalId, specializationId, cancellationToken);

        return Json(doctors.Select(d => new
        {
            id = d.Id,
            name = d.FullName,
            specialization = d.PrimarySpecialization.Name,
            availability = ReferralDisplay.AvailabilityLabel(d.Availability),
            badgeClass = ReferralDisplay.AvailabilityBadgeClass(d.Availability),
            isAccepting = d.IsAcceptingReferrals
        }));
    }

    [HttpGet]
    public async Task<IActionResult> SearchPatients(string? term, CancellationToken cancellationToken)
    {
        var patients = await _patients.SearchAsync(term, 25, cancellationToken);

        return Json(patients.Select(p => new
        {
            id = p.Id,
            patientNumber = p.PatientNumber,
            fullName = p.FullName,
            age = p.Age,
            sex = p.Sex.ToString(),
            municipality = p.Municipality,
            bloodType = p.BloodType,
            contactNumber = p.ContactNumber
        }));
    }

    [HttpGet]
    public async Task<IActionResult> Notifications(CancellationToken cancellationToken)
    {
        var hospitalId = await _currentFacility.GetHospitalIdAsync(cancellationToken);
        var notifications = await _notifications.GetRecentAsync(hospitalId, 20, cancellationToken);
        var unread = await _notifications.GetUnreadCountAsync(hospitalId, cancellationToken);

        return Json(new
        {
            unreadCount = unread,
            items = notifications.Select(n => new
            {
                id = n.Id,
                type = n.Type.ToString(),
                title = n.Title,
                message = n.Message,
                referralId = n.ReferralId,
                icon = n.Icon,
                isRead = n.IsRead,
                createdUtc = n.CreatedUtc
            })
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkNotificationsRead(CancellationToken cancellationToken)
    {
        var hospitalId = await _currentFacility.GetHospitalIdAsync(cancellationToken);
        await _notifications.MarkAllAsReadAsync(hospitalId, cancellationToken);

        return Ok();
    }

    /// <summary>Identifies the acting facility so the client can join the right SignalR group.</summary>
    [HttpGet]
    public async Task<IActionResult> CurrentFacility(CancellationToken cancellationToken)
    {
        var hospitalId = await _currentFacility.GetHospitalIdAsync(cancellationToken);
        var hospital = await _hospitals.GetByIdAsync(hospitalId, cancellationToken);

        return Json(new { id = hospitalId, name = hospital?.Name });
    }
}
