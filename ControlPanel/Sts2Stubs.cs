// 编译期存根
using System;

namespace MegaCrit.Sts2.Core.Modding
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
    public class ModInitializerAttribute : Attribute
    {
        public ModInitializerAttribute(string methodName) { }
    }
}

namespace MegaCrit.Sts2.Core.Logging
{
    public static class Log
    {
        public static void Info(string msg) => Godot.GD.Print(msg);
        public static void Warn(string msg) => Godot.GD.PrintErr(msg);
    }
}
