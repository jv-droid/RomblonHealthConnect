namespace RomblonHealthConnect.Models.Enums;

/// <summary>
/// Lifecycle states a referral moves through. Persisted as an int.
/// </summary>
public enum ReferralStatus
{
    Draft = 0,
    Submitted = 1,
    Accepted = 2,
    Rejected = 3,
    Cancelled = 4,
    Completed = 5,
    Expired = 6
}

/// <summary>
/// Clinical urgency, which drives queue ordering and the response deadline.
/// </summary>
public enum ReferralPriority
{
    Routine = 0,
    Urgent = 1,
    Emergency = 2
}

/// <summary>
/// Every auditable event recorded against a referral timeline.
/// </summary>
public enum ReferralAction
{
    Created = 0,
    Submitted = 1,
    Accepted = 2,
    Rejected = 3,
    InformationRequested = 4,
    DoctorAssigned = 5,
    PatientScheduled = 6,
    Completed = 7,
    Cancelled = 8,
    Expired = 9,
    NoteAdded = 10,
    AttachmentAdded = 11
}
