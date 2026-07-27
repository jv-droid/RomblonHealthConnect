using RomblonHealthConnect.Models.Enums;

namespace RomblonHealthConnect.ViewModels.Referrals;

/// <summary>
/// Backing model for the referral dashboard: metric cards plus the first page of results.
/// </summary>
public class ReferralDashboardViewModel
{
    public int TodayCount { get; init; }

    public int PendingCount { get; init; }

    public int AcceptedCount { get; init; }

    public int RejectedCount { get; init; }

    public int CompletedCount { get; init; }

    public int IncomingCount { get; init; }

    public int OutgoingCount { get; init; }

    public PagedResult<ReferralListItemViewModel> Referrals { get; init; } = new();

    public ReferralFilter Filter { get; init; } = new();

    public IReadOnlyList<FilterOption> Hospitals { get; init; } = Array.Empty<FilterOption>();

    public IReadOnlyList<string> Municipalities { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Model shared by the Incoming, Outgoing, Pending, Completed, and Archive queues.
/// </summary>
public class ReferralListViewModel
{
    public string Title { get; init; } = "Referrals";

    public string Subtitle { get; init; } = string.Empty;

    public ReferralScope Scope { get; init; }

    public PagedResult<ReferralListItemViewModel> Referrals { get; init; } = new();

    public ReferralFilter Filter { get; init; } = new();

    public IReadOnlyList<FilterOption> Hospitals { get; init; } = Array.Empty<FilterOption>();

    public IReadOnlyList<string> Municipalities { get; init; } = Array.Empty<string>();
}

public record FilterOption(int Value, string Label);

/// <summary>
/// Model for the shared filter bar partial. <paramref name="Action"/> is the
/// controller action the form posts back to, so one partial serves every queue.
/// </summary>
public record FilterPanelModel(
    ReferralFilter Filter,
    IReadOnlyList<FilterOption> Hospitals,
    IReadOnlyList<string> Municipalities,
    string Action);

/// <summary>
/// Result of any referral state transition.
/// </summary>
public class ReferralOperationResult
{
    public bool Success { get; private init; }

    public string? Error { get; private init; }

    public int ReferralId { get; private init; }

    public string? ReferralNumber { get; private init; }

    public ReferralStatus Status { get; private init; }

    public static ReferralOperationResult Ok(int id, string number, ReferralStatus status) => new()
    {
        Success = true,
        ReferralId = id,
        ReferralNumber = number,
        Status = status
    };

    public static ReferralOperationResult Fail(string error) => new()
    {
        Success = false,
        Error = error
    };
}
