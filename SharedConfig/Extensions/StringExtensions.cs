using System;
using System.Reflection;

namespace SharedConfig.Extensions;

public static partial class StringExtensions
{
    private const char PrefixSplitChar = '.';

    /// <summary>
    /// Removes the mod prefix from an ID string (everything before and including the last dot).
    /// </summary>
    public static string RemovePrefix(this string id)
    {
        int idx = id.LastIndexOf(PrefixSplitChar);
        return idx < 0 ? id : id[(idx + 1)..];
    }
}

/// <summary>
/// Internal helper used by <see cref="ModConfig"/> to derive mod prefixes from types.
/// </summary>
internal static class TypePrefix
{
    public const char PrefixSplitChar = '.';

    public static string GetPrefix(this Type type)
    {
        string ns = type.GetRootNamespace() ?? "";
        if (string.IsNullOrEmpty(ns)) return "";

        int lastDot = ns.LastIndexOf(PrefixSplitChar);
        return lastDot < 0 ? ns : ns[(lastDot + 1)..];
    }

    public static string? GetRootNamespace(this Type type)
    {
        string? ns = type.Namespace;
        if (string.IsNullOrEmpty(ns)) return null;

        int firstDot = ns.IndexOf(PrefixSplitChar);
        return firstDot < 0 ? ns : ns[..firstDot];
    }
}
