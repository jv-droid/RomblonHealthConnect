using Microsoft.EntityFrameworkCore;
using RomblonHealthConnect.Data;
using RomblonHealthConnect.Interfaces;
using RomblonHealthConnect.Models;
using RomblonHealthConnect.Models.Enums;

namespace RomblonHealthConnect.Repositories;

public class HospitalRepository : IHospitalRepository
{
    private readonly ApplicationDbContext _context;

    public HospitalRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Hospital>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Hospitals
            .Where(h => h.IsActive)
            .OrderBy(h => h.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<Hospital?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Hospitals
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
    }

    public async Task<Hospital?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.Hospitals
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.Code == code, cancellationToken);
    }

    public async Task<IReadOnlyList<Hospital>> GetPotentialDestinationsAsync(
        int originHospitalId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Hospitals
            .Where(h => h.IsActive && h.Id != originHospitalId)
            // Facilities that are reporting and can admit come first.
            .OrderByDescending(h => h.Status == FacilityStatus.Online)
            .ThenByDescending(h => h.HasEmergency)
            .ThenBy(h => h.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetMunicipalitiesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Hospitals
            .Where(h => h.IsActive)
            .Select(h => h.Municipality)
            .Distinct()
            .OrderBy(m => m)
            .ToListAsync(cancellationToken);
    }
}

public class DoctorRepository : IDoctorRepository
{
    private readonly ApplicationDbContext _context;

    public DoctorRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Doctor>> GetByHospitalAsync(
        int hospitalId,
        CancellationToken cancellationToken = default)
    {
        return await _context.Doctors
            .Where(d => d.HospitalId == hospitalId && d.IsActive)
            .Include(d => d.PrimarySpecialization)
            .OrderBy(d => d.LastName)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Doctor>> GetAvailableAsync(
        int hospitalId,
        int? specializationId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Doctors
            .Where(d => d.HospitalId == hospitalId && d.IsActive);

        if (specializationId.HasValue)
        {
            var specId = specializationId.Value;

            // Primary specialty or any secondary one.
            query = query.Where(d =>
                d.PrimarySpecializationId == specId
                || d.DoctorSpecializations.Any(ds => ds.SpecializationId == specId));
        }

        return await query
            .Include(d => d.PrimarySpecialization)
            // Available, then on call, then in surgery, then off duty.
            .OrderBy(d => d.Availability)
            .ThenBy(d => d.LastName)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<Doctor?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Doctors
            .Include(d => d.PrimarySpecialization)
            .Include(d => d.Hospital)
            .AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
    }
}

public class SpecializationRepository : ISpecializationRepository
{
    private readonly ApplicationDbContext _context;

    public SpecializationRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Specialization>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Specializations
            .OrderBy(s => s.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Specialization>> GetByHospitalAsync(
        int hospitalId,
        CancellationToken cancellationToken = default)
    {
        // Union of primary and secondary specialties held by active staff.
        var primary = _context.Doctors
            .Where(d => d.HospitalId == hospitalId && d.IsActive)
            .Select(d => d.PrimarySpecialization);

        var secondary = _context.DoctorSpecializations
            .Where(ds => ds.Doctor.HospitalId == hospitalId && ds.Doctor.IsActive)
            .Select(ds => ds.Specialization);

        return await primary
            .Union(secondary)
            .Distinct()
            .OrderBy(s => s.Name)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }
}

public class PatientRepository : IPatientRepository
{
    private readonly ApplicationDbContext _context;

    public PatientRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Patient>> SearchAsync(
        string? term,
        int take = 25,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Patients.Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(term))
        {
            var search = term.Trim();

            query = query.Where(p =>
                p.FirstName.Contains(search)
                || p.LastName.Contains(search)
                || p.PatientNumber.Contains(search)
                || p.Municipality.Contains(search));
        }

        return await query
            .OrderBy(p => p.LastName)
            .ThenBy(p => p.FirstName)
            .Take(take)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<Patient?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Patients
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }
}
