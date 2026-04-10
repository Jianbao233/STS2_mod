using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;

namespace LoadOrderManager;

internal sealed class LoadOrderEntry
{
    public LoadOrderEntry(string id, string name, int source, bool isEnabled)
    {
        Id = id;
        Name = string.IsNullOrWhiteSpace(name) ? id : name;
        Source = source;
        IsEnabled = isEnabled;
    }

    public string Id { get; }
    public string Name { get; }
    public int Source { get; }
    public bool IsEnabled { get; set; }

    public string SourceText => Source switch
    {
        1 => I18n.T("source_mods"),
        2 => I18n.T("source_workshop"),
        _ => I18n.T("source_unknown")
    };

    public string Key => BuildKey(Id, Source);

    public static string BuildKey(string id, int source) => $"{id}::{source}";
}

internal static class LoadOrderRuntime
{
    public static bool TryGetOrderedEntries(out List<LoadOrderEntry> entries, out string error)
    {
        entries = new List<LoadOrderEntry>();
        error = string.Empty;

        try
        {
            var loadedModsByKey = ReadLoadedMods();
            if (loadedModsByKey.Count == 0)
            {
                error = "No mods found in ModManager.AllMods.";
                DebugLog.Warn(error);
                return false;
            }

            ReadSettings(out var settingsOrder, out var enabledByKey);

            var used = new HashSet<string>(StringComparer.Ordinal);
            foreach (var key in settingsOrder)
            {
                if (!loadedModsByKey.TryGetValue(key, out var mod)) continue;
                mod.IsEnabled = enabledByKey.GetValueOrDefault(key, true);
                entries.Add(mod);
                used.Add(key);
            }

            foreach (var kv in loadedModsByKey)
            {
                if (used.Contains(kv.Key)) continue;
                kv.Value.IsEnabled = enabledByKey.GetValueOrDefault(kv.Key, true);
                entries.Add(kv.Value);
            }

            DebugLog.Info($"Read order success. allMods={loadedModsByKey.Count}, settingsOrder={settingsOrder.Count}, merged={entries.Count}");
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            DebugLog.Error("TryGetOrderedEntries failed.", ex);
            return false;
        }
    }

    public static bool TrySaveOrderedEntries(IReadOnlyList<LoadOrderEntry> orderedEntries, out string error)
    {
        error = string.Empty;

        try
        {
            var saveManagerType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Saves.SaveManager");
            if (saveManagerType == null)
            {
                error = "SaveManager type not found.";
                DebugLog.Error(error);
                return false;
            }

            var saveManager = AccessTools.Property(saveManagerType, "Instance")?.GetValue(null);
            if (saveManager == null)
            {
                error = "SaveManager.Instance is null.";
                DebugLog.Error(error);
                return false;
            }

            var settingsSave = GetMemberValue(saveManager, "SettingsSave");

            if (settingsSave == null)
            {
                error = "SaveManager.SettingsSave is null.";
                DebugLog.Error(error);
                return false;
            }

            var modSettingsType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Modding.ModSettings");
            var settingsSaveModType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Modding.SettingsSaveMod");
            var modSourceType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Modding.ModSource");
            if (modSettingsType == null || settingsSaveModType == null || modSourceType == null)
            {
                error = "Mod settings runtime types not found.";
                DebugLog.Error(error);
                return false;
            }

            var modSettings = GetMemberValue(settingsSave, "ModSettings");
            if (modSettings == null)
            {
                modSettings = Activator.CreateInstance(modSettingsType);
                SetMemberValue(settingsSave, "ModSettings", modSettings);
            }

            if (modSettings == null)
            {
                error = "Failed to create ModSettings object.";
                DebugLog.Error(error);
                return false;
            }

            var listType = typeof(List<>).MakeGenericType(settingsSaveModType);
            var modList = (IList?)Activator.CreateInstance(listType);
            if (modList == null)
            {
                error = "Failed to create mod list.";
                DebugLog.Error(error);
                return false;
            }

            DebugLog.Info($"Saving manual order with {orderedEntries.Count} entries. Top={FormatTop(orderedEntries)}");

            foreach (var entry in orderedEntries)
            {
                var item = Activator.CreateInstance(settingsSaveModType);
                if (item == null) continue;

                SetMemberValue(item, "Id", entry.Id);
                SetMemberValue(item, "Source", Enum.ToObject(modSourceType, entry.Source));
                SetMemberValue(item, "IsEnabled", entry.IsEnabled);
                modList.Add(item);
            }

            SetMemberValue(modSettings, "ModList", modList);

            var saveSettings = AccessTools.Method(saveManagerType, "SaveSettings", Type.EmptyTypes);
            if (saveSettings == null)
            {
                error = "SaveManager.SaveSettings() not found.";
                DebugLog.Error(error);
                return false;
            }

            saveSettings.Invoke(saveManager, null);

            var expectedKeys = orderedEntries.Select(e => e.Key).ToList();
            ReadSettings(out var persistedKeys, out _);

            if (!IsOrderMonotonic(expectedKeys, persistedKeys))
            {
                error = "Order verification failed after save. Check log for details.";
                DebugLog.Warn($"Order verification failed. expectedTop={FormatTop(expectedKeys)}, persistedTop={FormatTop(persistedKeys)}");
                return false;
            }

            DebugLog.Info($"SaveSettings succeeded. Persisted top={FormatTop(persistedKeys)}");
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            DebugLog.Error("TrySaveOrderedEntries failed.", ex);
            return false;
        }
    }

    private static Dictionary<string, LoadOrderEntry> ReadLoadedMods()
    {
        var dict = new Dictionary<string, LoadOrderEntry>(StringComparer.Ordinal);
        var modManagerType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Modding.ModManager");
        var allModsObj = AccessTools.Property(modManagerType, "AllMods")?.GetValue(null);
        if (allModsObj is not IEnumerable allMods) return dict;

        foreach (var modObj in allMods)
        {
            var manifest = GetMemberValue(modObj, "manifest");
            var id = AsString(GetMemberValue(manifest, "id"));
            if (string.IsNullOrWhiteSpace(id)) continue;

            var name = AsString(GetMemberValue(manifest, "name"));
            var source = ToInt(GetMemberValue(modObj, "modSource"));
            var key = LoadOrderEntry.BuildKey(id, source);

            dict[key] = new LoadOrderEntry(id, name ?? id, source, true);
        }

        return dict;
    }

    private static void ReadSettings(out List<string> orderedKeys, out Dictionary<string, bool> enabledByKey)
    {
        orderedKeys = new List<string>();
        enabledByKey = new Dictionary<string, bool>(StringComparer.Ordinal);

        var saveManagerType = AccessTools.TypeByName("MegaCrit.Sts2.Core.Saves.SaveManager");
        var saveManager = AccessTools.Property(saveManagerType, "Instance")?.GetValue(null);
        var settingsSave = GetMemberValue(saveManager, "SettingsSave");
        var modSettings = GetMemberValue(settingsSave, "ModSettings");
        var modListObj = GetMemberValue(modSettings, "ModList");
        if (modListObj is not IEnumerable modList) return;

        foreach (var item in modList)
        {
            var id = AsString(GetMemberValue(item, "Id"));
            if (string.IsNullOrWhiteSpace(id)) continue;

            var source = ToInt(GetMemberValue(item, "Source"));
            var enabled = ToBool(GetMemberValue(item, "IsEnabled"), true);
            var key = LoadOrderEntry.BuildKey(id, source);

            orderedKeys.Add(key);
            enabledByKey[key] = enabled;
        }
    }

    private static object? GetMemberValue(object? obj, string name)
    {
        if (obj == null) return null;
        var type = obj.GetType();
        var prop = AccessTools.Property(type, name);
        if (prop != null) return prop.GetValue(obj);
        var field = AccessTools.Field(type, name);
        return field?.GetValue(obj);
    }

    private static void SetMemberValue(object? obj, string name, object? value)
    {
        if (obj == null) return;
        var type = obj.GetType();
        var prop = AccessTools.Property(type, name);
        if (prop != null)
        {
            prop.SetValue(obj, value);
            return;
        }
        var field = AccessTools.Field(type, name);
        field?.SetValue(obj, value);
    }

    private static string? AsString(object? obj)
    {
        return obj switch
        {
            null => null,
            string s => s,
            _ => obj.ToString()
        };
    }

    private static int ToInt(object? obj)
    {
        return obj switch
        {
            null => 0,
            int i => i,
            long l => (int)l,
            Enum e => Convert.ToInt32(e),
            _ when int.TryParse(obj.ToString(), out var parsed) => parsed,
            _ => 0
        };
    }

    private static bool ToBool(object? obj, bool fallback)
    {
        return obj switch
        {
            null => fallback,
            bool b => b,
            _ when bool.TryParse(obj.ToString(), out var parsed) => parsed,
            _ => fallback
        };
    }

    private static bool IsOrderMonotonic(IReadOnlyList<string> expectedOrder, IReadOnlyList<string> persistedOrder)
    {
        var indexMap = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < persistedOrder.Count; i++)
        {
            indexMap[persistedOrder[i]] = i;
        }

        var prev = -1;
        foreach (var key in expectedOrder)
        {
            if (!indexMap.TryGetValue(key, out var idx))
            {
                continue;
            }

            if (idx < prev)
            {
                return false;
            }

            prev = idx;
        }

        return true;
    }

    private static string FormatTop(IReadOnlyList<LoadOrderEntry> entries)
    {
        var take = Math.Min(entries.Count, 8);
        if (take == 0) return "<empty>";
        var ids = entries.Take(take).Select(e => $"{e.Id}({e.Source})");
        return string.Join(" > ", ids);
    }

    private static string FormatTop(IReadOnlyList<string> keys)
    {
        var take = Math.Min(keys.Count, 8);
        if (take == 0) return "<empty>";
        return string.Join(" > ", keys.Take(take));
    }
}
