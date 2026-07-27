using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using RomblonHealthConnect.Models.Enums;

namespace RomblonHealthConnect.ViewModels.Referrals;

/// <summary>
/// Collects every step of the create-referral wizard. The wizard is client-side,
/// so the whole model posts once on the final step.
/// </summary>
public class CreateReferralViewModel
{
    // Step 1 — Patient
    [Required(ErrorMessage = "Select a patient.")]
    [Display(Name = "Patient")]
    public int? PatientId { get; set; }

    // Step 2 — Origin hospital
    [Required(ErrorMessage = "Select the referring facility.")]
    [Display(Name = "Origin hospital")]
    public int? OriginHospitalId { get; set; }

    [Display(Name = "Referring doctor")]
    public int? ReferringDoctorId { get; set; }

    // Step 3 — Destination hospital
    [Required(ErrorMessage = "Select the receiving facility.")]
    [Display(Name = "Destination hospital")]
    public int? DestinationHospitalId { get; set; }

    [Required(ErrorMessage = "Select the specialty being requested.")]
    [Display(Name = "Requested specialization")]
    public int? RequestedSpecializationId { get; set; }

    // Step 4 — Doctor
    [Display(Name = "Preferred doctor")]
    public int? AssignedDoctorId { get; set; }

    // Step 5 — Clinical detail and attachments
    [Required(ErrorMessage = "Describe the reason for this referral.")]
    [StringLength(500, MinimumLength = 5, ErrorMessage = "Reason must be between 5 and 500 characters.")]
    [Display(Name = "Reason for referral")]
    public string ReasonForReferral { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Working diagnosis")]
    public string? Diagnosis { get; set; }

    [StringLength(4000)]
    [Display(Name = "Clinical notes")]
    public string? ClinicalNotes { get; set; }

    [Display(Name = "Priority")]
    public ReferralPriority Priority { get; set; } = ReferralPriority.Routine;

    /// <summary>Laboratory results, imaging, and supporting documents.</summary>
    public List<IFormFile> Attachments { get; set; } = new();

    /// <summary>Parallel to <see cref="Attachments"/>; defaults to Document when absent.</summary>
    public List<AttachmentCategory> AttachmentCategories { get; set; } = new();

    /// <summary>Populated when the user chooses "Save as draft" instead of "Send referral".</summary>
    public bool SaveAsDraft { get; set; }
}

/// <summary>
/// Reference data the wizard needs on first render.
/// </summary>
public class CreateReferralPageViewModel
{
    public CreateReferralViewModel Form { get; set; } = new();

    public IReadOnlyList<PatientOption> Patients { get; init; } = Array.Empty<PatientOption>();

    public IReadOnlyList<HospitalOption> Hospitals { get; init; } = Array.Empty<HospitalOption>();

    public IReadOnlyList<FilterOption> Specializations { get; init; } = Array.Empty<FilterOption>();

    public int DefaultOriginHospitalId { get; init; }

    public long MaxFileSizeBytes { get; init; }

    public IReadOnlyCollection<string> AllowedExtensions { get; init; } = Array.Empty<string>();
}

public record PatientOption(
    int Id,
    string PatientNumber,
    string FullName,
    int Age,
    string Sex,
    string Municipality,
    string? BloodType,
    string? ContactNumber);

/// <summary>
/// Hospital summary serialised to the wizard so the map and cards render without extra round trips.
/// </summary>
public record HospitalOption(
    int Id,
    string Code,
    string Name,
    string TypeKey,
    string TypeLabel,
    string Municipality,
    string Address,
    double Latitude,
    double Longitude,
    string StatusLabel,
    string StatusBadgeClass,
    bool HasEmergency,
    int AvailableBeds,
    int TotalBeds,
    string Services);
