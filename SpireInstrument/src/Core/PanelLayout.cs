using System;

namespace SpireInstrument.Core;

/// <summary>
/// 面板尺寸计算（**纯逻辑，不引用 Godot**，可离线自检）。
///
/// 抽出来的原因：这里出过真问题 —— 缩放后内容变高，面板被撑到屏幕外、
/// 键盘与曲目区看不见。布局数学必须能被断言长期看住，而不是靠肉眼看截图。
/// </summary>
public static class PanelLayout
{
    /// <summary>各档位的基准尺寸（未缩放、未夹取）。</summary>
    public static (float W, float H) BaseSize(PanelSize size, float miniHeight) => size switch
    {
        PanelSize.Mini => (700f, miniHeight),
        PanelSize.Large => (1320f, 600f),
        _ => (1080f, 524f),
    };

    /// <summary>
    /// 目标尺寸：基准 × 缩放，再夹进视口（留边距），并保证不小于可用下限。
    /// </summary>
    public static (float W, float H) TargetSize(PanelSize size, float ui, float viewportW, float viewportH, float miniHeight)
    {
        var (bw, bh) = BaseSize(size, miniHeight);
        float marginX = size == PanelSize.Mini ? 40f : 60f;
        float marginY = size == PanelSize.Mini ? 40f : (size == PanelSize.Large ? 100f : 120f);

        float w = Math.Min(bw * ui, Math.Max(200f, viewportW - marginX));
        float h = Math.Min(bh * ui, Math.Max(80f, viewportH - marginY));
        return (w, h);
    }

    /// <summary>把面板高度夹进视口（内容比目标更高时用）。</summary>
    public static float ClampHeight(float height, float viewportH)
        => Math.Min(height, Math.Max(120f, viewportH - 40f));
}
