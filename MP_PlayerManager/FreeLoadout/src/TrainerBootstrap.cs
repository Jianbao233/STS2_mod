using System;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Modding;

namespace MP_PlayerManager
{
	// Token: 0x02000016 RID: 22
	[ModInitializer("Init")]
	public static class TrainerBootstrap
	{
		// Token: 0x0600005A RID: 90 RVA: 0x00006284 File Offset: 0x00004484
		public static void Init()
		{
			if (TrainerBootstrap._initialized)
			{
				return;
			}
			TrainerBootstrap._initialized = true;
			Config.Load();
			new Harmony("bon.freeloadout").PatchAll(Assembly.GetExecutingAssembly());
			GD.Print("[FreeLoadout] Initialized");
		}

		// Token: 0x04000025 RID: 37
		private static bool _initialized;
	}
}
