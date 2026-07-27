using RomblonHealthConnect.Models;
using RomblonHealthConnect.Models.Enums;

namespace RomblonHealthConnect.ViewModels.Referrals;

/// <summary>
/// One row in the referral table.
/// </summary>
public class ReferralListItemViewModel
{
    public int Id { get; init; }

    public string ReferralNumber { get; init; } = string.Empty;

    public string PatientName { get; init; } = string.Empty;

    public string PatientNumber { get; init; } = string.Empty;

    public string OriginHospital { get; init; } = string.Empty;

    public string DestinationHospital { get; init; } = string.Empty;

    public string RequestedSpecialist { get; init; } = string.Empty;

    public string? AssignedDoctor { get; init; }

    public ReferralStatus Status { get; init; }

    public ReferralPriority Priority { get; init; }

    public DateTime CreatedUtc { get; init; }

    public bool IsArchived { get; init; }

    public static ReferralListItemViewModel FromEntity(Referral referral) => new()
    {
        Id = referral.Id,
        ReferralNumber = referral.ReferralNumber,
        PatientName = referral.Patient.FullName,
        PatientNumber = referral.Patient.PatientNumber,
        OriginHospital = referral.OriginHospital.Name,
        DestinationHospital = referral.DestinationHospital.Name,
        RequestedSpecialist = referral.RequestedSpecialization.Name,
        AssignedDoctor = referral.AssignedDoctor?.FullName,
        Status = referral.Status,
        Priority = referral.Priority,
        CreatedUtc = referral.CreatedUtc,
        IsArchived = referral.IsArchived
    };
}

/// <summary>
/// Maps referral enums onto the Fluent badge classes defined in the Phase 2 stylesheet.
/// Kept in one place so every view renders status identically.
/// </summary>
public static class ReferralDisplay
{
    public static string StatusBadgeClass(ReferralStatus status) => status switch
    {
        ReferralStatus.Draft => "rhc-badge-neutral",
        ReferralStatus.Submitted => "rhc-badge-warning",
        ReferralStatus.Accepted => "rhc-badge-success",
        ReferralStatus.Rejected => "rhc-badge-danger",
        ReferralStatus.Cancelled => "rhc-badge-neutral",
        ReferralStatus.Completed => "rhc-badge-info",
        ReferralStatus.Expired => "rhc-badge-neutral",
        _ => "rhc-badge-neutral"
    };

    public static string StatusLabel(ReferralStatus status) => status switch
    {
        ReferralStatus.Draft => "Draft",
        ReferralStatus.Submitted => "Submitted",
        ReferralStatus.Accepted => "Accepted",
        ReferralStatus.Rejected => "Rejected",
        ReferralStatus.Cancelled => "Cancelled",
        ReferralStatus.Completed => "Completed",
        ReferralStatus.Expired => "Expired",
        _ => status.ToString()
    };

    public static string PriorityBadgeClass(ReferralPriority priority) => priority switch
    {
        ReferralPriority.Routine => "rhc-badge-neutral",
        ReferralPriority.Urgent => "rhc-badge-warning",
        ReferralPriority.Emergency => "rhc-badge-danger",
        _ => "rhc-badge-neutral"
    };

    public static string PriorityLabel(ReferralPriority priority) => priority.ToString();

    public static string FacilityTypeKey(FacilityType type) => type switch
    {
        FacilityType.Public => "public",
        FacilityType.District => "district",
        FacilityType.RuralHealthUnit => "rhu",
        FacilityType.Private => "private",
        _ => "public"
    };

    public static string FacilityStatusBadgeClass(FacilityStatus status) => status switch
    {
        FacilityStatus.Online => "rhc-badge-success",
        FacilityStatus.Limited => "rhc-badge-warning",
        FacilityStatus.Offline => "rhc-badge-neutral",
        _ => "rhc-badge-neutral"
    };

    public static string AvailabilityBadgeClass(DoctorAvailability availability) => availability switch
    {
        DoctorAvailability.Available => "rhc-badge-success",
        DoctorAvailability.OnCall => "rhc-badge-info",
        DoctorAvailability.InSurgery => "rhc-badge-warning",
        DoctorAvailability.OffDuty => "rhc-badge-neutral",
        _ => "rhc-badge-neutral"
    };

    public static string AvailabilityLabel(DoctorAvailability availability) => availability switch
    {
        DoctorAvailability.Available => "Available",
        DoctorAvailability.OnCall => "On call",
        DoctorAvailability.InSurgery => "In surgery",
        DoctorAvailability.OffDuty => "Off duty",
        _ => availability.ToString()
    };
}
