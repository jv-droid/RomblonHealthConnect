using RomblonHealthConnect.Models.Enums;

namespace RomblonHealthConnect.Models;

/// <summary>
/// A patient record. Referrals always carry the patient rather than copying demographics.
/// </summary>
public class Patient
{
    public int Id { get; set; }

    /// <summary>Provincial patient identifier, for example "PT-2026-00184".</summary>
    public string PatientNumber { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string? MiddleName { get; set; }

    public string LastName { get; set; } = string.Empty;

    public DateOnly DateOfBirth { get; set; }

    public Sex Sex { get; set; }

    public string? ContactNumber { get; set; }

    public string Address { get; set; } = string.Empty;

    public string Municipality { get; set; } = string.Empty;

    public string? BloodType { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<Referral> Referrals { get; set; } = new List<Referral>();

    public string FullName => string.IsNullOrWhiteSpace(MiddleName)
        ? $"{FirstName} {LastName}"
        : $"{FirstName} {MiddleName} {LastName}";

    /// <summary>Age in whole years as of today.</summary>
    public int Age
    {
        get
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var age = today.Year - DateOfBirth.Year;
            if (DateOfBirth > today.AddYears(-age))
            {
                age--;
            }

            return age;
        }
    }
}
