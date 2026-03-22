using Godot;

namespace SharedConfig.Extensions
{
    public static class GodotExtensions
    {
        public static T AddChildSafely<T>(this Godot.Node parent, T child) where T : Godot.Node => child;
        public static void QueueFreeSafely(this Godot.Node? node) { }
        public static void RefreshLayout(this Godot.Node node) { }
        public static Node Instantiate(this Node node) => null!;
        public static T Instantiate<T>(this Node node) where T : Node => null!;
        public static T GetNode<T>(this Godot.Node node) where T : Godot.Node => null!;
    }

    public static class ControlExtensions
    {
        public static void SetAnchorsAndOffsetsPreset(this Godot.Control control, int preset, int mode = 0) { }
        public static void DrawDebug(this Godot.Control item) { }
        public static void DrawDebug(this Godot.Control artist, Godot.Control child) { }
    }

    public static class VBoxContainerExtensions
    {
        public static void FreeChildren(this Godot.VBoxContainer container) { }
    }

    // Extension methods for MegaRichTextLabel theme/font overrides
    public static class MegaRichTextLabelExtensions
    {
        public static void AddThemeFontOverride(this MegaCrit.Sts2.addons.mega_text.MegaRichTextLabel label, string name, Godot.Font font) { }
        public static void AddThemeFontSizeOverride(this MegaCrit.Sts2.addons.mega_text.MegaRichTextLabel label, string name, int size) { }
    }

    // NHoverTipSet extension methods
    public static class NHoverTipSetExtensions
    {
        public static MegaCrit.Sts2.Core.Nodes.HoverTips.NHoverTipSet CreateAndShow(this MegaCrit.Sts2.Core.Nodes.HoverTips.NHoverTipSet tipSet, Godot.Control source, MegaCrit.Sts2.Core.HoverTips.HoverTip tip)
        {
            tipSet.GlobalPosition = source.GlobalPosition + new Vector2(1015, 0);
            source.GetParent()?.AddChild(tipSet);
            return tipSet;
        }

        public static void Remove(this MegaCrit.Sts2.Core.Nodes.HoverTips.NHoverTipSet tipSet, Godot.Control source) { }
    }
}
