using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Godot;

namespace MP_PlayerManager
{
	// Token: 0x02000003 RID: 3
	internal static class Config
	{
		// Token: 0x06000008 RID: 8 RVA: 0x0000225D File Offset: 0x0000045D
		internal static void Load()
		{
			Config._hotkeys = null;
			Config._flags = null;
			Config.EnsureLoaded();
		}

		// Token: 0x06000009 RID: 9 RVA: 0x00002270 File Offset: 0x00000470
		internal static HotkeyBinding GetHotkey(string action)
		{
			Config.EnsureLoaded();
			HotkeyBinding hotkeyBinding;
			if (!Config._hotkeys.TryGetValue(action, out hotkeyBinding))
			{
				return HotkeyBinding.None;
			}
			return hotkeyBinding;
		}

		// Token: 0x0600000A RID: 10 RVA: 0x00002298 File Offset: 0x00000498
		internal static bool GetFlag(string name)
		{
			Config.EnsureLoaded();
			bool flag;
			if (!Config._flags.TryGetValue(name, out flag))
			{
				return Config.DefaultFlags.GetValueOrDefault(name, true);
			}
			return flag;
		}

		// Token: 0x0600000B RID: 11 RVA: 0x000022C8 File Offset: 0x000004C8
		internal static bool MatchesAny(InputEventKey keyEvent)
		{
			Config.EnsureLoaded();
			foreach (HotkeyBinding hotkeyBinding in Config._hotkeys.Values)
			{
				if (hotkeyBinding.Matches(keyEvent))
				{
					return true;
				}
			}
			return false;
		}

		// Token: 0x0600000C RID: 12 RVA: 0x00002330 File Offset: 0x00000530
		private static void EnsureLoaded()
		{
			if (Config._hotkeys != null)
			{
				return;
			}
			Config._hotkeys = new Dictionary<string, HotkeyBinding>();
			foreach (KeyValuePair<string, string> keyValuePair in Config.DefaultHotkeys)
			{
				string text;
				string text2;
				keyValuePair.Deconstruct(out text, out text2);
				string text3 = text;
				string text4 = text2;
				Config._hotkeys[text3] = HotkeyBinding.Parse(text4);
			}
			Config._flags = new Dictionary<string, bool>(Config.DefaultFlags);
			string configPath = Config.GetConfigPath();
			try
			{
				if (!File.Exists(configPath))
				{
					Config.WriteDefaults(configPath);
					GD.Print("[FreeLoadout] Created default config.json: " + configPath);
				}
				else
				{
					using (JsonDocument jsonDocument = JsonDocument.Parse(File.ReadAllText(configPath), new JsonDocumentOptions
					{
						CommentHandling = JsonCommentHandling.Skip,
						AllowTrailingCommas = true
					}))
					{
						JsonElement jsonElement;
						if (jsonDocument.RootElement.TryGetProperty("hotkeys", out jsonElement))
						{
							foreach (JsonProperty jsonProperty in jsonElement.EnumerateObject())
							{
								HotkeyBinding hotkeyBinding = HotkeyBinding.Parse(jsonProperty.Value.GetString());
								if (!hotkeyBinding.IsNone)
								{
									Config._hotkeys[jsonProperty.Name] = hotkeyBinding;
								}
							}
						}
						JsonElement jsonElement2;
						if (jsonDocument.RootElement.TryGetProperty("flags", out jsonElement2))
						{
							foreach (JsonProperty jsonProperty2 in jsonElement2.EnumerateObject())
							{
								if (jsonProperty2.Value.ValueKind == JsonValueKind.True || jsonProperty2.Value.ValueKind == JsonValueKind.False)
								{
									Config._flags[jsonProperty2.Name] = jsonProperty2.Value.GetBoolean();
								}
							}
						}
						DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(43, 3);
						defaultInterpolatedStringHandler.AppendLiteral("[FreeLoadout] Loaded ");
						defaultInterpolatedStringHandler.AppendFormatted("config.json");
						defaultInterpolatedStringHandler.AppendLiteral(": hotkeys=[");
						defaultInterpolatedStringHandler.AppendFormatted(string.Join(", ", Config._hotkeys.Select(delegate(KeyValuePair<string, HotkeyBinding> kv)
						{
							DefaultInterpolatedStringHandler defaultInterpolatedStringHandler2 = new DefaultInterpolatedStringHandler(1, 2);
							defaultInterpolatedStringHandler2.AppendFormatted(kv.Key);
							defaultInterpolatedStringHandler2.AppendLiteral("=");
							defaultInterpolatedStringHandler2.AppendFormatted<HotkeyBinding>(kv.Value);
							return defaultInterpolatedStringHandler2.ToStringAndClear();
						})));
						defaultInterpolatedStringHandler.AppendLiteral("], flags=[");
						defaultInterpolatedStringHandler.AppendFormatted(string.Join(", ", Config._flags.Select(delegate(KeyValuePair<string, bool> kv)
						{
							DefaultInterpolatedStringHandler defaultInterpolatedStringHandler3 = new DefaultInterpolatedStringHandler(1, 2);
							defaultInterpolatedStringHandler3.AppendFormatted(kv.Key);
							defaultInterpolatedStringHandler3.AppendLiteral("=");
							defaultInterpolatedStringHandler3.AppendFormatted<bool>(kv.Value);
							return defaultInterpolatedStringHandler3.ToStringAndClear();
						})));
						defaultInterpolatedStringHandler.AppendLiteral("]");
						GD.Print(defaultInterpolatedStringHandler.ToStringAndClear());
					}
				}
			}
			catch (Exception ex)
			{
				GD.Print("[FreeLoadout] Failed to load config.json: " + ex.Message);
			}
		}

		// Token: 0x0600000D RID: 13 RVA: 0x00002684 File Offset: 0x00000884
		private static string GetConfigPath()
		{
			try
			{
				string location = Assembly.GetExecutingAssembly().Location;
				if (!string.IsNullOrEmpty(location))
				{
					string directoryName = Path.GetDirectoryName(location);
					if (!string.IsNullOrEmpty(directoryName))
					{
						return Path.Combine(directoryName, "config.json");
					}
				}
			}
			catch
			{
			}
			try
			{
				string directoryName2 = Path.GetDirectoryName(OS.GetExecutablePath());
				if (!string.IsNullOrEmpty(directoryName2))
				{
					return Path.Combine(directoryName2, "mods", "FreeLoadout", "config.json");
				}
			}
			catch
			{
			}
			return "config.json";
		}

		// Token: 0x0600000E RID: 14 RVA: 0x0000271C File Offset: 0x0000091C
		private static void WriteDefaults(string path)
		{
			try
			{
				Dictionary<string, object> dictionary = new Dictionary<string, object>();
				dictionary["_readme"] = "Hotkey format: Key or Ctrl+Key or Shift+Alt+Key. Valid keys: F1-F12, A-Z, Space, Enter, Tab, Escape, etc.";
				dictionary["hotkeys"] = new Dictionary<string, string>(Config.DefaultHotkeys);
				dictionary["flags"] = new Dictionary<string, bool>(Config.DefaultFlags);
				string text = JsonSerializer.Serialize<Dictionary<string, object>>(dictionary, new JsonSerializerOptions
				{
					WriteIndented = true
				});
				File.WriteAllText(path, text);
			}
			catch (Exception ex)
			{
				GD.Print("[FreeLoadout] Failed to write default config.json: " + ex.Message);
			}
		}

		// Token: 0x0600000F RID: 15 RVA: 0x000027AC File Offset: 0x000009AC
		// Note: this type is marked as 'beforefieldinit'.
		static Config()
		{
			Dictionary<string, string> dictionary = new Dictionary<string, string>();
			dictionary["toggle_panel"] = "F1";
			Config.DefaultHotkeys = dictionary;
			Dictionary<string, bool> dictionary2 = new Dictionary<string, bool>();
			dictionary2["show_topbar_icon"] = true;
			Config.DefaultFlags = dictionary2;
		}

		// Token: 0x04000006 RID: 6
		private const string FileName = "config.json";

		// Token: 0x04000007 RID: 7
		private static Dictionary<string, HotkeyBinding> _hotkeys;

		// Token: 0x04000008 RID: 8
		private static Dictionary<string, bool> _flags;

		// Token: 0x04000009 RID: 9
		private static readonly Dictionary<string, string> DefaultHotkeys;

		// Token: 0x0400000A RID: 10
		private static readonly Dictionary<string, bool> DefaultFlags;
	}
}
