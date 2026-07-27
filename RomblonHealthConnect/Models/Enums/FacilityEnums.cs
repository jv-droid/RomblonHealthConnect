namespace RomblonHealthConnect.Models.Enums;

/// <summary>
/// Facility classification. Mirrors the marker categories used by the GIS dashboard.
/// </summary>
public enum FacilityType
{
    Public = 0,
    District = 1,
    RuralHealthUnit = 2,
    Private = 3
}

/// <summary>
/// Connectivity/reporting state of a facility within the provincial network.
/// </summary>
public enum FacilityStatus
{
    Online = 0,
    Limited = 1,
    Offline = 2
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
