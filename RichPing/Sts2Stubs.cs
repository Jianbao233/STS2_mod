// 编译期存根，解决 Godot 4.5.1 (.NET 8) 与 sts2 (.NET 9) 版本冲突
// 移除 sts2 直接引用，改用 net8.0 以被 Godot 加载；Harmony 等运行时通过反射访问游戏程序集

using System;
using Godot;

namespace MegaCrit.Sts2.Core.Modding
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method | AttributeTargets.Struct)]
    public class ModInitializerAttribute : Attribute
    {
        public ModInitializerAttribute(string methodName) { }
    }
}

namespace MegaCrit.Sts2.Core.Logging
{
    public static class Log
    {
        public static void Info(string message) => GD.Print(message);
        public static void Warn(string message) => GD.PrintErr(message);
    }
}
