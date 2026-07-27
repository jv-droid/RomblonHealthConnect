using RomblonHealthConnect.Models.Enums;

namespace RomblonHealthConnect.Models;

/// <summary>
/// Append-only audit entry. Drives the referral timeline and is never edited.
/// </summary>
public class ReferralHistory
{
    public int Id { get; set; }

    public int ReferralId { get; set; }

    public Referral Referral { get; set; } = null!;

    public ReferralAction Action { get; set; }

    public ReferralStatus? FromStatus { get; set; }

    public ReferralStatus? ToStatus { get; set; }

    public string? Notes { get; set; }

    /// <summary>Actor display name. Replaced by the authenticated user in a later phase.</summary>
    public string PerformedBy { get; set; } = "System";

    public DateTime PerformedAtUtc { get; set; }

    /// <summary>Timeline label shown beside the timestamp.</summary>
    public string ActionLabel => Action switch
    {
        ReferralAction.Created => "Referral created",
        ReferralAction.Submitted => "Referral submitted",
        ReferralAction.Accepted => "Hospital accepted",
        ReferralAction.Rejected => "Hospital rejected",
        ReferralAction.InformationRequested => "More information requested",
        ReferralAction.DoctorAssigned => "Doctor assigned",
        ReferralAction.PatientScheduled => "Patient scheduled",
        ReferralAction.Completed => "Referral completed",
        ReferralAction.Cancelled => "Referral cancelled",
        ReferralAction.Expired => "Referral expired",
        ReferralAction.NoteAdded => "Note added",
        ReferralAction.AttachmentAdded => "Attachment added",
        _ => Action.ToString()
    };
}
