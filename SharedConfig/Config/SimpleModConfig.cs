using System.Reflection;
using Godot;
using SharedConfig.Config;
using SharedConfig.Config.UI;

namespace SharedConfig.Config;

public class SimpleModConfig : ModConfig
{
    /// <summary>
    /// Auto-generate a UI from the properties used. Should be enough for the vast majority of mods.
    /// </summary>
    public override void SetupConfigUI(Control optionContainer)
    {
        VBoxContainer options = new();
        options.Size = optionContainer.Size;
        options.AddThemeConstantOverride("separation", 8);
        optionContainer.AddChild(options);

        options.AddChild(new Control { CustomMinimumSize = new Vector2(0, 16) });

        GenerateOptionsForAllProperties(options);
    }

    protected NConfigOptionRow CreateToggleOption(PropertyInfo property, bool addHoverTip = false) =>
        CreateStandardOption(CreateRawTickboxControl, property, addHoverTip);

    protected NConfigOptionRow CreateSliderOption(PropertyInfo property, bool addHoverTip = false) =>
        CreateStandardOption(CreateRawSliderControl, property, addHoverTip);

    protected NConfigOptionRow CreateDropdownOption(PropertyInfo property, bool addHoverTip = false) =>
        CreateStandardOption(CreateRawDropdownControl, property, addHoverTip);

    protected MarginContainer CreateSectionHeader(string labelName, bool alignToTop = false)
    {
        MarginContainer container = new();
        container.Name = "Container_" + labelName.Replace(" ", "");
        container.AddThemeConstantOverride("margin_left", 24);
        container.AddThemeConstantOverride("margin_right", 24);
        container.MouseFilter = Control.MouseFilterEnum.Ignore;

        var label = CreateRawLabelControl($"[center][b]{GetLabelText(labelName)}[/b][/center]", 40);
        label.Name = "SectionLabel_" + labelName.Replace(" ", "");
        label.CustomMinimumSize = new Vector2(0, 64);

        if (alignToTop) label.VerticalAlignment = VerticalAlignment.Top;

        container.AddChild(label);
        return container;
    }

    protected NConfigOptionRow CreateStandardOption(Func<PropertyInfo, Control> controlCreator, PropertyInfo property, bool addHoverTip = false)
    {
        var control = controlCreator.Invoke(property);
        var label = CreateRawLabelControl(GetLabelText(property.Name), 28);
        var option = new NConfigOptionRow(ModPrefix, property, label, control);
        if (addHoverTip) option.AddHoverTip();
        return option;
    }

    protected NConfigOptionRow GenerateOptionFromProperty(PropertyInfo property)
    {
        var propertyType = property.PropertyType;

        NConfigOptionRow optionRow;
        if (propertyType == typeof(bool)) optionRow = CreateToggleOption(property);
        else if (propertyType == typeof(double)) optionRow = CreateSliderOption(property);
        else if (propertyType.IsEnum) optionRow = CreateDropdownOption(property);
        else throw new NotSupportedException($"Type {propertyType.FullName} is not supported by SimpleModConfig.");

        var propertyHoverAttr = property.GetCustomAttribute<ConfigHoverTipAttribute>();
        var classHoverAttr = GetType().GetCustomAttribute<HoverTipsByDefaultAttribute>();

        var hoverTipsByDefault = classHoverAttr != null;
        var explicitHoverAttrEnabled = propertyHoverAttr?.Enabled;

        if (explicitHoverAttrEnabled ?? hoverTipsByDefault)
            optionRow.AddHoverTip();

        return optionRow;
    }

    protected void GenerateOptionsForAllProperties(Control targetContainer)
    {
        Control? currentSetting = null;
        string? currentSection = null;

        var properties = ConfigProperties.ToArray();
        for (var i = 0; i < properties.Length; i++)
        {
            var property = properties[i];
            var nextProperty = i < properties.Length - 1 ? properties[i + 1] : null;

            var sectionName = property.GetCustomAttribute<ConfigSectionAttribute>()?.Name;
            if (sectionName != null && sectionName != currentSection)
            {
                currentSection = sectionName;
                var isFirstChild = targetContainer.GetChildCount() == 0;
                targetContainer.AddChild(CreateSectionHeader(currentSection, alignToTop: isFirstChild));
            }

            try
            {
                var newRow = GenerateOptionFromProperty(property);
                targetContainer.AddChild(newRow);

                var previousSetting = currentSetting;
                currentSetting = newRow.SettingControl;

                if (previousSetting != null)
                {
                    var path = currentSetting.GetPathTo(previousSetting);
                    if (currentSetting.FocusNeighborLeft == new NodePath()) currentSetting.FocusNeighborLeft = path;
                    if (currentSetting.FocusNeighborTop == new NodePath()) currentSetting.FocusNeighborTop = path;

                    path = previousSetting.GetPathTo(currentSetting);
                    if (previousSetting.FocusNeighborRight == new NodePath()) previousSetting.FocusNeighborRight = path;
                    if (previousSetting.FocusNeighborBottom == new NodePath()) previousSetting.FocusNeighborBottom = path;
                }
            }
            catch (NotSupportedException ex)
            {
                ModConfigLogger.Error($"Not creating UI for unsupported property '{property.Name}': {ex.Message}");
                continue;
            }

            var nextSectionName = nextProperty?.GetCustomAttribute<ConfigSectionAttribute>()?.Name;
            var nextIsSameSection = nextSectionName == null || nextSectionName == currentSection;
            if (nextProperty != null && nextIsSameSection)
                targetContainer.AddChild(CreateDividerControl());
        }
    }
}
