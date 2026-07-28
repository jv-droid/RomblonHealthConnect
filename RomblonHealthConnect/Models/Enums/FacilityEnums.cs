namespace RomblonHealthConnect.Models.Enums;

/// <summary>
/// Facility classification. Mirrors the marker categories used by the GIS dashboard.
///
/// Values 0-3 are already persisted in dbo.Hospitals and MUST keep their meaning.
/// Later members are appended so existing rows are unaffected.
/// </summary>
public enum FacilityType
{
    Public = 0,
    District = 1,
    RuralHealthUnit = 2,
    Private = 3,

    // Appended in Phase 1. No existing row uses these.
    MunicipalHospital = 4,
    Infirmary = 5,
    HealthCenter = 6,
    SpecialtyClinic = 7,
    DiagnosticFacility = 8,
    Other = 9
}

/// <summary>
/// Connectivity/reporting state of a facility within the provincial network.
///
/// Values 0-2 are already persisted in dbo.Hospitals.Status and keep their meaning.
/// </summary>
public enum FacilityStatus
{
    Online = 0,
    Limited = 1,
    Offline = 2,

    // Appended in Phase 1.
    Maintenance = 3,
    Unknown = 4
}

/// <summary>
/// Who operates the facility. New in Phase 1; existing rows are backfilled from
/// FacilityType, so no row is left at an arbitrary default.
/// </summary>
public enum HospitalOwnershipType
{
    ProvincialGovernment = 0,
    MunicipalGovernment = 1,
    NationalGovernment = 2,
    Private = 3,
    NonGovernmentOrganization = 4,
    Other = 5
}

/// <summary>
/// Current duty state of a doctor, used when proposing referral recipients.
/// </summary>
public enum DoctorAvailability
{
    Available = 0,
    OnCall = 1,
    InSurgery = 2,
    OffDuty = 3
}

public enum Sex
{
    Male = 0,
    Female = 1
}
