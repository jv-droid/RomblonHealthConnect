using Microsoft.AspNetCore.Http;
using RomblonHealthConnect.Models;
using RomblonHealthConnect.Models.Enums;
using RomblonHealthConnect.ViewModels.Referrals;

namespace RomblonHealthConnect.Interfaces;

/// <summary>
/// Orchestrates the referral workflow: creation, submission, and every state transition.
/// All transitions write a history entry and raise notifications.
/// </summary>
public interface IReferralService
{
    Task<ReferralDashboardViewModel> GetDashboardAsync(
        ReferralFilter filter,
        CancellationToken cancellationToken = default);

    Task<PagedResult<ReferralListItemViewModel>> GetListAsync(
        ReferralFilter filter,
        CancellationToken cancellationToken = default);

    Task<ReferralDetailsViewModel?> GetDetailsAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>Creates a referral. Saved as Draft, or Submitted when <paramref name="submit"/> is true.</summary>
    Task<ReferralOperationResult> CreateAsync(
        CreateReferralViewModel model,
        bool submit,
        CancellationToken cancellationToken = default);

    Task<ReferralOperationResult> SubmitAsync(int referralId, CancellationToken cancellationToken = default);

    Task<ReferralOperationResult> AcceptAsync(
        int referralId,
        int? assignedDoctorId,
        string? notes,
        CancellationToken cancellationToken = default);

    Task<ReferralOperationResult> RejectAsync(
        int referralId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<ReferralOperationResult> RequestInformationAsync(
        int referralId,
        string question,
        CancellationToken cancellationToken = default);

    Task<ReferralOperationResult> CompleteAsync(
        int referralId,
        string? notes,
        CancellationToken cancellationToken = default);

    Task<ReferralOperationResult> CancelAsync(
        int referralId,
        string reason,
        CancellationToken cancellationToken = default);

    /// <summary>Capability snapshot shown when a destination hospital is picked in the wizard.</summary>
    Task<HospitalCapabilityViewModel?> GetHospitalCapabilityAsync(
        int hospitalId,
        int? specializationId = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Persists notifications and pushes them to connected clients.
/// </summary>
public interface INotificationService
{
    Task CreateAsync(
        int hospitalId,
        NotificationType type,
        string title,
        string message,
        int? referralId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Notification>> GetRecentAsync(
        int hospitalId,
        int take = 20,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(int hospitalId, CancellationToken cancellationToken = default);

    Task MarkAsReadAsync(int notificationId, CancellationToken cancellationToken = default);

    Task MarkAllAsReadAsync(int hospitalId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Validates and stores referral attachments on the local file system.
/// </summary>
public interface IFileStorageService
{
    /// <summary>Extensions accepted by the upload control.</summary>
    IReadOnlyCollection<string> AllowedExtensions { get; }

    long MaxFileSizeBytes { get; }

    /// <summary>Validates then writes the file, returning attachment metadata.</summary>
    Task<FileStorageResult> SaveAsync(
        IFormFile file,
        AttachmentCategory category,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(string storedFileName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Absolute path of a stored file, or null when the name would escape the
    /// storage directory. Never pass a caller-supplied path straight to the file system.
    /// </summary>
    string? ResolvePath(string storedFileName);

    /// <summary>Route to the authorised download action, not a static file URL.</summary>
    string GetPublicPath(string storedFileName);
}

/// <summary>Outcome of a single file upload attempt.</summary>
public record FileStorageResult(
    bool Success,
    string? StoredFileName,
    string? ContentType,
    long FileSizeBytes,
    string? Error);
