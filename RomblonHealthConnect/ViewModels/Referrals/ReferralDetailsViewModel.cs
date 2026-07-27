using RomblonHealthConnect.Models;
using RomblonHealthConnect.Models.Enums;

namespace RomblonHealthConnect.ViewModels.Referrals;

/// <summary>
/// Full referral record: clinical detail, attachments, and the audit timeline.
/// </summary>
public class ReferralDetailsViewModel
{
    public Referral Referral { get; init; } = null!;

    public IReadOnlyList<TimelineEntryViewModel> Timeline { get; init; } = Array.Empty<TimelineEntryViewModel>();

    public IReadOnlyList<ReferralAttachment> Attachments { get; init; } = Array.Empty<ReferralAttachment>();

    /// <summary>Doctors the receiving facility can assign when accepting.</summary>
    public IReadOnlyList<Doctor> AssignableDoctors { get; init; } = Array.Empty<Doctor>();

    /// <summary>True when the acting facility is the destination and the referral awaits a response.</summary>
    public bool CanRespond { get; init; }

    /// <summary>True when the acting facility owns the referral and it has not been sent yet.</summary>
    public bool CanSubmit { get; init; }

    public bool CanCancel { get; init; }

    public bool CanComplete { get; init; }
}

/// <summary>
/// One point on the referral timeline.
/// </summary>
public class TimelineEntryViewModel
{
    public string Label { get; init; } = string.Empty;

    public string? Notes { get; init; }

    public string PerformedBy { get; init; } = string.Empty;

    public DateTime OccurredUtc { get; init; }

    public ReferralAction Action { get; init; }

    /// <summary>Font Awesome icon for the timeline marker.</summary>
    public string Icon => Action switch
    {
        ReferralAction.Created => "fa-file-circle-plus",
        ReferralAction.Submitted => "fa-paper-plane",
        ReferralAction.Accepted => "fa-circle-check",
        ReferralAction.Rejected => "fa-circle-xmark",
        ReferralAction.InformationRequested => "fa-circle-question",
        ReferralAction.DoctorAssigned => "fa-user-doctor",
        ReferralAction.PatientScheduled => "fa-calendar-check",
        ReferralAction.Completed => "fa-flag-checkered",
        ReferralAction.Cancelled => "fa-ban",
        ReferralAction.Expired => "fa-clock",
        ReferralAction.AttachmentAdded => "fa-paperclip",
        _ => "fa-circle-dot"
    };

    /// <summary>Marker colour modifier, matching the status palette.</summary>
    public string ToneClass => Action switch
    {
        ReferralAction.Accepted or ReferralAction.Completed => "timeline-marker-success",
        ReferralAction.Rejected or ReferralAction.Cancelled or ReferralAction.Expired => "timeline-marker-danger",
        ReferralAction.InformationRequested => "timeline-marker-warning",
        _ => "timeline-marker-default"
    };

    public static TimelineEntryViewModel FromEntity(ReferralHistory history) => new()
    {
        Label = history.ActionLabel,
        Notes = history.Notes,
        PerformedBy = history.PerformedBy,
        OccurredUtc = history.PerformedAtUtc,
        Action = history.Action
    };
}

/// <summary>
/// Capability snapshot for the destination hospital card in the wizard.
/// </summary>
public class HospitalCapabilityViewModel
{
    public int HospitalId { get; init; }

    public string Code { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string TypeLabel { get; init; } = string.Empty;

    public string Municipality { get; init; } = string.Empty;

    public string Address { get; init; } = string.Empty;

    public string StatusLabel { get; init; } = string.Empty;

    public string StatusBadgeClass { get; init; } = string.Empty;

    public bool HasEmergency { get; init; }

    public int AvailableBeds { get; init; }

    public int TotalBeds { get; init; }

    public double Latitude { get; init; }

    public double Longitude { get; init; }

    public IReadOnlyList<string> Services { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Specializations { get; init; } = Array.Empty<string>();

    public IReadOnlyList<AvailableDoctorViewModel> Doctors { get; init; } = Array.Empty<AvailableDoctorViewModel>();

    public int AvailableDoctorCount => Doctors.Count(d => d.IsAccepting);
}

public record AvailableDoctorViewModel(
    int Id,
    string FullName,
    string Specialization,
    string AvailabilityLabel,
    string AvailabilityBadgeClass,
    bool IsAccepting);
