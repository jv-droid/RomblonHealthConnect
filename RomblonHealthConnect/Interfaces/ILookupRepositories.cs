using RomblonHealthConnect.Models;

namespace RomblonHealthConnect.Interfaces;

/// <summary>
/// Facility lookups shared by the referral wizard and the GIS dashboard.
/// </summary>
public interface IHospitalRepository
{
    Task<IReadOnlyList<Hospital>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Hospital?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<Hospital?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    /// <summary>Every active facility except the origin, ordered for the destination picker.</summary>
    Task<IReadOnlyList<Hospital>> GetPotentialDestinationsAsync(
        int originHospitalId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetMunicipalitiesAsync(CancellationToken cancellationToken = default);

    /* -- management ---------------------------------------------------- */

    /// <summary>Includes inactive facilities, for the management list.</summary>
    Task<IReadOnlyList<Hospital>> GetAllIncludingInactiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Tracked entity, for editing. <see cref="GetByIdAsync"/> returns a detached copy.</summary>
    Task<Hospital?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>True when the code is already taken by a different facility.</summary>
    Task<bool> CodeExistsAsync(string code, int? excludeId = null, CancellationToken cancellationToken = default);

    /// <summary>Counts referrals and staff, so deletion can be blocked when in use.</summary>
    Task<(int Referrals, int Doctors)> GetUsageAsync(int id, CancellationToken cancellationToken = default);

    Task AddAsync(Hospital hospital, CancellationToken cancellationToken = default);

    void Remove(Hospital hospital);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Doctor lookups, primarily for proposing recipients at a destination facility.
/// </summary>
public interface IDoctorRepository
{
    Task<IReadOnlyList<Doctor>> GetByHospitalAsync(int hospitalId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Doctors at the destination who can take the referral, optionally narrowed to a specialty.
    /// </summary>
    Task<IReadOnlyList<Doctor>> GetAvailableAsync(
        int hospitalId,
        int? specializationId = null,
        CancellationToken cancellationToken = default);

    Task<Doctor?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}

public interface ISpecializationRepository
{
    Task<IReadOnlyList<Specialization>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Specialties actually staffed at the given facility.</summary>
    Task<IReadOnlyList<Specialization>> GetByHospitalAsync(
        int hospitalId,
        CancellationToken cancellationToken = default);
}

public interface IPatientRepository
{
    Task<IReadOnlyList<Patient>> SearchAsync(string? term, int take = 25, CancellationToken cancellationToken = default);

    Task<Patient?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
}
