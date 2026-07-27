using RomblonHealthConnect.Models.Enums;

namespace RomblonHealthConnect.Models;

/// <summary>
/// A laboratory result, image, or document supporting a referral.
/// The file itself lives on disk; only metadata is stored here.
/// </summary>
public class ReferralAttachment
{
    public int Id { get; set; }

    public int ReferralId { get; set; }

    public Referral Referral { get; set; } = null!;

    /// <summary>Original name as uploaded, shown to users.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Generated name on disk. Never derived from user input.</summary>
    public string StoredFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public AttachmentCategory Category { get; set; }

    public string UploadedBy { get; set; } = "System";

    public DateTime UploadedAtUtc { get; set; }

    /// <summary>Images can be previewed inline; other types show a file card.</summary>
    public bool IsPreviewable => ContentType is "image/jpeg" or "image/png";

    public string Extension => Path.GetExtension(FileName).TrimStart('.').ToUpperInvariant();

    /// <summary>Size rendered for display, for example "1.4 MB".</summary>
    public string DisplaySize => FileSizeBytes switch
    {
        < 1024 => $"{FileSizeBytes} B",
        < 1024 * 1024 => $"{FileSizeBytes / 1024.0:0.#} KB",
        _ => $"{FileSizeBytes / (1024.0 * 1024.0):0.#} MB"
    };
}
