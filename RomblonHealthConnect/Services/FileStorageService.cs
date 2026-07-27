using Microsoft.AspNetCore.Http;
using RomblonHealthConnect.Interfaces;
using RomblonHealthConnect.Models.Enums;

namespace RomblonHealthConnect.Services;

/// <summary>
/// Stores referral attachments under wwwroot/uploads/referrals.
/// Swap for blob storage in production without touching callers.
/// </summary>
public class FileStorageService : IFileStorageService
{
    private const string UploadFolder = "uploads/referrals";

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

        var targetDirectory = Path.Combine(_environment.WebRootPath, UploadFolder);
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
        // Guard against traversal in case a stored name is ever tampered with.
        var safeName = Path.GetFileName(storedFileName);
        var fullPath = Path.Combine(_environment.WebRootPath, UploadFolder, safeName);

        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    public string GetPublicPath(string storedFileName) => $"/{UploadFolder}/{Path.GetFileName(storedFileName)}";
}
