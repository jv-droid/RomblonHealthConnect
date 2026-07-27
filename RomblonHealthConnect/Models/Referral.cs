using RomblonHealthConnect.Models.Enums;

namespace RomblonHealthConnect.Models;

/// <summary>
/// An electronic patient transfer request between two facilities.
/// This is the aggregate root of the referral engine.
/// </summary>
public class Referral
{
    public int Id { get; set; }

    /// <summary>Human-facing reference, for example "RF-2026-0418".</summary>
    public string ReferralNumber { get; set; } = string.Empty;

    public int PatientId { get; set; }

    public Patient Patient { get; set; } = null!;

    public int OriginHospitalId { get; set; }

    public Hospital OriginHospital { get; set; } = null!;

    public int DestinationHospitalId { get; set; }

    public Hospital DestinationHospital { get; set; } = null!;

    public int RequestedSpecializationId { get; set; }

    public Specialization RequestedSpecialization { get; set; } = null!;

    /// <summary>Clinician at the destination facility. Assigned on or after acceptance.</summary>
    public int? AssignedDoctorId { get; set; }

    public Doctor? AssignedDoctor { get; set; }

    /// <summary>Clinician at the origin facility who raised the referral.</summary>
    public int? ReferringDoctorId { get; set; }

    public Doctor? ReferringDoctor { get; set; }

    public ReferralStatus Status { get; set; } = ReferralStatus.Draft;

    public ReferralPriority Priority { get; set; } = ReferralPriority.Routine;

    public string ReasonForReferral { get; set; } = string.Empty;

    public string? Diagnosis { get; set; }

    public string? ClinicalNotes { get; set; }

    /// <summary>Populated by the receiving facility when accepting, rejecting, or querying.</summary>
    public string? ResponseNotes { get; set; }

    public DateTime CreatedUtc { get; set; }

    public DateTime? SubmittedUtc { get; set; }

    public DateTime? RespondedUtc { get; set; }

    public DateTime? ScheduledUtc { get; set; }

    public DateTime? CompletedUtc { get; set; }

    /// <summary>Submitted referrals expire if the destination does not respond in time.</summary>
    public DateTime? ExpiresUtc { get; set; }

    public bool IsArchived { get; set; }

    public ICollection<ReferralHistory> History { get; set; } = new List<ReferralHistory>();

    public ICollection<ReferralAttachment> Attachments { get; set; } = new List<ReferralAttachment>();

    /// <summary>Statuses that still require action from one of the facilities.</summary>
    public bool IsOpen => Status is ReferralStatus.Submitted or ReferralStatus.Accepted;

    /// <summary>Statuses that can no longer transition.</summary>
    public bool IsTerminal => Status is ReferralStatus.Completed
        or ReferralStatus.Rejected
        or ReferralStatus.Cancelled
        or ReferralStatus.Expired;
}
