using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Godot;

namespace MP_PlayerManager
{
	// Token: 0x02000002 RID: 2
	internal readonly struct HotkeyBinding
	{
		// Token: 0x06000001 RID: 1 RVA: 0x00002050 File Offset: 0x00000250
		public HotkeyBinding(Key key, bool ctrl = false, bool shift = false, bool alt = false)
		{
			this.Key = key;
			this.Ctrl = ctrl;
			this.Shift = shift;
			this.Alt = alt;
		}

		// Token: 0x17000001 RID: 1
		// (get) Token: 0x06000002 RID: 2 RVA: 0x0000206F File Offset: 0x0000026F
		public bool IsNone
		{
			get
			{
				return this.Key == Key.None;
			}
		}

		// Token: 0x06000003 RID: 3 RVA: 0x0000207C File Offset: 0x0000027C
		public bool Matches(InputEventKey keyEvent)
		{
			return this.Key != Key.None && (keyEvent.Keycode == this.Key && keyEvent.CtrlPressed == this.Ctrl && keyEvent.ShiftPressed == this.Shift) && keyEvent.AltPressed == this.Alt;
		}

		// Token: 0x06000004 RID: 4 RVA: 0x000020D0 File Offset: 0x000002D0
		public static HotkeyBinding Parse(string str)
		{
			if (string.IsNullOrWhiteSpace(str))
			{
				return HotkeyBinding.None;
			}
			bool flag = false;
			bool flag2 = false;
			bool flag3 = false;
			string[] array = str.Split('+', StringSplitOptions.None);
			for (int i = 0; i < array.Length - 1; i++)
			{
				string text = array[i].Trim();
				if (text.Equals("Ctrl", StringComparison.OrdinalIgnoreCase))
				{
					flag = true;
				}
				else if (text.Equals("Shift", StringComparison.OrdinalIgnoreCase))
				{
					flag2 = true;
				}
				else if (text.Equals("Alt", StringComparison.OrdinalIgnoreCase))
				{
					flag3 = true;
				}
			}
			string[] array2 = array;
			string text2 = array2[array2.Length - 1].Trim();
			Key key;
			if (!HotkeyBinding.TryParseKey(text2, out key))
			{
				GD.Print("[FreeLoadout] Unknown key: '" + text2 + "'");
				return HotkeyBinding.None;
			}
			return new HotkeyBinding(key, flag, flag2, flag3);
		}

		// Token: 0x06000005 RID: 5 RVA: 0x00002190 File Offset: 0x00000390
		private static bool TryParseKey(string str, out Key key)
		{
			if (Enum.TryParse<Key>(str, true, out key))
			{
				return true;
			}
			if (str.Length == 1 && char.IsDigit(str[0]))
			{
				key = (Key)('0' + (str[0] - '0'));
				return true;
			}
			key = Key.None;
			return false;
		}

		// Token: 0x06000006 RID: 6 RVA: 0x000021CC File Offset: 0x000003CC
		public override string ToString()
		{
			if (this.Key == Key.None)
			{
				return "None";
			}
			List<string> list = new List<string>(4);
			if (this.Ctrl)
			{
				list.Add("Ctrl");
			}
			if (this.Shift)
			{
				list.Add("Shift");
			}
			if (this.Alt)
			{
				list.Add("Alt");
			}
			list.Add(this.Key.ToString());
			return string.Join("+", list);
		}

		// Token: 0x04000001 RID: 1
		public static readonly HotkeyBinding None = new HotkeyBinding(Key.None, false, false, false);

		// Token: 0x04000002 RID: 2
		public readonly Key Key;

		// Token: 0x04000003 RID: 3
		public readonly bool Ctrl;

		// Token: 0x04000004 RID: 4
		public readonly bool Shift;

		// Token: 0x04000005 RID: 5
		public readonly bool Alt;
	}
}
