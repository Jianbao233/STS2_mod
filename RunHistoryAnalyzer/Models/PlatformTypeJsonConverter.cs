using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace RunHistoryAnalyzer.Models;

/// <summary>
/// 游戏 RunHistory.platform_type 在 JSON 中可能为数字或字符串（如枚举名 "Steam"），
/// 与游戏内 PlatformType 枚举一致：None=0, Steam=1。
/// </summary>
public sealed class PlatformTypeJsonConverter : JsonConverter<int>
{
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Number:
                if (reader.TryGetInt32(out var n))
                    return n;
                if (reader.TryGetDouble(out var d))
                    return (int)d;
                return 0;
            case JsonTokenType.String:
                var s = reader.GetString();
                if (string.IsNullOrEmpty(s))
                    return 0;
                if (int.TryParse(s, out var i))
                    return i;
                if (string.Equals(s, "Steam", StringComparison.OrdinalIgnoreCase))
                    return 1;
                if (string.Equals(s, "None", StringComparison.OrdinalIgnoreCase))
                    return 0;
                return 0;
            case JsonTokenType.Null:
                return 0;
            default:
                return 0;
        }
    }

    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}
