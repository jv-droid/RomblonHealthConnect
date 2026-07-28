using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RomblonHealthConnect.Constants;
using RomblonHealthConnect.Data;
using RomblonHealthConnect.Interfaces;
using RomblonHealthConnect.Models;
using RomblonHealthConnect.Models.Enums;
using RomblonHealthConnect.Services;

namespace RomblonHealthConnect.Tests;

/// <summary>
/// Record-level authorization for the referral module.
///
/// Hospital A = 1, Hospital B = 2. Referral 15 belongs to A, referral 18 to B,
/// mirroring the IDOR described in the security review.
/// </summary>
public class ReferralAuthorizationTests
{
    private const int HospitalA = 1;
    private const int HospitalB = 2;
    private const int ReferralOfA = 15;
    private const int ReferralOfB = 18;
    private const int ReferralAtoB = 20;

    /* -- test doubles ---------------------------------------------------- */

    /// <summary>Stands in for the signed-in principal; nothing reads the request.</summary>
    private sealed class FakeCurrentUser : ICurrentUserService
    {
        public bool IsAuthenticated { get; init; } = true;
        public string? UserId { get; init; } = "user-1";
        public string? UserName { get; init; } = "test.user";
        public string? DisplayName => UserName;
        public string? PositionTitle => null;
        public int? HospitalId { get; init; }
        public IReadOnlyList<string> Roles { get; init; } = [];
        public bool IsInRole(string role) => Roles.Contains(role);
        public bool HasProvinceWideScope => Constants.Roles.ProvinceWide.Any(IsInRole);
        public bool CanAccessHospital(int hospitalId) =>
            HasProvinceWideScope || (HospitalId.HasValue && HospitalId.Value == hospitalId);
        public string? IpAddress => "127.0.0.1";
        public string? UserAgent => "tests";
    }

    private sealed class NoOpAudit : IAuditService
    {
        public int Entries { get; private set; }

        public Task LogAsync(string action, string entityName, string? entityId, string description,
            object? oldValues = null, object? newValues = null, CancellationToken cancellationToken = default)
        {
            Entries++;
            return Task.CompletedTask;
        }
    }

    /* -- fixtures -------------------------------------------------------- */

    private static ApplicationDbContext NewContext(string name)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name)
            .Options;

        var context = new ApplicationDbContext(options);
        Seed(context);
        return context;
    }

    private static void Seed(ApplicationDbContext context)
    {
        if (context.Referrals.Any())
        {
            return;
        }

        context.Referrals.AddRange(
            NewReferral(ReferralOfA, HospitalA, HospitalA),   // wholly inside A
            NewReferral(ReferralOfB, HospitalB, HospitalB),   // wholly inside B
            NewReferral(ReferralAtoB, HospitalA, HospitalB)); // A -> B, visible to both

        context.ReferralAttachments.AddRange(
            new ReferralAttachment
            {
                Id = 100, ReferralId = ReferralOfA, FileName = "a-labs.pdf",
                StoredFileName = "aaaa1111.pdf", ContentType = "application/pdf",
                FileSizeBytes = 10, UploadedAtUtc = DateTime.UtcNow, UploadedBy = "seed"
            },
            new ReferralAttachment
            {
                Id = 200, ReferralId = ReferralOfB, FileName = "b-labs.pdf",
                StoredFileName = "bbbb2222.pdf", ContentType = "application/pdf",
                FileSizeBytes = 10, UploadedAtUtc = DateTime.UtcNow, UploadedBy = "seed"
            });

        context.SaveChanges();
    }

    private static Referral NewReferral(int id, int origin, int destination) => new()
    {
        Id = id,
        ReferralNumber = $"RF-2026-{id:D4}",
        PatientId = 1,
        OriginHospitalId = origin,
        DestinationHospitalId = destination,
        RequestedSpecializationId = 1,
        Status = ReferralStatus.Submitted,
        Priority = ReferralPriority.Routine,
        ReasonForReferral = "Test referral",
        CreatedUtc = DateTime.UtcNow
    };

    private static ReferralAuthorizationService NewService(
        ApplicationDbContext context, ICurrentUserService user, IAuditService? audit = null) =>
        new(context, user, audit ?? new NoOpAudit(),
            NullLogger<ReferralAuthorizationService>.Instance);

    private static FakeCurrentUser HospitalUser(int hospitalId, string role) =>
        new() { HospitalId = hospitalId, Roles = [role] };

    /* -- the reported IDOR ------------------------------------------------ */

    [Fact]
    public async Task HospitalA_CannotViewHospitalB_Referral()
    {
        await using var context = NewContext(nameof(HospitalA_CannotViewHospitalB_Referral));
        var service = NewService(context, HospitalUser(HospitalA, Roles.HospitalAdministrator));

        Assert.True(await service.CanViewReferralAsync(ReferralOfA));
        Assert.False(await service.CanViewReferralAsync(ReferralOfB));
    }

    [Fact]
    public async Task HospitalB_CannotEditHospitalA_Referral()
    {
        await using var context = NewContext(nameof(HospitalB_CannotEditHospitalA_Referral));
        var service = NewService(context, HospitalUser(HospitalB, Roles.ReferralCoordinator));

        Assert.False(await service.CanEditReferralAsync(ReferralOfA));
        Assert.False(await service.CanModifyStatusAsync(ReferralOfA));
        Assert.False(await service.CanDeleteReferralAsync(ReferralOfA));
    }

    [Fact]
    public async Task Scope_ExcludesOtherHospitalsReferrals_InTheQueryItself()
    {
        await using var context = NewContext(nameof(Scope_ExcludesOtherHospitalsReferrals_InTheQueryItself));
        var service = NewService(context, HospitalUser(HospitalA, Roles.HospitalAdministrator));

        var visible = await service.ApplyScope(context.Referrals.AsNoTracking())
            .Select(r => r.Id)
            .ToListAsync();

        Assert.Contains(ReferralOfA, visible);
        Assert.Contains(ReferralAtoB, visible);   // A is the origin
        Assert.DoesNotContain(ReferralOfB, visible);
    }

    /* -- province-wide roles ---------------------------------------------- */

    [Theory]
    [InlineData(Roles.ProvincialAdministrator)]
    [InlineData(Roles.PHOAdministrator)]
    public async Task ProvinceWideAdministrators_CanAccessEveryReferral(string role)
    {
        await using var context = NewContext("province-" + role);
        var service = NewService(context, new FakeCurrentUser { HospitalId = null, Roles = [role] });

        Assert.True(await service.CanViewReferralAsync(ReferralOfA));
        Assert.True(await service.CanViewReferralAsync(ReferralOfB));
        Assert.True(await service.CanEditReferralAsync(ReferralOfB));
    }

    [Fact]
    public async Task ExecutiveViewer_CanReadEverything_ButCannotModify()
    {
        await using var context = NewContext(nameof(ExecutiveViewer_CanReadEverything_ButCannotModify));
        var service = NewService(context,
            new FakeCurrentUser { HospitalId = null, Roles = [Roles.ExecutiveViewer] });

        Assert.True(service.IsReadOnly);
        Assert.True(await service.CanViewReferralAsync(ReferralOfA));
        Assert.True(await service.CanViewReferralAsync(ReferralOfB));

        Assert.False(await service.CanEditReferralAsync(ReferralOfA));
        Assert.False(await service.CanAcceptReferralAsync(ReferralOfA));
        Assert.False(await service.CanRejectReferralAsync(ReferralOfA));
        Assert.False(await service.CanCancelReferralAsync(ReferralOfA));
        Assert.False(await service.CanModifyStatusAsync(ReferralOfA));
    }

    [Fact]
    public async Task SystemAuditor_IsReadOnlyAcrossTheProvince()
    {
        await using var context = NewContext(nameof(SystemAuditor_IsReadOnlyAcrossTheProvince));
        var service = NewService(context,
            new FakeCurrentUser { HospitalId = null, Roles = [Roles.SystemAuditor] });

        Assert.True(await service.CanViewReferralAsync(ReferralOfB));
        Assert.True(await service.CanAccessHistoryAsync(ReferralOfB));
        Assert.False(await service.CanEditReferralAsync(ReferralOfB));
    }

    /* -- direction-specific actions ---------------------------------------- */

    [Fact]
    public async Task OnlyDestinationFacility_MayAcceptOrReject()
    {
        await using var context = NewContext(nameof(OnlyDestinationFacility_MayAcceptOrReject));

        var origin = NewService(context, HospitalUser(HospitalA, Roles.ReferralCoordinator));
        var destination = NewService(context, HospitalUser(HospitalB, Roles.ReferralCoordinator));

        // Referral 20 runs A -> B.
        Assert.False(await origin.CanAcceptReferralAsync(ReferralAtoB));
        Assert.False(await origin.CanRejectReferralAsync(ReferralAtoB));

        Assert.True(await destination.CanAcceptReferralAsync(ReferralAtoB));
        Assert.True(await destination.CanRejectReferralAsync(ReferralAtoB));
    }

    [Fact]
    public async Task OnlyOriginFacility_MayCancel()
    {
        await using var context = NewContext(nameof(OnlyOriginFacility_MayCancel));

        var origin = NewService(context, HospitalUser(HospitalA, Roles.ReferralCoordinator));
        var destination = NewService(context, HospitalUser(HospitalB, Roles.ReferralCoordinator));

        Assert.True(await origin.CanCancelReferralAsync(ReferralAtoB));
        Assert.False(await destination.CanCancelReferralAsync(ReferralAtoB));
    }

    /* -- doctors and nurses ------------------------------------------------ */

    [Fact]
    public async Task Doctor_SeesOnlyTheirOwnHospitalsReferrals()
    {
        await using var context = NewContext(nameof(Doctor_SeesOnlyTheirOwnHospitalsReferrals));
        var service = NewService(context, HospitalUser(HospitalB, Roles.Doctor));

        Assert.True(await service.CanViewReferralAsync(ReferralOfB));
        Assert.True(await service.CanViewReferralAsync(ReferralAtoB));
        Assert.False(await service.CanViewReferralAsync(ReferralOfA));
    }

    [Fact]
    public async Task Nurse_IsScopedLikeADoctor()
    {
        await using var context = NewContext(nameof(Nurse_IsScopedLikeADoctor));
        var service = NewService(context, HospitalUser(HospitalA, Roles.Nurse));

        Assert.True(await service.CanViewReferralAsync(ReferralOfA));
        Assert.False(await service.CanViewReferralAsync(ReferralOfB));
    }

    [Fact]
    public async Task RecordsOfficer_IsConfinedToTheirHospital()
    {
        await using var context = NewContext(nameof(RecordsOfficer_IsConfinedToTheirHospital));
        var service = NewService(context, HospitalUser(HospitalA, Roles.RecordsOfficer));

        Assert.True(await service.CanViewReferralAsync(ReferralOfA));
        Assert.False(await service.CanViewReferralAsync(ReferralOfB));
    }

    /* -- fail closed -------------------------------------------------------- */

    [Fact]
    public async Task UnauthenticatedPrincipal_SeesNothing()
    {
        await using var context = NewContext(nameof(UnauthenticatedPrincipal_SeesNothing));
        var service = NewService(context, new FakeCurrentUser { IsAuthenticated = false, HospitalId = HospitalA });

        Assert.Empty(await service.ApplyScope(context.Referrals.AsNoTracking()).ToListAsync());
        Assert.False(await service.CanViewReferralAsync(ReferralOfA));
    }

    [Fact]
    public async Task HospitalRoleWithNoAssignedFacility_SeesNothing()
    {
        await using var context = NewContext(nameof(HospitalRoleWithNoAssignedFacility_SeesNothing));
        var service = NewService(context,
            new FakeCurrentUser { HospitalId = null, Roles = [Roles.ReferralCoordinator] });

        Assert.Empty(await service.ApplyScope(context.Referrals.AsNoTracking()).ToListAsync());
        Assert.False(await service.CanViewReferralAsync(ReferralOfA));
    }

    /* -- attachments -------------------------------------------------------- */

    [Fact]
    public async Task Attachment_IsAuthorisedThroughItsParentReferral()
    {
        await using var context = NewContext(nameof(Attachment_IsAuthorisedThroughItsParentReferral));
        var service = NewService(context, HospitalUser(HospitalA, Roles.HospitalAdministrator));

        var own = await service.GetAuthorisedAttachmentAsync("aaaa1111.pdf");
        var other = await service.GetAuthorisedAttachmentAsync("bbbb2222.pdf");

        Assert.NotNull(own);
        Assert.Equal(ReferralOfA, own!.ReferralId);

        // Knowing the stored file name is not enough.
        Assert.Null(other);
    }

    [Fact]
    public async Task Attachment_UnknownFileName_ReturnsNull()
    {
        await using var context = NewContext(nameof(Attachment_UnknownFileName_ReturnsNull));
        var service = NewService(context, HospitalUser(HospitalA, Roles.HospitalAdministrator));

        Assert.Null(await service.GetAuthorisedAttachmentAsync("does-not-exist.pdf"));
        Assert.Null(await service.GetAuthorisedAttachmentAsync(string.Empty));
    }

    /* -- history ------------------------------------------------------------ */

    [Fact]
    public async Task History_FollowsTheSameScopeAsTheReferral()
    {
        await using var context = NewContext(nameof(History_FollowsTheSameScopeAsTheReferral));
        var service = NewService(context, HospitalUser(HospitalA, Roles.HospitalAdministrator));

        Assert.True(await service.CanAccessHistoryAsync(ReferralOfA));
        Assert.False(await service.CanAccessHistoryAsync(ReferralOfB));
    }

    /* -- denial logging ------------------------------------------------------ */

    [Fact]
    public async Task DeniedAccess_WritesAnAuditEntry()
    {
        await using var context = NewContext(nameof(DeniedAccess_WritesAnAuditEntry));
        var audit = new NoOpAudit();
        var service = NewService(context, HospitalUser(HospitalA, Roles.HospitalAdministrator), audit);

        await service.LogDeniedAsync(ReferralOfB, "Details");

        Assert.Equal(1, audit.Entries);
    }
}
