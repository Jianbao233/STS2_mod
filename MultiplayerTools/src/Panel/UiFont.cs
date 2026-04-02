using Godot;

namespace MultiplayerTools.Panel
{
    /// <summary>
    /// Centralised font-size scaling.
    /// Step 0 = base (default / smallest) size.
    /// Each step adds <c>StepPx</c> pixels to all font overrides.
    /// </summary>
    internal static class UiFont
    {
        /// <summary>Font size delta per step.</summary>
        internal const int StepPx = 2;

        /// <summary>Maximum step value (0-6).</summary>
        internal const int MaxStep = 6;

        /// <summary>Add <c>Config.UiFontStep * StepPx</c> to <c>basePx</c>.</summary>
        internal static int Scaled(int basePx) => basePx + Config.UiFontStep * StepPx;

        /// <summary>Apply font_size override to any Control node.</summary>
        internal static void ApplyTo(Control node, int basePx)
        {
            node.AddThemeFontSizeOverride("font_size", Scaled(basePx));
        }
    }
}
