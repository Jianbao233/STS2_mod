using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Godot;
using MegaCrit.Sts2.Core.Localization;

namespace MP_PlayerManager
{
	// Token: 0x02000027 RID: 39
	internal static class Loc
	{
		// Token: 0x060000AF RID: 175 RVA: 0x00007438 File Offset: 0x00005638
		private static Dictionary<string, string> LoadTable(string language)
		{
			string text = "res://MP_PlayerManager/localization/" + language + "/ui.json";
			if (!FileAccess.FileExists(text))
			{
				return new Dictionary<string, string>();
			}
			Dictionary<string, string> dictionary;
			using (FileAccess fileAccess = FileAccess.Open(text, FileAccess.ModeFlags.Read))
			{
				if (fileAccess == null)
				{
					dictionary = new Dictionary<string, string>();
				}
				else
				{
					dictionary = JsonSerializer.Deserialize<Dictionary<string, string>>(fileAccess.GetAsText(false), null) ?? new Dictionary<string, string>();
				}
			}
			return dictionary;
		}

		// Token: 0x060000B0 RID: 176 RVA: 0x000074AC File Offset: 0x000056AC
		private static Dictionary<string, string> GetStrings()
		{
			LocManager instance = LocManager.Instance;
			string text = ((instance != null) ? instance.Language : null) ?? "eng";
			if (Loc._strings != null && Loc._loadedLanguage == text)
			{
				return Loc._strings;
			}
			Loc._strings = Loc.LoadTable(text);
			if (Loc._strings.Count == 0 && text != "eng")
			{
				Loc._strings = Loc.LoadTable("eng");
			}
			Loc._loadedLanguage = text;
			return Loc._strings;
		}

		// Token: 0x060000B1 RID: 177 RVA: 0x0000752C File Offset: 0x0000572C
		internal static void Reload()
		{
			Loc._strings = null;
			Loc._loadedLanguage = null;
		}

		// Token: 0x060000B2 RID: 178 RVA: 0x0000753C File Offset: 0x0000573C
		internal static string Get(string key, string fallback = null)
		{
			string text;
			if (Loc.GetStrings().TryGetValue(key, out text) && !string.IsNullOrEmpty(text))
			{
				return text;
			}
			return fallback ?? key;
		}

		// Token: 0x060000B3 RID: 179 RVA: 0x00007568 File Offset: 0x00005768
		internal static string Fmt(string key, params object[] args)
		{
			return string.Format(Loc.Get(key, null), args);
		}

		// Token: 0x04000044 RID: 68
		private static Dictionary<string, string> _strings;

		// Token: 0x04000045 RID: 69
		private static string _loadedLanguage;
	}
}
