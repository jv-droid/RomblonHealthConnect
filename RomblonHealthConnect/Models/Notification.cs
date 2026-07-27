using RomblonHealthConnect.Models.Enums;

namespace RomblonHealthConnect.Models;

/// <summary>
/// An entry in the notification centre, targeted at a single facility.
/// </summary>
public class Notification
{
    public int Id { get; set; }

    public NotificationType Type { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    /// <summary>Facility this notification is addressed to.</summary>
    public int HospitalId { get; set; }

    public Hospital Hospital { get; set; } = null!;

    public int? ReferralId { get; set; }

    public Referral? Referral { get; set; }

    public bool IsRead { get; set; }

    public DateTime CreatedUtc { get; set; }

    /// <summary>Font Awesome icon rendered in the notification list.</summary>
    public string Icon => Type switch
    {
        NotificationType.ReferralReceived => "fa-inbox",
        NotificationType.ReferralAccepted => "fa-circle-check",
        NotificationType.ReferralRejected => "fa-circle-xmark",
        NotificationType.DoctorAssigned => "fa-user-doctor",
        NotificationType.ReferralCompleted => "fa-flag-checkered",
        NotificationType.InformationRequested => "fa-circle-question",
        NotificationType.ReferralCancelled => "fa-ban",
        NotificationType.ReferralExpired => "fa-clock",
        _ => "fa-bell"
    };
}
