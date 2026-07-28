using RomblonHealthConnect.Models.Enums;

namespace RomblonHealthConnect.Models;

/// <summary>
/// A health facility in the provincial network. Coordinates feed the GIS map.
///
/// Phase 1 extended this entity additively. Several Phase 1 requirements were
/// already satisfied by existing columns and were deliberately NOT duplicated:
///
///   FacilityCode      -> Code            (already unique-indexed)
///   FullAddress       -> Address
///   BedCapacity       -> TotalBeds
///   LastReportedAt    -> LastUpdatedUtc
///   NetworkStatus     -> Status
///   IsEmergencyCapable-> HasEmergency
///
/// Aliases are exposed as [NotMapped] convenience properties where the Phase 1
/// vocabulary is clearer, so no data is copied into a second column.
/// </summary>
public class Hospital
{
    public int Id { get; set; }

    /// <summary>Stable slug shared with the GIS dashboard (for example "rph-romblon").</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>Short label for dense tables and map labels.</summary>
    public string? ShortName { get; set; }

    public FacilityType FacilityType { get; set; }

    public HospitalOwnershipType OwnershipType { get; set; }

    public string Municipality { get; set; } = string.Empty;

    public string? Barangay { get; set; }

    public string Province { get; set; } = "Romblon";

    public string Region { get; set; } = "MIMAROPA";

    public string Address { get; set; } = string.Empty;

    public string? ContactNumber { get; set; }

    public string? Email { get; set; }

    public string? WebsiteUrl { get; set; }

    public string? LicenseNumber { get; set; }

    public string? PhilHealthAccreditationNumber { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public FacilityStatus Status { get; set; }

    public bool HasEmergency { get; set; }

    public bool HasOperatingRoom { get; set; }

    public bool HasLaboratory { get; set; }

    public bool HasPharmacy { get; set; }

    public bool HasAmbulance { get; set; }

    /// <summary>Whether the facility accepts inbound referrals.</summary>
    public bool IsReferralReceivingFacility { get; set; } = true;

    public int TotalBeds { get; set; }

    public int AvailableBeds { get; set; }

    /// <summary>Free-text service list shown on the destination hospital card.</summary>
    public string Services { get; set; } = string.Empty;

    public DateTime LastUpdatedUtc { get; set; }

    public bool IsActive { get; set; } = true;

    /* -- soft delete and audit ----------------------------------------- */

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public string? UpdatedBy { get; set; }

    /* -- navigation ----------------------------------------------------- */

    public ICollection<Doctor> Doctors { get; set; } = new List<Doctor>();

    public ICollection<Referral> OutgoingReferrals { get; set; } = new List<Referral>();

    public ICollection<Referral> IncomingReferrals { get; set; } = new List<Referral>();

    /* -- computed ------------------------------------------------------- */

    /// <summary>Human-readable facility type for display.</summary>
    public string FacilityTypeLabel => FacilityType switch
    {
        FacilityType.Public => "Provincial Hospital",
        FacilityType.District => "District Hospital",
        FacilityType.RuralHealthUnit => "Rural Health Unit",
        FacilityType.Private => "Private Facility",
        FacilityType.MunicipalHospital => "Municipal Hospital",
        FacilityType.Infirmary => "Infirmary",
        FacilityType.HealthCenter => "Health Center",
        FacilityType.SpecialtyClinic => "Specialty Clinic",
        FacilityType.DiagnosticFacility => "Diagnostic Facility",
        FacilityType.Other => "Other Facility",
        _ => "Facility"
    };

    public string OwnershipLabel => OwnershipType switch
    {
        HospitalOwnershipType.ProvincialGovernment => "Provincial Government",
        HospitalOwnershipType.MunicipalGovernment => "Municipal Government",
        HospitalOwnershipType.NationalGovernment => "National Government",
        HospitalOwnershipType.Private => "Private",
        HospitalOwnershipType.NonGovernmentOrganization => "Non-Government Organization",
        _ => "Other"
    };

    /* -- Phase 1 vocabulary aliases over existing columns --------------- */

    /// <summary>Alias for <see cref="Code"/>.</summary>
    public string FacilityCode => Code;

    /// <summary>Alias for <see cref="Address"/>.</summary>
    public string FullAddress => Address;

    /// <summary>Alias for <see cref="TotalBeds"/>.</summary>
    public int BedCapacity => TotalBeds;

    /// <summary>Alias for <see cref="LastUpdatedUtc"/>.</summary>
    public DateTime LastReportedAt => LastUpdatedUtc;

    /// <summary>Alias for <see cref="Status"/>.</summary>
    public FacilityStatus NetworkStatus => Status;

    /// <summary>Alias for <see cref="HasEmergency"/>.</summary>
    public bool IsEmergencyCapable => HasEmergency;
}
