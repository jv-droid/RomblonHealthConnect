namespace RomblonHealthConnect.Constants;

/// <summary>Canonical role names. Referenced instead of string literals.</summary>
public static class Roles
{
    public const string ProvincialAdministrator = "ProvincialAdministrator";
    public const string PHOAdministrator = "PHOAdministrator";
    public const string HospitalAdministrator = "HospitalAdministrator";
    public const string ReferralCoordinator = "ReferralCoordinator";
    public const string Doctor = "Doctor";
    public const string Nurse = "Nurse";
    public const string RecordsOfficer = "RecordsOfficer";
    public const string ExecutiveViewer = "ExecutiveViewer";
    public const string SystemAuditor = "SystemAuditor";

    /// <summary>Seeded on startup, with the description shown in administration.</summary>
    public static readonly IReadOnlyList<(string Name, string Description)> All =
    [
        (ProvincialAdministrator, "Full platform access: facilities, users, roles, and configuration."),
        (PHOAdministrator, "Province-wide health operations oversight and approved master data."),
        (HospitalAdministrator, "Manages the assigned hospital, its staff, and its operational reports."),
        (ReferralCoordinator, "Creates, submits, receives, and monitors referrals for the assigned hospital."),
        (Doctor, "Views assigned referrals and maintains availability."),
        (Nurse, "Assists with operational and referral coordination tasks."),
        (RecordsOfficer, "Maintains permitted records and attachments."),
        (ExecutiveViewer, "Read-only Executive Mode, GIS dashboard, and high-level reports."),
        (SystemAuditor, "Read-only audit and compliance access.")
    ];

    /// <summary>Roles whose scope is the whole province rather than one facility.</summary>
    public static readonly IReadOnlyList<string> ProvinceWide =
    [
        ProvincialAdministrator,
        PHOAdministrator,
        ExecutiveViewer,
        SystemAuditor
    ];
}

/// <summary>Policy names used by [Authorize(Policy = ...)].</summary>
public static class Policies
{
    public const string CanManageHospitals = "CanManageHospitals";
    public const string CanManageUsers = "CanManageUsers";
    public const string CanViewProvinceWideData = "CanViewProvinceWideData";
    public const string CanManageHospitalData = "CanManageHospitalData";
    public const string CanCreateReferral = "CanCreateReferral";
    public const string CanReviewReferral = "CanReviewReferral";
    public const string CanViewExecutiveDashboard = "CanViewExecutiveDashboard";
    public const string CanViewAuditLogs = "CanViewAuditLogs";
}
