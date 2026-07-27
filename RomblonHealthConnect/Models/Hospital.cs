using RomblonHealthConnect.Models.Enums;

namespace RomblonHealthConnect.Models;

/// <summary>
/// A health facility in the provincial network. Coordinates feed the GIS map.
/// </summary>
public class Hospital
{
    public int Id { get; set; }

    /// <summary>Stable slug shared with the GIS dashboard (for example "rph-romblon").</summary>
    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public FacilityType FacilityType { get; set; }

    public string Municipality { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string? ContactNumber { get; set; }

    public string? Email { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public FacilityStatus Status { get; set; }

    public bool HasEmergency { get; set; }

    public int TotalBeds { get; set; }

    public int AvailableBeds { get; set; }

    /// <summary>Free-text service list shown on the destination hospital card.</summary>
    public string Services { get; set; } = string.Empty;

    public DateTime LastUpdatedUtc { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Doctor> Doctors { get; set; } = new List<Doctor>();

    public ICollection<Referral> OutgoingReferrals { get; set; } = new List<Referral>();

    public ICollection<Referral> IncomingReferrals { get; set; } = new List<Referral>();

    /// <summary>Human-readable facility type for display.</summary>
    public string FacilityTypeLabel => FacilityType switch
    {
        FacilityType.Public => "Provincial Hospital",
        FacilityType.District => "District Hospital",
        FacilityType.RuralHealthUnit => "Rural Health Unit",
        FacilityType.Private => "Private Facility",
        _ => "Facility"
    };
}
