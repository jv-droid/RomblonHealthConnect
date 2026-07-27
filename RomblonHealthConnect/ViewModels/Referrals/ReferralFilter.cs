using RomblonHealthConnect.Models.Enums;

namespace RomblonHealthConnect.ViewModels.Referrals;

/// <summary>
/// Which queue the user is looking at. Determines the base query before filters apply.
/// </summary>
public enum ReferralScope
{
    All = 0,
    Incoming = 1,
    Outgoing = 2,
    Pending = 3,
    Completed = 4,
    Archive = 5
}

/// <summary>
/// Search, filter, and paging criteria for every referral list in the module.
/// </summary>
public class ReferralFilter
{
    public const int DefaultPageSize = 15;

    /// <summary>Facility acting as "us" — drives the Incoming and Outgoing queues.</summary>
    public int CurrentHospitalId { get; set; }

    public ReferralScope Scope { get; set; } = ReferralScope.All;

    /// <summary>Matches referral number, patient name/number, hospital, or doctor.</summary>
    public string? SearchTerm { get; set; }

    public ReferralStatus? Status { get; set; }

    public ReferralPriority? Priority { get; set; }

    /// <summary>Filters on either side of the transfer.</summary>
    public int? HospitalId { get; set; }

    public string? Municipality { get; set; }

    public DateTime? DateFrom { get; set; }

    public DateTime? DateTo { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = DefaultPageSize;

    /// <summary>True when any user-supplied filter is active, used to show the reset control.</summary>
    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(SearchTerm)
        || Status.HasValue
        || Priority.HasValue
        || HospitalId.HasValue
        || !string.IsNullOrWhiteSpace(Municipality)
        || DateFrom.HasValue
        || DateTo.HasValue;

    /// <summary>Clamps paging values into a safe range.</summary>
    public void Normalise()
    {
        if (Page < 1)
        {
            Page = 1;
        }

        if (PageSize is < 1 or > 100)
        {
            PageSize = DefaultPageSize;
        }
    }
}

/// <summary>
/// A single page of results plus the paging metadata the view needs.
/// </summary>
public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; init; } = Array.Empty<T>();

    public int TotalCount { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = ReferralFilter.DefaultPageSize;

    public int TotalPages => PageSize > 0 ? (int)Math.Ceiling(TotalCount / (double)PageSize) : 0;

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;

    public int FirstItemNumber => TotalCount == 0 ? 0 : ((Page - 1) * PageSize) + 1;

    public int LastItemNumber => Math.Min(Page * PageSize, TotalCount);

    /// <summary>Projects the items while preserving paging metadata.</summary>
    public PagedResult<TOut> Map<TOut>(Func<T, TOut> selector) => new()
    {
        Items = Items.Select(selector).ToList(),
        TotalCount = TotalCount,
        Page = Page,
        PageSize = PageSize
    };
}
