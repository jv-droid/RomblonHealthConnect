namespace RomblonHealthConnect.Models;

/// <summary>
/// A clinical specialty that a referral can request.
/// </summary>
public class Specialization
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Primary-care specialties are excluded from the "specialists available" metric.</summary>
    public bool IsPrimaryCare { get; set; }

    public ICollection<Doctor> Doctors { get; set; } = new List<Doctor>();

    public ICollection<DoctorSpecialization> DoctorSpecializations { get; set; } = new List<DoctorSpecialization>();

    public ICollection<Referral> Referrals { get; set; } = new List<Referral>();
}
