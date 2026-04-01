namespace PhotoLayout.Api.Options;

public class MattingOptions
{
    public const string SectionName = "Matting";

    /// <summary>
    /// 相对 ContentRoot 或绝对路径。推荐 <c>onnx/u2net.onnx</c>（任意显著前景）；
    /// 仅人像可选用 <c>onnx/u2net_human_seg.onnx</c>。缺文件时回退 Pad。
    /// </summary>
    public string OnnxModelPath { get; set; } = "onnx/u2net.onnx";

    public bool Enabled { get; set; } = true;

    /// <summary>证件蓝幕优化：任意背景场景建议 false，避免误伤大面积蓝色物体。</summary>
    public bool RemoveBlueBackdrop { get; set; } = false;

    /// <summary>判为蓝底时，蒙版 alpha 上限。</summary>
    public float BlueBackdropAlphaCap { get; set; } = 0.08f;

    /// <summary>合成后去浅蓝边；一般场景可 false。</summary>
    public bool RefineBlueFringe { get; set; } = false;

    /// <summary>前景阈值（0-1）：越大越干净，但可能丢失发丝边缘。</summary>
    public float ForegroundThreshold { get; set; } = 0.58f;

    /// <summary>阈值后的软过渡宽度（0-1）：越大边缘越柔和。</summary>
    public float EdgeSoftness { get; set; } = 0.12f;
}
