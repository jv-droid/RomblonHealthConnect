using Microsoft.AspNetCore.SignalR;
using RomblonHealthConnect.Hubs;
using RomblonHealthConnect.Interfaces;
using RomblonHealthConnect.Models;
using RomblonHealthConnect.Models.Enums;
using RomblonHealthConnect.ViewModels.Referrals;

namespace RomblonHealthConnect.Services;

/// <summary>
/// Owns the referral workflow. Every transition validates the current state,
/// appends a history entry, notifies the affected facility, and broadcasts over SignalR.
/// </summary>
public class ReferralService : IReferralService
{
    /// <summary>Hours a submitted referral may wait for a response, by priority.</summary>
    private static readonly Dictionary<ReferralPriority, int> ResponseWindowHours = new()
    {
        [ReferralPriority.Emergency] = 2,
        [ReferralPriority.Urgent] = 12,
        [ReferralPriority.Routine] = 72
    };

    private readonly IReferralRepository _referrals;
    private readonly IHospitalRepository _hospitals;
    private readonly IDoctorRepository _doctors;
    private readonly ISpecializationRepository _specializations;
    private readonly IFileStorageService _fileStorage;
    private readonly INotificationService _notifications;
    private readonly IHubContext<ReferralHub> _hub;
    private readonly ILogger<ReferralService> _logger;

    public ReferralService(
        IReferralRepository referrals,
        IHospitalRepository hospitals,
        IDoctorRepository doctors,
        ISpecializationRepository specializations,
        IFileStorageService fileStorage,
        INotificationService notifications,
        IHubContext<ReferralHub> hub,
        ILogger<ReferralService> logger)
    {
        _referrals = referrals;
        _hospitals = hospitals;
        _doctors = doctors;
        _specializations = specializations;
        _fileStorage = fileStorage;
        _notifications = notifications;
        _hub = hub;
        _logger = logger;
    }

    /* ------------------------------------------------------------------ */
    /* Queries                                                             */
    /* ------------------------------------------------------------------ */

    public async Task<ReferralDashboardViewModel> GetDashboardAsync(
        ReferralFilter filter,
        CancellationToken cancellationToken = default)
    {
        var page = await _referrals.SearchAsync(filter, cancellationToken);

        // Counts ignore paging but honour the active filters.
        var counts = await _referrals.GetStatusCountsAsync(filter, cancellationToken);
        var today = await _referrals.CountCreatedOnAsync(DateTime.UtcNow, cancellationToken);

        var hospitals = await _hospitals.GetAllAsync(cancellationToken);
        var municipalities = await _hospitals.GetMunicipalitiesAsync(cancellationToken);

        var incoming = await _referrals.GetStatusCountsAsync(
            CloneFilterWithScope(filter, ReferralScope.Incoming), cancellationToken);

        var outgoing = await _referrals.GetStatusCountsAsync(
            CloneFilterWithScope(filter, ReferralScope.Outgoing), cancellationToken);

        return new ReferralDashboardViewModel
        {
            TodayCount = today,
            PendingCount = Count(counts, ReferralStatus.Submitted),
            AcceptedCount = Count(counts, ReferralStatus.Accepted),
            RejectedCount = Count(counts, ReferralStatus.Rejected),
            CompletedCount = Count(counts, ReferralStatus.Completed),
            IncomingCount = incoming.Values.Sum(),
            OutgoingCount = outgoing.Values.Sum(),
            Referrals = page.Map(ReferralListItemViewModel.FromEntity),
            Filter = filter,
            Hospitals = hospitals.Select(h => new FilterOption(h.Id, h.Name)).ToList(),
            Municipalities = municipalities
        };
    }

    public async Task<PagedResult<ReferralListItemViewModel>> GetListAsync(
        ReferralFilter filter,
        CancellationToken cancellationToken = default)
    {
        var page = await _referrals.SearchAsync(filter, cancellationToken);
        return page.Map(ReferralListItemViewModel.FromEntity);
    }

    public async Task<ReferralDetailsViewModel?> GetDetailsAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var referral = await _referrals.GetByIdAsync(id, cancellationToken);
        if (referral is null)
        {
            return null;
        }

        var assignable = await _doctors.GetAvailableAsync(
            referral.DestinationHospitalId,
            referral.RequestedSpecializationId,
            cancellationToken);

        var timeline = referral.History
            .OrderBy(h => h.PerformedAtUtc)
            .Select(TimelineEntryViewModel.FromEntity)
            .ToList();

        return new ReferralDetailsViewModel
        {
            Referral = referral,
            Timeline = timeline,
            Attachments = referral.Attachments.OrderBy(a => a.UploadedAtUtc).ToList(),
            AssignableDoctors = assignable,
            CanRespond = referral.Status == ReferralStatus.Submitted,
            CanSubmit = referral.Status == ReferralStatus.Draft,
            CanCancel = referral.Status is ReferralStatus.Draft or ReferralStatus.Submitted,
            CanComplete = referral.Status == ReferralStatus.Accepted
        };
    }

    public async Task<HospitalCapabilityViewModel?> GetHospitalCapabilityAsync(
        int hospitalId,
        int? specializationId = null,
        CancellationToken cancellationToken = default)
    {
        var hospital = await _hospitals.GetByIdAsync(hospitalId, cancellationToken);
        if (hospital is null)
        {
            return null;
        }

        var doctors = await _doctors.GetAvailableAsync(hospitalId, specializationId, cancellationToken);
        var specialties = await _specializations.GetByHospitalAsync(hospitalId, cancellationToken);

        return new HospitalCapabilityViewModel
        {
            HospitalId = hospital.Id,
            Code = hospital.Code,
            Name = hospital.Name,
            TypeLabel = hospital.FacilityTypeLabel,
            Municipality = hospital.Municipality,
            Address = hospital.Address,
            StatusLabel = hospital.Status.ToString(),
            StatusBadgeClass = ReferralDisplay.FacilityStatusBadgeClass(hospital.Status),
            HasEmergency = hospital.HasEmergency,
            AvailableBeds = hospital.AvailableBeds,
            TotalBeds = hospital.TotalBeds,
            Latitude = hospital.Latitude,
            Longitude = hospital.Longitude,
            Services = SplitServices(hospital.Services),
            Specializations = specialties.Select(s => s.Name).ToList(),
            Doctors = doctors.Select(d => new AvailableDoctorViewModel(
                d.Id,
                d.FullName,
                d.PrimarySpecialization.Name,
                ReferralDisplay.AvailabilityLabel(d.Availability),
                ReferralDisplay.AvailabilityBadgeClass(d.Availability),
                d.IsAcceptingReferrals)).ToList()
        };
    }

    /* ------------------------------------------------------------------ */
    /* Workflow                                                            */
    /* ------------------------------------------------------------------ */

    public async Task<ReferralOperationResult> CreateAsync(
        CreateReferralViewModel model,
        bool submit,
        CancellationToken cancellationToken = default)
    {
        if (model.OriginHospitalId == model.DestinationHospitalId)
        {
            return ReferralOperationResult.Fail("A referral must be sent to a different facility.");
        }

        var now = DateTime.UtcNow;
        var sequence = await _referrals.GetNextSequenceForYearAsync(now.Year, cancellationToken);

        var referral = new Referral
        {
            ReferralNumber = $"RF-{now.Year}-{sequence:D4}",
            PatientId = model.PatientId!.Value,
            OriginHospitalId = model.OriginHospitalId!.Value,
            DestinationHospitalId = model.DestinationHospitalId!.Value,
            RequestedSpecializationId = model.RequestedSpecializationId!.Value,
            ReferringDoctorId = model.ReferringDoctorId,
            AssignedDoctorId = model.AssignedDoctorId,
            Priority = model.Priority,
            ReasonForReferral = model.ReasonForReferral,
            Diagnosis = model.Diagnosis,
            ClinicalNotes = model.ClinicalNotes,
            Status = ReferralStatus.Draft,
            CreatedUtc = now
        };

        referral.History.Add(NewHistory(ReferralAction.Created, null, ReferralStatus.Draft, "Referral drafted.", now));

        await PersistAttachmentsAsync(referral, model, now, cancellationToken);

        if (submit)
        {
            referral.Status = ReferralStatus.Submitted;
            referral.SubmittedUtc = now;
            referral.ExpiresUtc = now.AddHours(ResponseWindowHours[referral.Priority]);

            referral.History.Add(NewHistory(
                ReferralAction.Submitted,
                ReferralStatus.Draft,
                ReferralStatus.Submitted,
                "Referral sent to the receiving facility.",
                now.AddSeconds(1)));
        }

        await _referrals.AddAsync(referral, cancellationToken);
        await _referrals.SaveChangesAsync(cancellationToken);

        if (submit)
        {
            await NotifyDestinationOfNewReferralAsync(referral, cancellationToken);
        }

        await BroadcastAsync(referral, ReferralHub.Events.ReferralCreated, cancellationToken);

        return ReferralOperationResult.Ok(referral.Id, referral.ReferralNumber, referral.Status);
    }

    public async Task<ReferralOperationResult> SubmitAsync(
        int referralId,
        CancellationToken cancellationToken = default)
    {
        var referral = await _referrals.GetByIdAsync(referralId, cancellationToken);
        if (referral is null)
        {
            return ReferralOperationResult.Fail("Referral not found.");
        }

        if (referral.Status != ReferralStatus.Draft)
        {
            return ReferralOperationResult.Fail("Only a draft referral can be submitted.");
        }

        var now = DateTime.UtcNow;

        referral.Status = ReferralStatus.Submitted;
        referral.SubmittedUtc = now;
        referral.ExpiresUtc = now.AddHours(ResponseWindowHours[referral.Priority]);
        referral.History.Add(NewHistory(
            ReferralAction.Submitted, ReferralStatus.Draft, ReferralStatus.Submitted,
            "Referral sent to the receiving facility.", now));

        await _referrals.SaveChangesAsync(cancellationToken);

        await NotifyDestinationOfNewReferralAsync(referral, cancellationToken);
        await BroadcastAsync(referral, ReferralHub.Events.ReferralStatusChanged, cancellationToken);

        return ReferralOperationResult.Ok(referral.Id, referral.ReferralNumber, referral.Status);
    }

    public async Task<ReferralOperationResult> AcceptAsync(
        int referralId,
        int? assignedDoctorId,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        var referral = await _referrals.GetByIdAsync(referralId, cancellationToken);
        if (referral is null)
        {
            return ReferralOperationResult.Fail("Referral not found.");
        }

        if (referral.Status != ReferralStatus.Submitted)
        {
            return ReferralOperationResult.Fail("Only a submitted referral can be accepted.");
        }

        var now = DateTime.UtcNow;

        referral.Status = ReferralStatus.Accepted;
        referral.RespondedUtc = now;
        referral.ResponseNotes = notes;
        referral.History.Add(NewHistory(
            ReferralAction.Accepted, ReferralStatus.Submitted, ReferralStatus.Accepted,
            notes ?? "Referral accepted by the receiving facility.", now));

        if (assignedDoctorId.HasValue)
        {
            var doctor = await _doctors.GetByIdAsync(assignedDoctorId.Value, cancellationToken);

            if (doctor is not null && doctor.HospitalId == referral.DestinationHospitalId)
            {
                referral.AssignedDoctorId = doctor.Id;
                referral.History.Add(NewHistory(
                    ReferralAction.DoctorAssigned, ReferralStatus.Accepted, ReferralStatus.Accepted,
                    $"{doctor.FullName} assigned to the patient.", now.AddSeconds(1)));
            }
            else
            {
                _logger.LogWarning(
                    "Doctor {DoctorId} is not assignable at hospital {HospitalId}; skipping assignment.",
                    assignedDoctorId, referral.DestinationHospitalId);
            }
        }

        await _referrals.SaveChangesAsync(cancellationToken);

        await _notifications.CreateAsync(
            referral.OriginHospitalId,
            NotificationType.ReferralAccepted,
            "Referral accepted",
            $"{referral.DestinationHospital.Name} accepted {referral.ReferralNumber} for {referral.Patient.FullName}.",
            referral.Id,
            cancellationToken);

        await BroadcastAsync(referral, ReferralHub.Events.ReferralStatusChanged, cancellationToken);

        return ReferralOperationResult.Ok(referral.Id, referral.ReferralNumber, referral.Status);
    }

    public async Task<ReferralOperationResult> RejectAsync(
        int referralId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var referral = await _referrals.GetByIdAsync(referralId, cancellationToken);
        if (referral is null)
        {
            return ReferralOperationResult.Fail("Referral not found.");
        }

        if (referral.Status != ReferralStatus.Submitted)
        {
            return ReferralOperationResult.Fail("Only a submitted referral can be rejected.");
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            return ReferralOperationResult.Fail("A reason is required when rejecting a referral.");
        }

        var now = DateTime.UtcNow;

        referral.Status = ReferralStatus.Rejected;
        referral.RespondedUtc = now;
        referral.ResponseNotes = reason;
        referral.History.Add(NewHistory(
            ReferralAction.Rejected, ReferralStatus.Submitted, ReferralStatus.Rejected, reason, now));

        await _referrals.SaveChangesAsync(cancellationToken);

        await _notifications.CreateAsync(
            referral.OriginHospitalId,
            NotificationType.ReferralRejected,
            "Referral rejected",
            $"{referral.DestinationHospital.Name} rejected {referral.ReferralNumber}: {reason}",
            referral.Id,
            cancellationToken);

        await BroadcastAsync(referral, ReferralHub.Events.ReferralStatusChanged, cancellationToken);

        return ReferralOperationResult.Ok(referral.Id, referral.ReferralNumber, referral.Status);
    }

    public async Task<ReferralOperationResult> RequestInformationAsync(
        int referralId,
        string question,
        CancellationToken cancellationToken = default)
    {
        var referral = await _referrals.GetByIdAsync(referralId, cancellationToken);
        if (referral is null)
        {
            return ReferralOperationResult.Fail("Referral not found.");
        }

        if (referral.Status != ReferralStatus.Submitted)
        {
            return ReferralOperationResult.Fail("Information can only be requested on a submitted referral.");
        }

        if (string.IsNullOrWhiteSpace(question))
        {
            return ReferralOperationResult.Fail("Enter the information you need.");
        }

        var now = DateTime.UtcNow;

        // The referral deliberately stays Submitted — the clock keeps running.
        referral.History.Add(NewHistory(
            ReferralAction.InformationRequested, referral.Status, referral.Status, question, now));

        await _referrals.SaveChangesAsync(cancellationToken);

        await _notifications.CreateAsync(
            referral.OriginHospitalId,
            NotificationType.InformationRequested,
            "More information requested",
            $"{referral.DestinationHospital.Name} asked about {referral.ReferralNumber}: {question}",
            referral.Id,
            cancellationToken);

        await BroadcastAsync(referral, ReferralHub.Events.ReferralStatusChanged, cancellationToken);

        return ReferralOperationResult.Ok(referral.Id, referral.ReferralNumber, referral.Status);
    }

    public async Task<ReferralOperationResult> CompleteAsync(
        int referralId,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        var referral = await _referrals.GetByIdAsync(referralId, cancellationToken);
        if (referral is null)
        {
            return ReferralOperationResult.Fail("Referral not found.");
        }

        if (referral.Status != ReferralStatus.Accepted)
        {
            return ReferralOperationResult.Fail("Only an accepted referral can be completed.");
        }

        var now = DateTime.UtcNow;

        referral.Status = ReferralStatus.Completed;
        referral.CompletedUtc = now;
        referral.History.Add(NewHistory(
            ReferralAction.Completed, ReferralStatus.Accepted, ReferralStatus.Completed,
            notes ?? "Patient seen and referral closed.", now));

        await _referrals.SaveChangesAsync(cancellationToken);

        await _notifications.CreateAsync(
            referral.OriginHospitalId,
            NotificationType.ReferralCompleted,
            "Referral completed",
            $"{referral.ReferralNumber} for {referral.Patient.FullName} has been completed.",
            referral.Id,
            cancellationToken);

        await BroadcastAsync(referral, ReferralHub.Events.ReferralStatusChanged, cancellationToken);

        return ReferralOperationResult.Ok(referral.Id, referral.ReferralNumber, referral.Status);
    }

    public async Task<ReferralOperationResult> CancelAsync(
        int referralId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var referral = await _referrals.GetByIdAsync(referralId, cancellationToken);
        if (referral is null)
        {
            return ReferralOperationResult.Fail("Referral not found.");
        }

        if (referral.IsTerminal)
        {
            return ReferralOperationResult.Fail("This referral has already been closed.");
        }

        var now = DateTime.UtcNow;
        var previous = referral.Status;

        referral.Status = ReferralStatus.Cancelled;
        referral.History.Add(NewHistory(
            ReferralAction.Cancelled, previous, ReferralStatus.Cancelled,
            string.IsNullOrWhiteSpace(reason) ? "Cancelled by the referring facility." : reason, now));

        await _referrals.SaveChangesAsync(cancellationToken);

        // Only worth telling the destination if they had already been notified.
        if (previous != ReferralStatus.Draft)
        {
            await _notifications.CreateAsync(
                referral.DestinationHospitalId,
                NotificationType.ReferralCancelled,
                "Referral cancelled",
                $"{referral.OriginHospital.Name} cancelled {referral.ReferralNumber}.",
                referral.Id,
                cancellationToken);
        }

        await BroadcastAsync(referral, ReferralHub.Events.ReferralStatusChanged, cancellationToken);

        return ReferralOperationResult.Ok(referral.Id, referral.ReferralNumber, referral.Status);
    }

    /* ------------------------------------------------------------------ */
    /* Helpers                                                             */
    /* ------------------------------------------------------------------ */

    private static int Count(IReadOnlyDictionary<ReferralStatus, int> counts, ReferralStatus status) =>
        counts.TryGetValue(status, out var value) ? value : 0;

    private static ReferralFilter CloneFilterWithScope(ReferralFilter source, ReferralScope scope) => new()
    {
        CurrentHospitalId = source.CurrentHospitalId,
        Scope = scope,
        Page = 1,
        PageSize = source.PageSize
    };

    private static ReferralHistory NewHistory(
        ReferralAction action,
        ReferralStatus? from,
        ReferralStatus? to,
        string? notes,
        DateTime occurredUtc) => new()
        {
            Action = action,
            FromStatus = from,
            ToStatus = to,
            Notes = notes,
            PerformedBy = "Provincial Administrator",
            PerformedAtUtc = occurredUtc
        };

    private static IReadOnlyList<string> SplitServices(string services) =>
        string.IsNullOrWhiteSpace(services)
            ? Array.Empty<string>()
            : services.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    /// <summary>Stores each uploaded file and records an attachment plus a timeline entry.</summary>
    private async Task PersistAttachmentsAsync(
        Referral referral,
        CreateReferralViewModel model,
        DateTime now,
        CancellationToken cancellationToken)
    {
        for (var i = 0; i < model.Attachments.Count; i++)
        {
            var file = model.Attachments[i];

            var category = i < model.AttachmentCategories.Count
                ? model.AttachmentCategories[i]
                : AttachmentCategory.Document;

            var result = await _fileStorage.SaveAsync(file, category, cancellationToken);

            if (!result.Success)
            {
                // A rejected file must not sink the referral; it is logged and skipped.
                _logger.LogWarning("Attachment rejected for {ReferralNumber}: {Error}",
                    referral.ReferralNumber, result.Error);
                continue;
            }

            referral.Attachments.Add(new ReferralAttachment
            {
                FileName = Path.GetFileName(file.FileName),
                StoredFileName = result.StoredFileName!,
                ContentType = result.ContentType!,
                FileSizeBytes = result.FileSizeBytes,
                Category = category,
                UploadedBy = "Provincial Administrator",
                UploadedAtUtc = now
            });
        }

        if (referral.Attachments.Count > 0)
        {
            referral.History.Add(NewHistory(
                ReferralAction.AttachmentAdded, null, null,
                $"{referral.Attachments.Count} file(s) attached.", now));
        }
    }

    private async Task NotifyDestinationOfNewReferralAsync(Referral referral, CancellationToken cancellationToken)
    {
        var priority = referral.Priority == ReferralPriority.Routine
            ? string.Empty
            : $" [{referral.Priority.ToString().ToUpperInvariant()}]";

        await _notifications.CreateAsync(
            referral.DestinationHospitalId,
            NotificationType.ReferralReceived,
            $"New referral received{priority}",
            $"{referral.OriginHospital.Name} referred {referral.Patient.FullName} " +
            $"for {referral.RequestedSpecialization.Name} ({referral.ReferralNumber}).",
            referral.Id,
            cancellationToken);
    }

    /// <summary>
    /// Pushes the change to both facilities. Transport problems are logged, never thrown,
    /// because the database transition has already committed.
    /// </summary>
    private async Task BroadcastAsync(Referral referral, string eventName, CancellationToken cancellationToken)
    {
        var payload = new
        {
            id = referral.Id,
            referralNumber = referral.ReferralNumber,
            status = referral.Status.ToString(),
            statusLabel = ReferralDisplay.StatusLabel(referral.Status),
            statusBadgeClass = ReferralDisplay.StatusBadgeClass(referral.Status),
            priority = referral.Priority.ToString(),
            originHospitalId = referral.OriginHospitalId,
            destinationHospitalId = referral.DestinationHospitalId,
            patientName = referral.Patient?.FullName,
            updatedUtc = DateTime.UtcNow
        };

        try
        {
            await _hub.Clients
                .Groups(
                    ReferralHub.GroupForHospital(referral.OriginHospitalId),
                    ReferralHub.GroupForHospital(referral.DestinationHospitalId))
                .SendAsync(eventName, payload, cancellationToken);

            await _hub.Clients.All.SendAsync(ReferralHub.Events.CountsChanged, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to broadcast {Event} for referral {ReferralNumber}.",
                eventName, referral.ReferralNumber);
        }
    }
}
