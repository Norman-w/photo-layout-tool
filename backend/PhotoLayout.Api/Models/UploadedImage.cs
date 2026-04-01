namespace PhotoLayout.Api.Models;

public class UploadedImage
{
    public Guid Id { get; set; }
    public string OriginalFileName { get; set; } = "";
    public string ContentType { get; set; } = "image/jpeg";
    /// <summary>Relative to storage root, e.g. {id}/original.jpg</summary>
    public string OriginalRelativePath { get; set; } = "";
    public string? WhiteBackgroundRelativePath { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
