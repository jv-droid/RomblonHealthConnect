using Microsoft.EntityFrameworkCore;
using RomblonHealthConnect.Constants;
using RomblonHealthConnect.Data;
using RomblonHealthConnect.Interfaces;
using RomblonHealthConnect.Models;

namespace RomblonHealthConnect.Services;

/// <summary>
/// Record-level authorization for referrals.
///
/// Every decision is made from the authenticated principal plus a scoped
/// database query. A hospital user changing the id in the URL simply finds no
/// matching row, because the scope predicate is part of the query rather than a
/// check applied afterwards.
/// </summary>
public class ReferralAuthorizationService : IReferralAuthorizationService
{
    private readonly ApplicationDbContext _context;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditService _audit;
    private readonly ILogger<ReferralAuthorizationService> _logger;

    public ReferralAuthorizationService(
        ApplicationDbContext context,
        ICurrentUserService currentUser,
        IAuditService audit,
        ILogger<ReferralAuthorizationService> logger)
    {
        _context = context;
        _currentUser = currentUser;
        _audit = audit;
        _logger = logger;
    }

    public bool HasProvinceWideScope => _currentUser.HasProvinceWideScope;

    /// <summary>
    /// Executive viewers and auditors may read province-wide but must not change
    /// anything. Checked in addition to the record scope.
    /// </summary>
    public bool IsReadOnly =>
        (_currentUser.IsInRole(Roles.ExecutiveViewer) || _currentUser.IsInRole(Roles.SystemAuditor))
        && !_currentUser.IsInRole(Roles.ProvincialAdministrator)
        && !_currentUser.IsInRole(Roles.PHOAdministrator);

    /* ------------------------------------------------------------------ */
    /* Scope                                                               */
    /* ------------------------------------------------------------------ */

    public IQueryable<Referral> ApplyScope(IQueryable<Referral> query)
    {
        // Unauthenticated requests never reach a referral. Returning an empty
        // set keeps this safe even if middleware were misconfigured.
        if (!_currentUser.IsAuthenticated)
        {
            return query.Where(_ => false);
        }

        // Provincial, PHO, Executive, and Auditor roles see the whole province.
        if (_currentUser.HasProvinceWideScope)
        {
            return query;
        }

        var hospitalId = _currentUser.HospitalId;

        // A hospital-scoped account with no facility assigned has no referrals.
        // Failing closed is deliberate: a misconfigured account sees nothing
        // rather than everything.
        if (!hospitalId.HasValue)
        {
            return query.Where(_ => false);
        }

        var id = hospitalId.Value;

        return query.Where(r => r.OriginHospitalId == id || r.DestinationHospitalId == id);
    }

    /* ------------------------------------------------------------------ */
    /* Record checks                                                       */
    /* ------------------------------------------------------------------ */

    /// <summary>Single scoped existence query; no referral is materialised.</summary>
    private Task<bool> InScopeAsync(int referralId, CancellationToken cancellationToken) =>
        ApplyScope(_context.Referrals.AsNoTracking())
            .AnyAsync(r => r.Id == referralId, cancellationToken);

    public Task<bool> CanViewReferralAsync(int referralId, CancellationToken cancellationToken = default) =>
        InScopeAsync(referralId, cancellationToken);

    public Task<bool> CanViewAttachmentsAsync(int referralId, CancellationToken cancellationToken = default) =>
        InScopeAsync(referralId, cancellationToken);

    public Task<bool> CanAccessHistoryAsync(int referralId, CancellationToken cancellationToken = default) =>
        InScopeAsync(referralId, cancellationToken);

    public async Task<bool> CanEditReferralAsync(int referralId, CancellationToken cancellationToken = default)
    {
        if (IsReadOnly)
        {
            return false;
        }

        return await InScopeAsync(referralId, cancellationToken);
    }

    public Task<bool> CanModifyStatusAsync(int referralId, CancellationToken cancellationToken = default) =>
        CanEditReferralAsync(referralId, cancellationToken);

    public Task<bool> CanDeleteReferralAsync(int referralId, CancellationToken cancellationToken = default) =>
        CanEditReferralAsync(referralId, cancellationToken);

    /// <summary>
    /// Accepting belongs to the receiving facility. Province-wide roles may act
    /// on behalf of either side; a hospital user must be the destination.
    /// </summary>
    public Task<bool> CanAcceptReferralAsync(int referralId, CancellationToken cancellationToken = default) =>
        CanRespondAsDestinationAsync(referralId, cancellationToken);

    public Task<bool> CanRejectReferralAsync(int referralId, CancellationToken cancellationToken = default) =>
        CanRespondAsDestinationAsync(referralId, cancellationToken);

    private async Task<bool> CanRespondAsDestinationAsync(int referralId, CancellationToken cancellationToken)
    {
        if (IsReadOnly)
        {
            return false;
        }

        if (_currentUser.HasProvinceWideScope)
        {
            return await InScopeAsync(referralId, cancellationToken);
        }

        var hospitalId = _currentUser.HospitalId;
        if (!hospitalId.HasValue)
        {
            return false;
        }

        return await _context.Referrals
            .AsNoTracking()
            .AnyAsync(r => r.Id == referralId && r.DestinationHospitalId == hospitalId.Value, cancellationToken);
    }

    /// <summary>Cancelling belongs to the originating facility.</summary>
    public async Task<bool> CanCancelReferralAsync(int referralId, CancellationToken cancellationToken = default)
    {
        if (IsReadOnly)
        {
            return false;
        }

        if (_currentUser.HasProvinceWideScope)
        {
            return await InScopeAsync(referralId, cancellationToken);
        }

        var hospitalId = _currentUser.HospitalId;
        if (!hospitalId.HasValue)
        {
            return false;
        }

        return await _context.Referrals
            .AsNoTracking()
            .AnyAsync(r => r.Id == referralId && r.OriginHospitalId == hospitalId.Value, cancellationToken);
    }

    /* ------------------------------------------------------------------ */
    /* Attachments                                                         */
    /* ------------------------------------------------------------------ */

    public async Task<ReferralAttachment?> GetAuthorisedAttachmentAsync(
        string storedFileName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(storedFileName))
        {
            return null;
        }

        // The scope is applied to the parent referral, so the attachment
        // identifier alone never grants access.
        var authorisedReferralIds = ApplyScope(_context.Referrals.AsNoTracking()).Select(r => r.Id);

        return await _context.ReferralAttachments
            .AsNoTracking()
            .Where(a => a.StoredFileName == storedFileName
                        && authorisedReferralIds.Contains(a.ReferralId))
            .FirstOrDefaultAsync(cancellationToken);
    }

    /* ------------------------------------------------------------------ */
    /* Denial logging                                                      */
    /* ------------------------------------------------------------------ */

    public async Task LogDeniedAsync(int referralId, string action, CancellationToken cancellationToken = default)
    {
        var description =
            $"Denied '{action}' on referral {referralId} for user '{_currentUser.UserName ?? "anonymous"}' " +
            $"(hospital {_currentUser.HospitalId?.ToString() ?? "none"}).";

        _logger.LogWarning(
            "Referral access denied. User={UserId} Hospital={HospitalId} Referral={ReferralId} Action={Action} Ip={Ip}",
            _currentUser.UserId, _currentUser.HospitalId, referralId, action, _currentUser.IpAddress);

        // UserId, display name, IP, and user agent are stamped by the audit service.
        await _audit.LogAsync(
            AuditActions.AccessDenied,
            nameof(Referral),
            referralId.ToString(),
            description,
            newValues: new
            {
                requestedAction = action,
                referralId,
                userHospitalId = _currentUser.HospitalId,
                outcome = "Denied"
            },
            cancellationToken: cancellationToken);
    }
}
