using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Localization;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;
using SharedConfig.Config.UI;
using SharedConfig.Extensions;
using SharedConfig.Utils;

namespace SharedConfig.Config;

public abstract partial class ModConfig
{
    private const string SettingsTheme = "res://themes/settings_screen_line_header.tres";

    /// <summary>Event that fires when <see cref="Changed"/> is called. Custom controls must call Changed() when mutating a property.</summary>
    public event EventHandler? ConfigChanged;

    private readonly string _path;
    protected string ModPrefix { get; private set; }

    private readonly string _modConfigName;
    private bool _savingDisabled;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    protected readonly List<PropertyInfo> ConfigProperties = [];

    public static class ModConfigLogger
    {
        public static List<string> PendingUserMessages { get; } = [];

        public static void Warn(string message, bool showInGui = false)
        {
            if (showInGui && !PendingUserMessages.Contains(message))
                PendingUserMessages.Add(message);
        }

        public static void Error(string message, bool showInGui = true)
        {
            if (showInGui && !PendingUserMessages.Contains(message))
                PendingUserMessages.Add(message);
        }
    }

    public ModConfig(string? filename = null)
    {
        ModPrefix = GetType().GetPrefix();
        _modConfigName = GetType().FullName ?? "unknown";
        var rootNamespace = GetType().GetRootNamespace();

        if (string.IsNullOrEmpty(rootNamespace) && string.IsNullOrEmpty(filename))
        {
            var message = $"Cannot determine a safe configuration file path for {_modConfigName} " +
                $"(assembly {GetType().Assembly.GetName().Name}). " +
                "You must either place your configuration class inside a namespace, " +
                "or explicitly provide a filename in the constructor.";
            ModConfigLogger.Error(message);
            throw new InvalidOperationException(message);
        }

        var defaultFilename = SpecialCharRegex().Replace(rootNamespace ?? "", "");

        filename = filename == null ? defaultFilename : SpecialCharRegex().Replace(filename, "");
        if (!filename.Contains('.')) filename += ".cfg";

        _path = Path.Combine(OS.GetUserDataDir(), "mod_configs", filename);

        CheckConfigProperties();
        Init();
    }

    public bool HasSettings() => ConfigProperties.Count > 0;

    private void CheckConfigProperties()
    {
        ConfigProperties.Clear();
        foreach (var property in GetType().GetProperties())
        {
            if (!property.CanRead || !property.CanWrite) continue;
            if (property.GetMethod?.IsStatic != true)
            {
                ModConfigLogger.Warn($"Ignoring {_modConfigName} property {property.Name}: only static properties are supported", true);
                continue;
            }
            ConfigProperties.Add(property);
        }
    }

    public abstract void SetupConfigUI(Control optionContainer);

    private void Init()
    {
        if (File.Exists(_path)) Load();
        else Save();
    }

    public void Changed()
    {
        ConfigChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Save()
    {
        if (_savingDisabled)
        {
            ModConfigLogger.Error($"Skipping save for {_modConfigName} because the config file is currently in a corrupted, read-only state.");
            return;
        }

        Dictionary<string, string> values = [];
        foreach (var property in ConfigProperties)
        {
            var value = property.GetValue(null);
            var converter = TypeDescriptor.GetConverter(property.PropertyType);
            var stringValue = converter.ConvertToInvariantString(value);

            if (stringValue != null)
                values.Add(property.Name, stringValue);
            else
                ModConfigLogger.Warn($"Failed to convert {_modConfigName} property {property.Name} to string for saving; it will be omitted.", true);
        }

        try
        {
            new FileInfo(_path).Directory?.Create();
            using var fileStream = File.Create(_path);
            JsonSerializer.Serialize(fileStream, values, JsonOptions);
        }
        catch (Exception e)
        {
            ModConfigLogger.Error($"Failed to save config {_modConfigName}: {e.Message}");
        }
    }

    public void Load()
    {
        if (!File.Exists(_path))
        {
            ModConfigLogger.Error($"Load for {_modConfigName} failed. File not found: {_path}");
            return;
        }

        var hasSoftErrors = false;
        _savingDisabled = false;

        try
        {
            using var fileStream = File.OpenRead(_path);
            var values = JsonSerializer.Deserialize<Dictionary<string, string>>(fileStream);

            if (values == null)
            {
                ModConfigLogger.Warn($"Config file {_modConfigName} was empty or null. Will re-save using default values.");
                hasSoftErrors = true;
            }
            else
            {
                foreach (var property in ConfigProperties)
                {
                    if (!values.TryGetValue(property.Name, out var value))
                    {
                        ModConfigLogger.Warn($"Config {_modConfigName} has no value for {property.Name}; will re-save to fill in the default.", true);
                        hasSoftErrors = true;
                        continue;
                    }
                    if (!TryApplyPropertyValue(property, value)) hasSoftErrors = true;
                }
            }
        }
        catch (JsonException jsonEx)
        {
            ModConfigLogger.Error($"Failed to parse config file for {_modConfigName}. The JSON is likely invalid. Error: {jsonEx.Message}");
            ModConfigLogger.Warn("Config saving has been DISABLED for this session to protect any manual edits. Please fix the JSON formatting.", true);
            _savingDisabled = true;
            return;
        }
        catch (Exception e)
        {
            ModConfigLogger.Error($"Unexpected error loading config {_modConfigName}: {e.Message}");
            return;
        }

        if (hasSoftErrors && !_savingDisabled)
        {
            ModConfigLogger.Warn($"Saving fresh config for {_modConfigName} to correct soft errors.");
            Save();
        }
    }

    private static bool TryApplyPropertyValue(PropertyInfo property, string value)
    {
        try
        {
            var converter = TypeDescriptor.GetConverter(property.PropertyType);
            var configVal = converter.ConvertFromInvariantString(value);

            if (configVal == null)
            {
                ModConfigLogger.Warn($"Failed to load saved config value \"{value}\" for property {property.Name}: Converter returned null.", true);
                return false;
            }

            property.SetValue(null, configVal);
            return true;
        }
        catch (Exception ex)
        {
            ModConfigLogger.Warn($"Failed to load saved config value \"{value}\" for property {property.Name}. Error: {ex.Message}", true);
            return false;
        }
    }

    protected string GetLabelText(string labelName)
    {
        var loc = LocString.GetIfExists("settings_ui", ModPrefix + StringHelper.Slugify(labelName) + ".title");
        return loc?.GetFormattedText() ?? labelName;
    }

    protected NConfigTickbox CreateRawTickboxControl(PropertyInfo property)
    {
        var tickbox = new NConfigTickbox().TransferAllNodes(SceneHelper.GetScenePath("screens/settings_tickbox"));
        tickbox.Initialize(this, property);
        return tickbox;
    }

    protected NConfigSlider CreateRawSliderControl(PropertyInfo property)
    {
        var slider = new NConfigSlider().TransferAllNodes(SceneHelper.GetScenePath("screens/settings_slider"));
        slider.Initialize(this, property);
        return slider;
    }

    private static readonly FieldInfo DropdownNode = AccessTools.DeclaredField(typeof(NDropdownPositioner), "_dropdownNode");
    protected NDropdownPositioner CreateRawDropdownControl(PropertyInfo property)
    {
        var dropdown = new NConfigDropdown().TransferAllNodes(SceneHelper.GetScenePath("screens/settings_dropdown"));
        var items = CreateDropdownItems(property, out var currentIndex);
        dropdown.SetItems(items, currentIndex);

        var dropdownPositioner = new NDropdownPositioner();
        dropdownPositioner.SetCustomMinimumSize(new Vector2(320, 64));
        dropdownPositioner.FocusMode = Control.FocusModeEnum.All;
        dropdownPositioner.SizeFlagsHorizontal = Godot.Control.SizeFlags.ShrinkEnd;
        dropdownPositioner.SizeFlagsVertical = Godot.Control.SizeFlags.Fill;
        DropdownNode.SetValue(dropdownPositioner, dropdown);

        dropdownPositioner.AddChild(dropdown);
        dropdownPositioner.MouseFilter = Control.MouseFilterEnum.Ignore;

        return dropdownPositioner;
    }

    private List<ConfigDropdownItem> CreateDropdownItems(PropertyInfo property, out int currentIndex)
    {
        List<ConfigDropdownItem> items = [];
        var type = property.PropertyType;
        var currentValue = property.GetValue(null);
        int count = 0;
        currentIndex = 0;

        if (type.IsEnum)
        {
            foreach (var value in type.GetEnumValues())
            {
                if (currentValue != null && currentValue.Equals(value))
                    currentIndex = count;
                ++count;
                var loc = LocString.GetIfExists("settings_ui", $"{ModPrefix}{StringHelper.Slugify(property.Name)}.{value}");
                var label = loc?.GetRawText() ?? value?.ToString() ?? "UNKNOWN";
                items.Add(new ConfigDropdownItem(label, () =>
                {
                    property.SetValue(null, value);
                    Changed();
                }));
            }
        }
        else
        {
            throw new NotSupportedException("Dropdown only supports enum types currently");
        }

        return items;
    }

    protected static MegaRichTextLabel CreateRawLabelControl(string labelText, int fontSize)
    {
        var kreonNormal = PreloadManager.Cache.GetAsset("res://themes/kreon_regular_shared.tres") as Godot.Font;
        var kreonBold = PreloadManager.Cache.GetAsset("res://themes/kreon_bold_shared.tres") as Godot.Font;

        MegaRichTextLabel label = new()
        {
            Name = "Label",
            Theme = PreloadManager.Cache.GetAsset(SettingsTheme) as Godot.Theme,
            AutoSizeEnabled = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            BbcodeEnabled = true,
            ScrollActive = false,
            VerticalAlignment = VerticalAlignment.Center,
            Text = labelText
        };

        if (kreonNormal != null) label.AddThemeFontOverride("normal_font", kreonNormal);
        if (kreonBold != null) label.AddThemeFontOverride("bold_font", kreonBold);
        label.AddThemeFontSizeOverride("normal_font_size", fontSize);
        label.AddThemeFontSizeOverride("bold_font_size", fontSize);
        label.AddThemeFontSizeOverride("bold_italics_font_size", fontSize);
        label.AddThemeFontSizeOverride("italics_font_size", fontSize);
        label.AddThemeFontSizeOverride("mono_font_size", fontSize);

        return label;
    }

    protected static ColorRect CreateDividerControl()
    {
        return new ColorRect
        {
            Name = "Divider",
            CustomMinimumSize = new Vector2(0, 2),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Color = new Color(0.909804f, 0.862745f, 0.745098f, 0.25098f)
        };
    }

    [GeneratedRegex("[^a-zA-Z0-9_.]")]
    private static partial Regex SpecialCharRegex();
}
