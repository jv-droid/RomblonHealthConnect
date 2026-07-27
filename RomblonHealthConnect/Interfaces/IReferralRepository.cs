using RomblonHealthConnect.Models;
using RomblonHealthConnect.Models.Enums;
using RomblonHealthConnect.ViewModels.Referrals;

namespace RomblonHealthConnect.Interfaces;

/// <summary>
/// Data access for the referral aggregate.
/// </summary>
public interface IReferralRepository
{
    /// <summary>Referral with patient, both hospitals, specialisation, doctors, history, and attachments.</summary>
    Task<Referral?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<Referral?> GetByNumberAsync(string referralNumber, CancellationToken cancellationToken = default);

    /// <summary>Applies search and filter criteria and returns a single page of results.</summary>
    Task<PagedResult<Referral>> SearchAsync(ReferralFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Referral counts grouped by status, honouring the supplied filter.</summary>
    Task<IReadOnlyDictionary<ReferralStatus, int>> GetStatusCountsAsync(
        ReferralFilter filter,
        CancellationToken cancellationToken = default);

    Task<int> CountCreatedOnAsync(DateTime dateUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Referral>> GetRecentAsync(int count, CancellationToken cancellationToken = default);

    /// <summary>Next sequence number for the given year, used to build the referral number.</summary>
    Task<int> GetNextSequenceForYearAsync(int year, CancellationToken cancellationToken = default);

    Task AddAsync(Referral referral, CancellationToken cancellationToken = default);

    void Update(Referral referral);

    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
