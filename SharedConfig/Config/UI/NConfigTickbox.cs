using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using SharedConfig.Config;
using SharedConfig.Extensions;

namespace SharedConfig.Config.UI;

public partial class NConfigTickbox : NSettingsTickbox
{
    private ModConfig? _config;
    private System.Reflection.PropertyInfo? _property;

    public NConfigTickbox()
    {
        SetCustomMinimumSize(new Vector2(64, 64));
        SizeFlagsHorizontal = Godot.Control.SizeFlags.ShrinkEnd;
        SizeFlagsVertical = Godot.Control.SizeFlags.Fill;
    }

    public override void _Ready()
    {
        if (_property == null) throw new System.Exception("NConfigTickbox added to tree without an assigned property");
        ConnectSignals();

        var tickboxVisuals = GetNode("%TickboxVisuals") as Godot.Control;
        if (tickboxVisuals != null)
        {
            tickboxVisuals.SetAnchorsAndOffsetsPreset(3 /* LayoutPreset.CenterRight */, 1 /* KeepSize */);
        }

        if (GetParent() is MarginContainer parentContainer)
        {
            parentContainer.AddThemeConstantOverride("margin_right",
                parentContainer.GetThemeConstant("margin_right") - 10);
        }

        SetFromProperty();
    }

    public void Initialize(ModConfig modConfig, System.Reflection.PropertyInfo property)
    {
        if (property.PropertyType != typeof(bool)) throw new System.ArgumentException("Attempted to assign NConfigTickbox a non-bool property");
        _config = modConfig;
        _property = property;
    }

    private void SetFromProperty()
    {
        IsTicked = (bool?)_property!.GetValue(null) == true;
    }

    protected override void OnTick()
    {
        _property?.SetValue(null, true);
        _config?.Changed();
    }

    protected override void OnUntick()
    {
        _property?.SetValue(null, false);
        _config?.Changed();
    }
}
