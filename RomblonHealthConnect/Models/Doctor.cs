using RomblonHealthConnect.Models.Enums;

namespace RomblonHealthConnect.Models;

/// <summary>
/// A clinician assigned to a facility. Availability drives referral routing.
/// </summary>
public class Doctor
{
    public int Id { get; set; }

    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string LicenseNumber { get; set; } = string.Empty;

    public int HospitalId { get; set; }

    public Hospital Hospital { get; set; } = null!;

    public int PrimarySpecializationId { get; set; }

    public Specialization PrimarySpecialization { get; set; } = null!;

    public DoctorAvailability Availability { get; set; }

    public string? ContactNumber { get; set; }

    public string? Email { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<DoctorSpecialization> DoctorSpecializations { get; set; } = new List<DoctorSpecialization>();

    public string FullName => $"Dr. {FirstName} {LastName}";

    /// <summary>A doctor can receive a referral unless they are off duty.</summary>
    public bool IsAcceptingReferrals => IsActive && Availability != DoctorAvailability.OffDuty;
}
