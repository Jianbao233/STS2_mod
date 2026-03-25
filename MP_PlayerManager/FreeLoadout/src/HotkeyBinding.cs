using System;
using System.Collections.Generic;
using Godot;

namespace MP_PlayerManager
{
    /// <summary>
    /// 表示一个按键绑定（支持 Ctrl/Shift/Alt 修饰键）。
    /// </summary>
    internal readonly struct HotkeyBinding
    {
        public static readonly HotkeyBinding None = new(Key.None, false, false, false);

        public HotkeyBinding(Key key, bool ctrl = false, bool shift = false, bool alt = false)
        {
            Key = key;
            Ctrl = ctrl;
            Shift = shift;
            Alt = alt;
        }

        public Key Key { get; }
        public bool Ctrl { get; }
        public bool Shift { get; }
        public bool Alt { get; }

        public bool IsNone => Key == Key.None;

        public bool Matches(InputEventKey keyEvent)
        {
            return Key != Key.None
                && keyEvent.Keycode == Key
                && keyEvent.CtrlPressed == Ctrl
                && keyEvent.ShiftPressed == Shift
                && keyEvent.AltPressed == Alt;
        }

        public static HotkeyBinding Parse(string str)
        {
            if (string.IsNullOrWhiteSpace(str)) return None;

            bool ctrl = false, shift = false, alt = false;
            string[] parts = str.Split('+', StringSplitOptions.None);

            for (int i = 0; i < parts.Length - 1; i++)
            {
                string p = parts[i].Trim();
                if (p.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) ctrl = true;
                else if (p.Equals("Shift", StringComparison.OrdinalIgnoreCase)) shift = true;
                else if (p.Equals("Alt", StringComparison.OrdinalIgnoreCase)) alt = true;
            }

            string last = parts[^1].Trim();
            if (!Enum.TryParse<Key>(last, true, out Key key))
            {
                if (last.Length == 1 && char.IsDigit(last[0]))
                    key = (Key)('0' + (last[0] - '0'));
                else
                {
                    GD.Print("[MP_PlayerManager] Unknown key: '" + last + "'");
                    return None;
                }
            }

            return new HotkeyBinding(key, ctrl, shift, alt);
        }

        public override string ToString()
        {
            if (Key == Key.None) return "None";
            var parts = new List<string>();
            if (Ctrl) parts.Add("Ctrl");
            if (Shift) parts.Add("Shift");
            if (Alt) parts.Add("Alt");
            parts.Add(Key.ToString());
            return string.Join("+", parts);
        }
    }
}
