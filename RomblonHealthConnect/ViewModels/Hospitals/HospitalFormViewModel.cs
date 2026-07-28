using System.ComponentModel.DataAnnotations;
using RomblonHealthConnect.Constants;
using RomblonHealthConnect.Models;
using RomblonHealthConnect.Models.Enums;

namespace RomblonHealthConnect.ViewModels.Hospitals;

/// <summary>
/// Create and edit form for a health facility. The coordinates drive the
/// position of the marker on the provincial map.
/// </summary>
public class HospitalFormViewModel : IValidatableObject
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Enter the facility name.")]
    [StringLength(200, MinimumLength = 3)]
    [Display(Name = "Facility name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>Stable slug. Generated from the name when left blank.</summary>
    [StringLength(64)]
    [RegularExpression("^[a-z0-9-]*$", ErrorMessage = "Use lowercase letters, numbers, and hyphens only.")]
    [Display(Name = "Facility code")]
    public string? Code { get; set; }

    [Required(ErrorMessage = "Select the facility type.")]
    [Display(Name = "Facility type")]
    public FacilityType FacilityType { get; set; } = FacilityType.Public;

    [Required(ErrorMessage = "Select the municipality.")]
    [Display(Name = "Municipality")]
    public string Municipality { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter the street address.")]
    [StringLength(300)]
    [Display(Name = "Address")]
    public string Address { get; set; } = string.Empty;

    [StringLength(50)]
    [Display(Name = "Contact number")]
    public string? ContactNumber { get; set; }

    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [StringLength(200)]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "Set the location on the map.")]
    [Range(-90, 90)]
    [Display(Name = "Latitude")]
    public double? Latitude { get; set; }

    [Required(ErrorMessage = "Set the location on the map.")]
    [Range(-180, 180)]
    [Display(Name = "Longitude")]
    public double? Longitude { get; set; }

    [Display(Name = "Reporting status")]
    public FacilityStatus Status { get; set; } = FacilityStatus.Online;

    [Display(Name = "Emergency capable")]
    public bool HasEmergency { get; set; }

    [Range(0, 5000, ErrorMessage = "Enter a bed count between 0 and 5000.")]
    [Display(Name = "Total beds")]
    public int TotalBeds { get; set; }

    [Range(0, 5000, ErrorMessage = "Enter a bed count between 0 and 5000.")]
    [Display(Name = "Available beds")]
    public int AvailableBeds { get; set; }

    [StringLength(1000)]
    [Display(Name = "Services offered")]
    public string? Services { get; set; }

    [Display(Name = "Active in the network")]
    public bool IsActive { get; set; } = true;

    public bool IsEdit => Id > 0;

    /// <summary>Cross-field rules that data annotations cannot express.</summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (AvailableBeds > TotalBeds)
        {
            yield return new ValidationResult(
                "Available beds cannot exceed total beds.",
                [nameof(AvailableBeds)]);
        }

        if (!string.IsNullOrWhiteSpace(Municipality)
            && !RomblonGeography.Municipalities.Contains(Municipality))
        {
            yield return new ValidationResult(
                "Select a municipality within Romblon.",
                [nameof(Municipality)]);
        }

        // Catches a swapped latitude/longitude or a stray digit before it puts a
        // marker in the middle of the sea.
        if (Latitude.HasValue && Longitude.HasValue
            && !RomblonGeography.IsWithinProvince(Latitude.Value, Longitude.Value))
        {
            yield return new ValidationResult(
                "Those coordinates fall outside Romblon province. Place the pin on the map to correct it.",
                [nameof(Latitude)]);
        }
    }

    public static HospitalFormViewModel FromEntity(Hospital hospital) => new()
    {
        Id = hospital.Id,
        Name = hospital.Name,
        Code = hospital.Code,
        FacilityType = hospital.FacilityType,
        Municipality = hospital.Municipality,
        Address = hospital.Address,
        ContactNumber = hospital.ContactNumber,
        Email = hospital.Email,
        Latitude = hospital.Latitude,
        Longitude = hospital.Longitude,
        Status = hospital.Status,
        HasEmergency = hospital.HasEmergency,
        TotalBeds = hospital.TotalBeds,
        AvailableBeds = hospital.AvailableBeds,
        Services = hospital.Services,
        IsActive = hospital.IsActive
    };

    /// <summary>Copies the posted values onto a tracked entity.</summary>
    public void ApplyTo(Hospital hospital)
    {
        hospital.Name = Name.Trim();
        hospital.FacilityType = FacilityType;
        hospital.Municipality = Municipality;
        hospital.Address = Address.Trim();
        hospital.ContactNumber = ContactNumber?.Trim();
        hospital.Email = Email?.Trim();
        hospital.Latitude = Latitude!.Value;
        hospital.Longitude = Longitude!.Value;
        hospital.Status = Status;
        hospital.HasEmergency = HasEmergency;
        hospital.TotalBeds = TotalBeds;
        hospital.AvailableBeds = AvailableBeds;
        hospital.Services = Services?.Trim() ?? string.Empty;
        hospital.IsActive = IsActive;
        hospital.LastUpdatedUtc = DateTime.UtcNow;
    }
}

/// <summary>Row in the facility management table.</summary>
public class HospitalListItemViewModel
{
    public int Id { get; init; }
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public FacilityType FacilityType { get; init; }
    public string TypeLabel { get; init; } = string.Empty;
    public string Municipality { get; init; } = string.Empty;
    public FacilityStatus Status { get; init; }
    public bool HasEmergency { get; init; }
    public int AvailableBeds { get; init; }
    public int TotalBeds { get; init; }
    public int DoctorCount { get; init; }
    public bool IsActive { get; init; }

    public static HospitalListItemViewModel FromEntity(Hospital hospital, int doctorCount) => new()
    {
        Id = hospital.Id,
        Code = hospital.Code,
        Name = hospital.Name,
        FacilityType = hospital.FacilityType,
        TypeLabel = hospital.FacilityTypeLabel,
        Municipality = hospital.Municipality,
        Status = hospital.Status,
        HasEmergency = hospital.HasEmergency,
        AvailableBeds = hospital.AvailableBeds,
        TotalBeds = hospital.TotalBeds,
        DoctorCount = doctorCount,
        IsActive = hospital.IsActive
    };
}

public class HospitalListViewModel
{
    public IReadOnlyList<HospitalListItemViewModel> Hospitals { get; init; } = [];
    public string? SearchTerm { get; init; }
    public string? Municipality { get; init; }
    public FacilityType? FacilityType { get; init; }
    public IReadOnlyList<string> Municipalities { get; init; } = [];

    public int TotalCount => Hospitals.Count;
    public int OnlineCount => Hospitals.Count(h => h.Status == FacilityStatus.Online);
    public int EmergencyCount => Hospitals.Count(h => h.HasEmergency);
}
