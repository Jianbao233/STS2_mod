namespace SharedConfig.Config;

[AttributeUsage(AttributeTargets.Property)]
public class ConfigSectionAttribute(string name) : Attribute
{
    public string Name { get; } = name;
}

[AttributeUsage(AttributeTargets.Property)]
public class SliderRangeAttribute : Attribute
{
    public double Min { get; }
    public double Max { get; }
    public double Step { get; }

    public SliderRangeAttribute(double min, double max, double step = 1.0)
    {
        if (min > max)
            throw new ArgumentException($"SliderRange: Min ({min}) cannot be greater than Max ({max}).");
        if (step <= 0)
            throw new ArgumentOutOfRangeException(nameof(step), "SliderRange: Step must be greater than 0.");

        Min = min;
        Max = max;
        Step = step;
    }
}

[AttributeUsage(AttributeTargets.Property)]
public class SliderLabelFormatAttribute(string format) : Attribute
{
    public string Format { get; } = format;
}

/// <summary>
/// Show a tooltip for this setting on hover. Requires a settings_ui.json localization file.
/// The names used are typically:
/// YOURMOD-PROPERTY_NAME.hover.title (optional)
/// YOURMOD-PROPERTY_NAME.hover.desc (required)
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public class ConfigHoverTipAttribute(bool enabled = true) : Attribute
{
    public bool Enabled { get; } = enabled;
}

/// <summary>
/// Attempt to add hover tips to ALL config properties in this class. Use with
/// enabled=false to add exceptions. Still requires .hover.desc entries in your localization file.
/// </summary>
[AttributeUsage(AttributeTargets.Class)]
public class HoverTipsByDefaultAttribute : Attribute;
