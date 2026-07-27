namespace RomblonHealthConnect.Models;

/// <summary>
/// Join entity letting a doctor hold additional specialties beyond their primary one.
/// </summary>
public class DoctorSpecialization
{
    public int DoctorId { get; set; }

    public Doctor Doctor { get; set; } = null!;

    public int SpecializationId { get; set; }

    public Specialization Specialization { get; set; } = null!;
}
