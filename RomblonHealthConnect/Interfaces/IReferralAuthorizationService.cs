using RomblonHealthConnect.Models;

namespace RomblonHealthConnect.Interfaces;

/// <summary>
/// Record-level authorization for the referral module.
///
/// The scope is derived only from the authenticated user's roles and their
/// HospitalId claim. Nothing from the route, query string, form, or JavaScript
/// is ever consulted.
/// </summary>
public interface IReferralAuthorizationService
{
    /// <summary>
    /// Narrows a referral query to the records this user may see.
    ///
    /// This is the primary mechanism: applying it inside the EF query means an
    /// out-of-scope referral is never materialised, so it cannot leak through a
    /// later mistake. Callers must use this rather than loading and then checking.
    /// </summary>
    IQueryable<Referral> ApplyScope(IQueryable<Referral> query);

    /// <summary>True when the user has province-wide reach.</summary>
    bool HasProvinceWideScope { get; }

    /// <summary>True for roles that may read everything but change nothing.</summary>
    bool IsReadOnly { get; }

    Task<bool> CanViewReferralAsync(int referralId, CancellationToken cancellationToken = default);

    Task<bool> CanEditReferralAsync(int referralId, CancellationToken cancellationToken = default);

    /// <summary>Accepting is reserved to the destination facility.</summary>
    Task<bool> CanAcceptReferralAsync(int referralId, CancellationToken cancellationToken = default);

    /// <summary>Rejecting is reserved to the destination facility.</summary>
    Task<bool> CanRejectReferralAsync(int referralId, CancellationToken cancellationToken = default);

    /// <summary>Cancelling is reserved to the originating facility.</summary>
    Task<bool> CanCancelReferralAsync(int referralId, CancellationToken cancellationToken = default);

    Task<bool> CanDeleteReferralAsync(int referralId, CancellationToken cancellationToken = default);

    Task<bool> CanModifyStatusAsync(int referralId, CancellationToken cancellationToken = default);

    Task<bool> CanViewAttachmentsAsync(int referralId, CancellationToken cancellationToken = default);

    Task<bool> CanAccessHistoryAsync(int referralId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves an attachment only when its parent referral is in scope.
    /// Authorisation is never decided from the attachment identifier alone.
    /// </summary>
    Task<ReferralAttachment?> GetAuthorisedAttachmentAsync(
        string storedFileName,
        CancellationToken cancellationToken = default);

    /// <summary>Records a denied attempt, then returns so the caller can respond.</summary>
    Task LogDeniedAsync(int referralId, string action, CancellationToken cancellationToken = default);
}
