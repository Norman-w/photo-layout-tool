通用抠图（任意背景→白底，推荐）：
https://github.com/danielgatis/rembg/releases/download/v0.0.0/u2net.onnx
保存为 onnx/u2net.onnx（与 appsettings 默认一致）。

仅人像、更利于证件照：
https://github.com/danielgatis/rembg/releases/download/v0.0.0/u2net_human_seg.onnx
保存为 onnx/u2net_human_seg.onnx，并把 Matting:OnnxModelPath 改成该路径。

证件棚拍蓝底可另开 Matting:RemoveBlueBackdrop=true、RefineBlueFringe=true。
