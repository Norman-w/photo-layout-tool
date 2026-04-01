using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace PhotoLayout.Api.Services;

/// <summary>
/// 一寸照常用像素（约 300DPI，25×35mm）。若 ONNX 显著性抠图可用则先蒙版铺白底，再缩放；否则白边 Pad。
/// </summary>
public class ImageProcessingService(OnnxMattingService onnx)
{
    public const int OneInchWidthPx = 295;
    public const int OneInchHeightPx = 413;

    public async Task<byte[]> CreateWhiteBackgroundOneInchAsync(Stream sourceStream, CancellationToken ct = default)
        => await CreateBackgroundOneInchAsync(sourceStream, new Rgba32(255, 255, 255), ct);

    public async Task<byte[]> CreateBackgroundOneInchAsync(
        Stream sourceStream,
        Rgba32 bgColor,
        CancellationToken ct = default)
    {
        using var loaded = await Image.LoadAsync<Rgba32>(sourceStream, ct);

        Image<Rgba32>? onnxImg = null;
        try
        {
            onnxImg = onnx.TryCompositeOnColor(loaded, bgColor);
            using var toOneInch = onnxImg ?? loaded.Clone();
            toOneInch.Mutate(ctx =>
            {
                ctx.Resize(new ResizeOptions
                {
                    Size = new Size(OneInchWidthPx, OneInchHeightPx),
                    Mode = ResizeMode.Pad,
                    PadColor = Color.FromPixel(bgColor),
                    Position = AnchorPositionMode.Top,
                });
            });

            await using var ms = new MemoryStream();
            await toOneInch.SaveAsPngAsync(ms, ct);
            return ms.ToArray();
        }
        finally
        {
            onnxImg?.Dispose();
        }
    }

    public async Task<byte[]> CreateBackgroundSameSizeAsync(
        Stream sourceStream,
        Rgba32 bgColor,
        CancellationToken ct = default,
        float? foregroundThreshold = null,
        float? edgeSoftness = null)
    {
        using var loaded = await Image.LoadAsync<Rgba32>(sourceStream, ct);
        Image<Rgba32>? onnxImg = null;
        try
        {
            onnxImg = onnx.TryCompositeOnColor(loaded, bgColor, foregroundThreshold, edgeSoftness);
            using var output = onnxImg ?? loaded.Clone();
            await using var ms = new MemoryStream();
            await output.SaveAsPngAsync(ms, ct);
            return ms.ToArray();
        }
        finally
        {
            onnxImg?.Dispose();
        }
    }
}
