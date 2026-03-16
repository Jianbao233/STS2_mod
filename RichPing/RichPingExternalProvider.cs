using System.Collections.Generic;

namespace RichPing;

/// <summary>
/// 第三方 Mod 角色 Ping 文本提供接口。
/// 其他 Mod 的角色开发者可实现此接口并调用 RichPingMod.RegisterExternalProvider 注册，
/// RichPing 在选取文本时会优先查询已注册的提供者。
/// 详见 VC_RICHPING_DEVELOPER_GUIDE.md。
/// </summary>
public interface IRichPingTextProvider
{
    /// <summary>
    /// 该提供者支持的角色 Entry 列表（如 "MY_MOD_CHARACTER"）。
    /// 若为空列表，表示支持所有角色（作为兜底提供者，慎用）。
    /// </summary>
    IReadOnlyList<string> SupportedCharacterIds { get; }

    /// <summary>
    /// 获取指定角色、楼层、存活状态下的 Ping 文本。
    /// </summary>
    /// <param name="characterId">角色 Entry，如 IRONCLAD、THE_HIEROPHANT</param>
    /// <param name="actIndex">当前幕索引，0=第一幕，1=第二幕，2=第三幕</param>
    /// <param name="isDead">角色是否已死亡</param>
    /// <returns>自定义文本；返回 null 或空字符串时交给 RichPing 默认逻辑或其他提供者</returns>
    string GetPingText(string characterId, int actIndex, bool isDead);
}
