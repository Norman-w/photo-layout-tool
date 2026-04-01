using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PhotoLayout.Api.Data;
using PhotoLayout.Api.Models;
using PhotoLayout.Api.Services;

namespace PhotoLayout.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ImagesController(
    AppDbContext db,
    ImageProcessingService imageProcessing,
    IWebHostEnvironment env,
    ILogger<ImagesController> log) : ControllerBase
{
    private string StorageRoot => Path.Combine(env.ContentRootPath, "storage");

    private static bool TryParseHexColor(string? hex, out SixLabors.ImageSharp.PixelFormats.Rgba32 color)
    {
        color = new SixLabors.ImageSharp.PixelFormats.Rgba32(255, 255, 255);
        if (string.IsNullOrWhiteSpace(hex))
            return true;
        var s = hex.Trim().TrimStart('#');
        if (s.Length != 6)
            return false;
        if (!byte.TryParse(s.AsSpan(0, 2), System.Globalization.NumberStyles.HexNumber, null, out var r))
            return false;
        if (!byte.TryParse(s.AsSpan(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var g))
            return false;
        if (!byte.TryParse(s.AsSpan(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var b))
            return false;
        color = new SixLabors.ImageSharp.PixelFormats.Rgba32(r, g, b);
        return true;
    }

    [HttpPost("upload")]
    [RequestSizeLimit(52_428_800)] // 50MB
    public async Task<ActionResult<UploadResponse>> Upload(IFormFile file, CancellationToken ct)
    {
        if (file.Length == 0)
            return BadRequest("空文件");

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext) || ext.Length > 10)
            ext = ".jpg";

        var id = Guid.NewGuid();
        Directory.CreateDirectory(Path.Combine(StorageRoot, id.ToString()));

        var originalName = $"original{ext}";
        var originalPhysical = Path.Combine(StorageRoot, id.ToString(), originalName);
        await using (var fs = System.IO.File.Create(originalPhysical))
            await file.CopyToAsync(fs, ct);

        var relativeOriginal = $"{id}/{originalName}";

        byte[] whitePng;
        await using (var readStream = System.IO.File.OpenRead(originalPhysical))
            whitePng = await imageProcessing.CreateWhiteBackgroundOneInchAsync(readStream, ct);

        var whiteName = "white.png";
        var whitePhysical = Path.Combine(StorageRoot, id.ToString(), whiteName);
        await System.IO.File.WriteAllBytesAsync(whitePhysical, whitePng, ct);
        var relativeWhite = $"{id}/{whiteName}";

        var entity = new UploadedImage
        {
            Id = id,
            OriginalFileName = file.FileName,
            ContentType = file.ContentType ?? "application/octet-stream",
            OriginalRelativePath = relativeOriginal,
            WhiteBackgroundRelativePath = relativeWhite,
            CreatedAt = DateTimeOffset.UtcNow
        };
        db.UploadedImages.Add(entity);
        await db.SaveChangesAsync(ct);

        log.LogInformation("Uploaded image {Id}", id);

        // 相对路径：经前端同源或 Vite 代理访问，避免打印预览跨域与主机写死问题
        return Ok(new UploadResponse(
            id,
            $"/api/images/{id}/original",
            $"/api/images/{id}/white"));
    }

    [HttpGet("{id:guid}/original")]
    public async Task<IActionResult> GetOriginal(Guid id, CancellationToken ct)
    {
        var row = await db.UploadedImages.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null) return NotFound();
        var path = Path.Combine(StorageRoot, row.OriginalRelativePath);
        if (!System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path, row.ContentType, enableRangeProcessing: true);
    }

    [HttpGet("{id:guid}/white")]
    public async Task<IActionResult> GetWhite(Guid id, CancellationToken ct)
    {
        var row = await db.UploadedImages.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (row is null || row.WhiteBackgroundRelativePath is null) return NotFound();
        var path = Path.Combine(StorageRoot, row.WhiteBackgroundRelativePath);
        if (!System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path, "image/png", enableRangeProcessing: true);
    }

    [HttpPost("matte")]
    [RequestSizeLimit(52_428_800)]
    public async Task<ActionResult<MatteResponse>> Matte(
        IFormFile file,
        [FromForm] string? bgColor,
        [FromForm] string? foregroundThreshold,
        [FromForm] string? edgeSoftness,
        CancellationToken ct)
    {
        if (file.Length == 0)
            return BadRequest("空文件");
        if (!TryParseHexColor(bgColor, out var parsed))
            return BadRequest("bgColor 应为 #RRGGBB");

        float? fg = null;
        float? es = null;
        if (!string.IsNullOrWhiteSpace(foregroundThreshold)
            && float.TryParse(foregroundThreshold, NumberStyles.Float, CultureInfo.InvariantCulture, out var fgv))
            fg = fgv;
        if (!string.IsNullOrWhiteSpace(edgeSoftness)
            && float.TryParse(edgeSoftness, NumberStyles.Float, CultureInfo.InvariantCulture, out var esv))
            es = esv;

        var id = Guid.NewGuid();
        var dir = Path.Combine(StorageRoot, id.ToString());
        Directory.CreateDirectory(dir);

        await using var read = file.OpenReadStream();
        // matte 接口用于预览“裁剪后换底色”，保持原尺寸，保证与裁剪预览一致
        var png = await imageProcessing.CreateBackgroundSameSizeAsync(read, parsed, ct, fg, es);
        var mattePath = Path.Combine(dir, "matte.png");
        await System.IO.File.WriteAllBytesAsync(mattePath, png, ct);

        return Ok(new MatteResponse($"/api/images/{id}/matte"));
    }

    [HttpGet("{id:guid}/matte")]
    public IActionResult GetMatte(Guid id)
    {
        var path = Path.Combine(StorageRoot, id.ToString(), "matte.png");
        if (!System.IO.File.Exists(path)) return NotFound();
        return PhysicalFile(path, "image/png", enableRangeProcessing: true);
    }
}

public record UploadResponse(Guid Id, string OriginalUrl, string WhiteBackgroundUrl);
public record MatteResponse(string Url);
