namespace RomblonHealthConnect.Models.Enums;

/// <summary>
/// Clinical grouping of an uploaded file, used to drive the attachment preview.
/// </summary>
public enum AttachmentCategory
{
    Laboratory = 0,
    Imaging = 1,
    Document = 2,
    Other = 3
}

/// <summary>
/// Notification categories surfaced in the notification centre.
/// </summary>
public enum NotificationType
{
    ReferralReceived = 0,
    ReferralAccepted = 1,
    ReferralRejected = 2,
    DoctorAssigned = 3,
    ReferralCompleted = 4,
    InformationRequested = 5,
    ReferralCancelled = 6,
    ReferralExpired = 7
}
