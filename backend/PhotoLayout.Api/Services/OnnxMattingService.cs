using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using Microsoft.Extensions.Options;
using PhotoLayout.Api.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace PhotoLayout.Api.Services;

/// <summary>
/// 通用显著性抠图：默认使用 rembg 同款 <c>u2net.onnx</c>（任意背景→前景蒙版→白底）。
/// 亦兼容 <c>u2net_human_seg.onnx</c>（仅人像），预处理与输出形状一致。
/// </summary>
public sealed class OnnxMattingService : IDisposable
{
    private readonly ILogger<OnnxMattingService> _log;
    private readonly IOptions<MattingOptions> _options;
    private readonly InferenceSession? _session;
    private readonly string _inputName;

    public bool IsAvailable => _session != null;

    public OnnxMattingService(
        IWebHostEnvironment env,
        IOptions<MattingOptions> options,
        ILogger<OnnxMattingService> log)
    {
        _log = log;
        _options = options;
        var opt = options.Value;
        if (!opt.Enabled)
        {
            _inputName = "";
            return;
        }

        var path = Path.IsPathRooted(opt.OnnxModelPath)
            ? opt.OnnxModelPath
            : Path.Combine(env.ContentRootPath, opt.OnnxModelPath);

        if (!File.Exists(path))
        {
            _log.LogWarning("ONNX 模型不存在，跳过抠图：{Path}", path);
            _inputName = "";
            return;
        }

        try
        {
            var so = new Microsoft.ML.OnnxRuntime.SessionOptions
            {
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                InterOpNumThreads = 1,
                IntraOpNumThreads = Environment.ProcessorCount,
            };
            _session = new InferenceSession(path, so);
            _inputName = _session.InputMetadata.Keys.First();
            _log.LogInformation("已加载 ONNX 抠图模型：{Path}", path);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "加载 ONNX 失败：{Path}", path);
            _session?.Dispose();
            _inputName = "";
        }
    }

    /// <summary>蒙版前景 + 指定底色；失败返回 null（由上层回退 Pad）。</summary>
    /// <param name="foregroundThresholdOverride">可选，覆盖配置的前景阈值（抑制残留背景）。</param>
    /// <param name="edgeSoftnessOverride">可选，覆盖配置的软边宽度。</param>
    public Image<Rgba32>? TryCompositeOnColor(
        Image<Rgba32> source,
        Rgba32 bgColor,
        float? foregroundThresholdOverride = null,
        float? edgeSoftnessOverride = null,
        float? edgeColorPullOverride = null)
    {
        if (_session is null)
            return null;

        var ow = source.Width;
        var oh = source.Height;
        if (ow < 2 || oh < 2)
            return null;

        const int tw = 320;
        const int th = 320;

        using var small = source.Clone(ctx => ctx.Resize(tw, th, KnownResamplers.Lanczos3));

        var tensorData = new float[1 * 3 * th * tw];
        float gmax = 1e-6f;
        small.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < th; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < tw; x++)
                {
                    var p = row[x];
                    gmax = Math.Max(gmax, p.R);
                    gmax = Math.Max(gmax, p.G);
                    gmax = Math.Max(gmax, p.B);
                }
            }
        });

        small.ProcessPixelRows(accessor =>
        {
            for (var y = 0; y < th; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (var x = 0; x < tw; x++)
                {
                    var p = row[x];
                    var rf = p.R / gmax;
                    var gf = p.G / gmax;
                    var bf = p.B / gmax;
                    var c0 = (rf - 0.485f) / 0.229f;
                    var c1 = (gf - 0.456f) / 0.224f;
                    var c2 = (bf - 0.406f) / 0.225f;
                    tensorData[0 * 3 * th * tw + 0 * th * tw + y * tw + x] = c0;
                    tensorData[0 * 3 * th * tw + 1 * th * tw + y * tw + x] = c1;
                    tensorData[0 * 3 * th * tw + 2 * th * tw + y * tw + x] = c2;
                }
            }
        });

        var inputTensor = new DenseTensor<float>(tensorData, new[] { 1, 3, th, tw });
        var input = NamedOnnxValue.CreateFromTensor(_inputName, inputTensor);

        using var outputs = _session.Run(new List<NamedOnnxValue> { input });
        var first = outputs.First();
        var pred = first.AsTensor<float>();
        var predDims = pred.Dimensions.ToArray();
        if (predDims.Length < 2)
            return null;

        int ph, pw, planeOffset;
        if (predDims.Length == 4)
        {
            ph = (int)predDims[2];
            pw = (int)predDims[3];
            planeOffset = 0;
        }
        else if (predDims.Length == 3)
        {
            ph = (int)predDims[1];
            pw = (int)predDims[2];
            planeOffset = 0;
        }
        else
            return null;

        var predSpan = pred.ToArray();
        float pmin = float.MaxValue, pmax = float.MinValue;
        for (var i = planeOffset; i < predSpan.Length; i++)
        {
            var v = predSpan[i];
            pmin = Math.Min(pmin, v);
            pmax = Math.Max(pmax, v);
        }

        var denom = pmax - pmin;
        if (denom < 1e-8f)
            denom = 1f;

        // 与 rembg u2net 一致：min-max 归一化后再 clip 到 [0,1]
        var maskSmall = new byte[ph * pw];
        for (var y = 0; y < ph; y++)
        {
            for (var x = 0; x < pw; x++)
            {
                var idx = planeOffset + y * pw + x;
                if (idx >= predSpan.Length)
                    continue;
                var t = (predSpan[idx] - pmin) / denom;
                t = Math.Clamp(t, 0f, 1f);
                maskSmall[y * pw + x] = (byte)Math.Clamp(t * 255f, 0f, 255f);
            }
        }

        using var maskLo = Image.LoadPixelData<L8>(maskSmall, pw, ph);
        using var maskFull = maskLo.Clone(ctx => ctx.Resize(ow, oh, KnownResamplers.Lanczos3));

        var cfg = _options.Value;
        // 小分辨率蒙版 Lanczos 放大后 alpha 梯度极陡，阈值只平移亚像素级边缘；先轻模糊拉宽过渡带，阈值才有可见调节空间
        var blurSigma = Math.Clamp(cfg.MaskPreBlurSigma, 0f, 8f);
        if (blurSigma >= 0.05f)
            maskFull.Mutate(ctx => ctx.GaussianBlur(blurSigma));

        var result = new Image<Rgba32>(ow, oh);
        for (var y = 0; y < oh; y++)
        {
            for (var x = 0; x < ow; x++)
            {
                var a = maskFull[x, y].PackedValue / 255f;
                var threshold = Math.Clamp(
                    foregroundThresholdOverride ?? cfg.ForegroundThreshold,
                    0f,
                    0.95f);
                var softness = Math.Clamp(
                    edgeSoftnessOverride ?? cfg.EdgeSoftness,
                    0.01f,
                    1f);
                // 阈值抑制：去掉低置信前景，减少残余背景
                a = (a - threshold) / softness;
                a = Math.Clamp(a, 0f, 1f);
                var p = source[x, y];
                if (cfg.RemoveBlueBackdrop && BlueBackdropDetector.IsLikelyStudioBlue(p))
                    a = Math.Min(a, Math.Clamp(cfg.BlueBackdropAlphaCap, 0f, 1f));

                // 去色边：模型在发丝处常给高 alpha，但像素仍含原墙/天空色；单靠抬阈值压不掉。在过渡带把源 RGB 往目标底色拉。
                var pull = Math.Clamp(edgeColorPullOverride ?? cfg.EdgeColorPullStrength, 0f, 1f);
                float r = p.R, g = p.G, b = p.B;
                if (pull > 0.001f && a is > 0.02f and < 0.995f)
                {
                    var edge = Math.Clamp(4f * a * (1f - a) + 0.5f * (1f - a), 0f, 1f);
                    var mix = pull * edge;
                    r = r * (1f - mix) + bgColor.R * mix;
                    g = g * (1f - mix) + bgColor.G * mix;
                    b = b * (1f - mix) + bgColor.B * mix;
                }

                var spill = Math.Clamp(cfg.BlueSpillSuppression, 0f, 1f);
                if (spill > 0.001f && !MattingColorUtils.TargetBackgroundLooksBlueish(bgColor))
                    MattingColorUtils.ApplyBlueSpillRef(ref r, ref g, ref b, spill, a);

                result[x, y] = new Rgba32(
                    (byte)(r * a + bgColor.R * (1 - a)),
                    (byte)(g * a + bgColor.G * (1 - a)),
                    (byte)(b * a + bgColor.B * (1 - a)));
            }
        }

        var defringe = Math.Clamp(cfg.PostCompositeBlueDefringe, 0f, 1f);
        if (defringe > 0.001f && !MattingColorUtils.TargetBackgroundLooksBlueish(bgColor))
            MattingColorUtils.PostCompositeBlueDefringe(result, bgColor, defringe);

        if (cfg.RefineBlueFringe)
            RefineBlueFringeOnImage(result);

        return result;
    }

    private static void RefineBlueFringeOnImage(Image<Rgba32> img)
    {
        for (var y = 0; y < img.Height; y++)
        {
            for (var x = 0; x < img.Width; x++)
            {
                var q = img[x, y];
                if (!BlueBackdropDetector.IsResidualBlueNearWhite(q))
                    continue;
                img[x, y] = new Rgba32(255, 255, 255);
            }
        }
    }

    public void Dispose() => _session?.Dispose();
}

internal static class MattingColorUtils
{
    /// <summary>目标底色本身偏蓝时，不做强去蓝，避免把合法蓝底/蓝衬衫洗没。</summary>
    public static bool TargetBackgroundLooksBlueish(Rgba32 bg)
    {
        return bg.B >= bg.R + 18 && bg.B >= bg.G + 10;
    }

    /// <summary>按蒙版透明度加权压蓝；a 高时用「蓝过量」自适配权重，避免仅靠 4a(1-a)。</summary>
    public static void ApplyBlueSpillRef(ref float r, ref float g, ref float b, float spillStrength, float a)
    {
        var maxRg = Math.Max(r, g);
        var blueExcess = b - (maxRg + 4f);
        if (blueExcess <= 0f)
            return;

        var edge = Math.Clamp(4f * a * (1f - a) + 0.55f * (1f - a), 0f, 1f);
        var chroma = Math.Clamp(blueExcess / 48f, 0.08f, 1f);
        var w = Math.Max(edge, 0.22f + 0.78f * chroma);
        b -= blueExcess * spillStrength * w;
        if (b < 0f) b = 0f;
    }

    public static void PostCompositeBlueDefringe(Image<Rgba32> img, Rgba32 bg, float strength)
    {
        for (var y = 0; y < img.Height; y++)
        {
            for (var x = 0; x < img.Width; x++)
            {
                var p = img[x, y];
                float r = p.R, g = p.G, b = p.B;
                var maxRg = Math.Max(r, g);
                var excess = b - maxRg - 3f;
                if (excess <= 0f)
                    continue;

                var t = Math.Clamp(excess / 32f, 0f, 1f) * strength;
                b -= excess * t;
                if (b < 0f) b = 0f;
                var mix = t * 0.38f;
                r = r * (1f - mix) + bg.R * mix;
                g = g * (1f - mix) + bg.G * mix;
                b = b * (1f - mix) + bg.B * mix;
                img[x, y] = new Rgba32(
                    (byte)Math.Clamp(r, 0f, 255f),
                    (byte)Math.Clamp(g, 0f, 255f),
                    (byte)Math.Clamp(b, 0f, 255f));
            }
        }
    }
}

internal static class BlueBackdropDetector
{
    public static bool IsLikelyStudioBlue(Rgba32 p)
    {
        float r = p.R, g = p.G, b = p.B;
        if (b < 85f)
            return false;
        if (b <= r + 18f)
            return false;
        if (b <= g + 12f)
            return false;
        var second = Math.Max(r, g);
        if (b - second < 22f)
            return false;
        var lum = (r + g + b) / 3f;
        if (lum < 95f)
            return false;
        return true;
    }

    public static bool IsResidualBlueNearWhite(Rgba32 p)
    {
        float r = p.R, g = p.G, b = p.B;
        if (b < 195f)
            return false;
        if (b <= r + 6f)
            return false;
        if (b <= g + 4f)
            return false;
        var lum = (r + g + b) / 3f;
        return lum > 208f;
    }
}
