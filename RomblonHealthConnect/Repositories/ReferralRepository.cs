using Microsoft.EntityFrameworkCore;
using RomblonHealthConnect.Data;
using RomblonHealthConnect.Interfaces;
using RomblonHealthConnect.Models;
using RomblonHealthConnect.Models.Enums;
using RomblonHealthConnect.ViewModels.Referrals;

namespace RomblonHealthConnect.Repositories;

/// <summary>
/// EF Core implementation of the referral aggregate queries.
/// </summary>
public class ReferralRepository : IReferralRepository
{
    private readonly ApplicationDbContext _context;
    private readonly IReferralAuthorizationService _authorization;

    public ReferralRepository(
        ApplicationDbContext context,
        IReferralAuthorizationService authorization)
    {
        _context = context;
        _authorization = authorization;
    }

    /// <summary>
    /// Every read path starts here. The record scope is part of the query, so an
    /// out-of-scope referral is never returned and never materialised.
    /// </summary>
    private IQueryable<Referral> ScopedReferrals() =>
        _authorization.ApplyScope(_context.Referrals);

    public async Task<Referral?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await ScopedReferrals()
            .Include(r => r.Patient)
            .Include(r => r.OriginHospital)
            .Include(r => r.DestinationHospital)
            .Include(r => r.RequestedSpecialization)
            .Include(r => r.AssignedDoctor).ThenInclude(d => d!.PrimarySpecialization)
            .Include(r => r.ReferringDoctor)
            .Include(r => r.Attachments)
            .Include(r => r.History)
            // Two collection includes would otherwise produce a cartesian product.
            .AsSplitQuery()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<Referral?> GetByNumberAsync(string referralNumber, CancellationToken cancellationToken = default)
    {
        return await ScopedReferrals()
            .Include(r => r.Patient)
            .Include(r => r.OriginHospital)
            .Include(r => r.DestinationHospital)
            .FirstOrDefaultAsync(r => r.ReferralNumber == referralNumber, cancellationToken);
    }

    public async Task<PagedResult<Referral>> SearchAsync(
        ReferralFilter filter,
        CancellationToken cancellationToken = default)
    {
        filter.Normalise();

        var query = BuildQuery(filter);

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            // Emergency first, then most recent.
            .OrderByDescending(r => r.Priority)
            .ThenByDescending(r => r.CreatedUtc)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Include(r => r.Patient)
            .Include(r => r.OriginHospital)
            .Include(r => r.DestinationHospital)
            .Include(r => r.RequestedSpecialization)
            .Include(r => r.AssignedDoctor)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return new PagedResult<Referral>
        {
            Items = items,
            TotalCount = total,
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }

    public async Task<IReadOnlyDictionary<ReferralStatus, int>> GetStatusCountsAsync(
        ReferralFilter filter,
        CancellationToken cancellationToken = default)
    {
        var counts = await BuildQuery(filter)
            .GroupBy(r => r.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        return counts.ToDictionary(c => c.Status, c => c.Count);
    }

    public async Task<int> CountCreatedOnAsync(DateTime dateUtc, CancellationToken cancellationToken = default)
    {
        var start = dateUtc.Date;
        var end = start.AddDays(1);

        return await ScopedReferrals()
            .CountAsync(r => r.CreatedUtc >= start && r.CreatedUtc < end, cancellationToken);
    }

    public async Task<IReadOnlyList<Referral>> GetRecentAsync(int count, CancellationToken cancellationToken = default)
    {
        return await ScopedReferrals()
            .OrderByDescending(r => r.CreatedUtc)
            .Take(count)
            .Include(r => r.Patient)
            .Include(r => r.OriginHospital)
            .Include(r => r.DestinationHospital)
            .Include(r => r.RequestedSpecialization)
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<int> GetNextSequenceForYearAsync(int year, CancellationToken cancellationToken = default)
    {
        var prefix = $"RF-{year}-";

        // Deliberately unscoped: referral numbers are provincial, so the next
        // sequence must consider every facility's records. No referral data is
        // returned here, only the number.
        var lastNumber = await _context.Referrals
            .Where(r => r.ReferralNumber.StartsWith(prefix))
            .OrderByDescending(r => r.ReferralNumber)
            .Select(r => r.ReferralNumber)
            .FirstOrDefaultAsync(cancellationToken);

        if (lastNumber is null)
        {
            return 1;
        }

        var suffix = lastNumber[prefix.Length..];
        return int.TryParse(suffix, out var sequence) ? sequence + 1 : 1;
    }

    public async Task AddAsync(Referral referral, CancellationToken cancellationToken = default)
    {
        await _context.Referrals.AddAsync(referral, cancellationToken);
    }

    public void Update(Referral referral)
    {
        _context.Referrals.Update(referral);
    }

    public async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
    {
        // Scoped on purpose: an out-of-scope referral must read as non-existent.
        return await ScopedReferrals().AnyAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Applies scope first, then the user-supplied filters. Kept separate so the
    /// list query and the status-count query stay in sync.
    /// </summary>
    private IQueryable<Referral> BuildQuery(ReferralFilter filter)
    {
        // Search, queues, and dashboard counts all funnel through here, so the
        // scope applies to every one of them automatically.
        var query = ScopedReferrals();

        query = filter.Scope switch
        {
            ReferralScope.Incoming => query.Where(r =>
                r.DestinationHospitalId == filter.CurrentHospitalId
                && r.Status != ReferralStatus.Draft
                && !r.IsArchived),

            ReferralScope.Outgoing => query.Where(r =>
                r.OriginHospitalId == filter.CurrentHospitalId
                && !r.IsArchived),

            ReferralScope.Pending => query.Where(r =>
                (r.Status == ReferralStatus.Submitted || r.Status == ReferralStatus.Accepted)
                && !r.IsArchived),

            ReferralScope.Completed => query.Where(r =>
                r.Status == ReferralStatus.Completed && !r.IsArchived),

            ReferralScope.Archive => query.Where(r => r.IsArchived),

            _ => query.Where(r => !r.IsArchived)
        };

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var term = filter.SearchTerm.Trim();

            query = query.Where(r =>
                r.ReferralNumber.Contains(term)
                || r.Patient.FirstName.Contains(term)
                || r.Patient.LastName.Contains(term)
                || r.Patient.PatientNumber.Contains(term)
                || r.OriginHospital.Name.Contains(term)
                || r.DestinationHospital.Name.Contains(term)
                || r.RequestedSpecialization.Name.Contains(term)
                || (r.AssignedDoctor != null && r.AssignedDoctor.LastName.Contains(term)));
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(r => r.Status == filter.Status.Value);
        }

        if (filter.Priority.HasValue)
        {
            query = query.Where(r => r.Priority == filter.Priority.Value);
        }

        if (filter.HospitalId.HasValue)
        {
            var hospitalId = filter.HospitalId.Value;
            query = query.Where(r => r.OriginHospitalId == hospitalId || r.DestinationHospitalId == hospitalId);
        }

        if (!string.IsNullOrWhiteSpace(filter.Municipality))
        {
            var municipality = filter.Municipality;
            query = query.Where(r =>
                r.OriginHospital.Municipality == municipality
                || r.DestinationHospital.Municipality == municipality);
        }

        if (filter.DateFrom.HasValue)
        {
            var from = filter.DateFrom.Value.Date;
            query = query.Where(r => r.CreatedUtc >= from);
        }

        if (filter.DateTo.HasValue)
        {
            // Inclusive of the whole selected day.
            var to = filter.DateTo.Value.Date.AddDays(1);
            query = query.Where(r => r.CreatedUtc < to);
        }

        return query;
    }
}
