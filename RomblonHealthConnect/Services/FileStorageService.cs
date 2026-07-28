using Microsoft.AspNetCore.Http;
using RomblonHealthConnect.Interfaces;
using RomblonHealthConnect.Models.Enums;

namespace RomblonHealthConnect.Services;

/// <summary>
/// Stores referral attachments under the content root, deliberately OUTSIDE
/// wwwroot.
///
/// Anything inside wwwroot is served by the static-file middleware, which runs
/// before authentication. Clinical documents kept there were downloadable by
/// anyone who knew the file name. They now live in a private directory and are
/// only reachable through ReferralsController.Attachment, which authorises the
/// parent referral first.
/// </summary>
public class FileStorageService : IFileStorageService
{
    /// <summary>Relative to ContentRootPath, never to WebRootPath.</summary>
    private const string UploadFolder = "App_Data/uploads/referrals";

    /// <summary>Content types accepted, keyed by extension. Both must agree for an upload to pass.</summary>
    private static readonly Dictionary<string, string[]> PermittedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = ["application/pdf"],
        [".jpg"] = ["image/jpeg"],
        [".jpeg"] = ["image/jpeg"],
        [".png"] = ["image/png"],
        [".docx"] = ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"]
    };

    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<FileStorageService> _logger;

    public FileStorageService(IWebHostEnvironment environment, ILogger<FileStorageService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public IReadOnlyCollection<string> AllowedExtensions => PermittedTypes.Keys;

    public long MaxFileSizeBytes => 10 * 1024 * 1024;

    private string StorageRoot => Path.Combine(_environment.ContentRootPath, UploadFolder);

    public async Task<FileStorageResult> SaveAsync(
        IFormFile file,
        AttachmentCategory category,
        CancellationToken cancellationToken = default)
    {
        if (file.Length == 0)
        {
            return new FileStorageResult(false, null, null, 0, "The file is empty.");
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return new FileStorageResult(false, null, null, 0,
                $"\"{file.FileName}\" exceeds the {MaxFileSizeBytes / (1024 * 1024)} MB limit.");
        }

        var extension = Path.GetExtension(file.FileName);

        if (string.IsNullOrWhiteSpace(extension) || !PermittedTypes.TryGetValue(extension, out var allowedTypes))
        {
            return new FileStorageResult(false, null, null, 0,
                $"\"{file.FileName}\" is not an accepted file type. Allowed: PDF, JPEG, PNG, DOCX.");
        }

        // Reject a mismatch between the extension and the declared content type.
        if (!allowedTypes.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
        {
            return new FileStorageResult(false, null, null, 0,
                $"\"{file.FileName}\" does not match its file type.");
        }

        var targetDirectory = StorageRoot;
        Directory.CreateDirectory(targetDirectory);

        // Never build the stored name from user input.
        var storedFileName = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
        var fullPath = Path.Combine(targetDirectory, storedFileName);

        try
        {
            await using var stream = new FileStream(fullPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await file.CopyToAsync(stream, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store attachment {FileName}.", file.FileName);
            return new FileStorageResult(false, null, null, 0, $"\"{file.FileName}\" could not be saved.");
        }

        return new FileStorageResult(true, storedFileName, file.ContentType, file.Length, null);
    }

    public Task DeleteAsync(string storedFileName, CancellationToken cancellationToken = default)
    {
        var fullPath = ResolvePath(storedFileName);

        if (fullPath is not null && File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Absolute path of a stored file, or null when the name escapes the storage
    /// directory. Callers must treat null as "not found" rather than probing.
    /// </summary>
    public string? ResolvePath(string storedFileName)
    {
        if (string.IsNullOrWhiteSpace(storedFileName))
        {
            return null;
        }

        // Strip any directory component before combining, then confirm the
        // result really sits inside the storage root.
        var safeName = Path.GetFileName(storedFileName);
        var root = Path.GetFullPath(StorageRoot);
        var candidate = Path.GetFullPath(Path.Combine(root, safeName));

        if (!candidate.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Rejected attachment path outside the storage root: {Name}", storedFileName);
            return null;
        }

        return candidate;
    }

    /// <summary>
    /// Route to the authorised download action. Attachments are no longer
    /// addressable as static files.
    /// </summary>
    public string GetPublicPath(string storedFileName) => $"/Referrals/Attachment/{storedFileName}";
}
