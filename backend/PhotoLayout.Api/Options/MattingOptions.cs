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

    /// <summary>
    /// 放大到原图尺寸后、套阈值前对蒙版做高斯模糊（sigma，像素级）。
    /// U2Net 小图放大后蒙版往往很“陡”，不调模糊时阈值只移动亚像素级边缘，肉眼几乎看不出。
    /// 设为 0 关闭。
    /// </summary>
    public float MaskPreBlurSigma { get; set; } = 1.35f;

    /// <summary>
    /// 发丝/轮廓处常出现「蒙版很实但 RGB 仍夹原背景色」的色边；阈值主要改 alpha，对这种边几乎无效。
    /// 本项在半透明过渡带把源色往目标底色拽（0=关，~0.45 较均衡）。
    /// </summary>
    public float EdgeColorPullStrength { get; set; } = 0.42f;

    /// <summary>
    /// 蓝色溢色抑制强度（0-1）。针对发丝中常见的蓝幕/天空溢色，压低蓝通道；与 edgeColorPull 无关，高 alpha 发丝也会处理。
    /// </summary>
    public float BlueSpillSuppression { get; set; } = 0.72f;

    /// <summary>
    /// 合成后再扫一遍结果图去蓝边（0=关）。换白/红/灰底时建议大于 0；目标底色本身偏蓝时请关小或置 0。
    /// </summary>
    public float PostCompositeBlueDefringe { get; set; } = 0.88f;
}
