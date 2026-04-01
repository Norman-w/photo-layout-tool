using Microsoft.AspNetCore.Http;

namespace PhotoLayout.Api.Models;

/// <summary>multipart 换底请求；单 DTO 绑定比多个 [FromForm] 参数更稳定。</summary>
public sealed class MatteUploadForm
{
    public IFormFile File { get; set; } = null!;
    public string? BgColor { get; set; }
    public string? ForegroundThreshold { get; set; }
    public string? EdgeSoftness { get; set; }
    public string? EdgeColorPull { get; set; }
}
