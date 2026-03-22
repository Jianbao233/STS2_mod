using Godot;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.HoverTips;
using MegaCrit.Sts2.Core.Nodes.Localization;
using SharedConfig.Extensions;

namespace SharedConfig.Config.UI;

// Wrapper class that takes a control (e.g. toggle, slider) and adds a label and layout with margins,
// while allowing for a HoverTip.
public partial class NConfigOptionRow : MarginContainer
{
    public Control SettingControl { get; private set; } = null!;
    private MegaCrit.Sts2.Core.Nodes.HoverTips.HoverTip? _hoverTip;
    private bool _hoverTipVisible;
    private readonly string _modPrefix;
    private readonly System.Reflection.PropertyInfo _property;

    private const float HoverTipOffset = 1015;

    public NConfigOptionRow(string modPrefix, System.Reflection.PropertyInfo property, Control label, Control settingControl)
    {
        _modPrefix = modPrefix;
        _property = property;
        Name = property.Name;
        SettingControl = settingControl;

        AddThemeConstantOverride("margin_left", 24);
        AddThemeConstantOverride("margin_right", 24);
        MouseFilter = MouseFilterEnum.Pass;
        CustomMinimumSize = new Vector2(0, 64);

        label.CustomMinimumSize = new Vector2(0, 64);

        AddChild(label);
        AddChild(settingControl);
    }

    public void AddCustomHoverTip(string? titleEntryKey, string descriptionEntryKey)
    {
        var descLocString = (object)new LocString("settings_ui", descriptionEntryKey);
        _hoverTip = titleEntryKey != null
            ? new MegaCrit.Sts2.Core.Nodes.HoverTips.HoverTip(
                (object)new LocString("settings_ui", titleEntryKey),
                descLocString)
            : new MegaCrit.Sts2.Core.Nodes.HoverTips.HoverTip(null, descLocString);
    }

    public void AddHoverTip()
    {
        var descriptionEntryKey = _modPrefix + StringHelper.Slugify(_property.Name) + ".hover.desc";

        if (!LocString.Exists("settings_ui", descriptionEntryKey)) return;

        var explicitTitleKey = _modPrefix + StringHelper.Slugify(_property.Name) + ".hover.title";
        var fallbackTitleKey = _modPrefix + StringHelper.Slugify(_property.Name) + ".title";
        var titleKey = LocString.Exists("settings_ui", fallbackTitleKey) ? fallbackTitleKey : null;

        if (LocString.Exists("settings_ui", explicitTitleKey))
        {
            var hasText = LocString.GetIfExists("settings_ui", explicitTitleKey)?.GetFormattedText().Length > 0;
            titleKey = hasText ? explicitTitleKey : null;
        }

        AddCustomHoverTip(titleKey, descriptionEntryKey);
    }

    public override void _Process(double delta)
    {
        if (_hoverTip == null || !IsVisibleInTree()) return;

        var hoveredControl = GetViewport()?.GuiGetHoveredControl();
        var shouldShowHoverTip = hoveredControl != null && (hoveredControl == this || IsAncestorOf(hoveredControl));

        if (shouldShowHoverTip && hoveredControl!.Size.X >= GetViewportRect().Size.X * 0.8f
            && hoveredControl.Size.Y >= GetViewportRect().Size.Y * 0.8f)
            shouldShowHoverTip = GetGlobalRect().HasPoint(GetGlobalMousePosition());

        if (shouldShowHoverTip && !_hoverTipVisible) OnHovered();
        else if (!shouldShowHoverTip && _hoverTipVisible) OnUnhovered();

        _hoverTipVisible = shouldShowHoverTip;
    }

    private void OnHovered()
    {
        if (_hoverTip == null) return;
        var tipSet = NHoverTipSet.CreateAndShow(this, _hoverTip);
        tipSet.GlobalPosition = GlobalPosition + new Vector2(HoverTipOffset, 0);
    }

    private void OnUnhovered()
    {
        if (_hoverTip == null) return;
        NHoverTipSet.Remove(this);
    }
}
