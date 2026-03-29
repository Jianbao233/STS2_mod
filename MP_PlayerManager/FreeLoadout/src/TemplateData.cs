using System;
using System.Collections.Generic;
using Godot;

namespace MP_PlayerManager
{
    /// <summary>
    /// 单个角色模板的数据结构。
    /// </summary>
    [Serializable]
    internal class TemplateData
    {
        public string Id = Guid.NewGuid().ToString();
        public string Name = "新建模板";
        public string Description = "";
        public string CharacterId = "";     // "CHARACTER.IRONCLAD" 等，空=未选择
        public string CharacterName = "";   // 显示用（如 "Ironclad"）
        public int CurHp = 0;               // 当前生命值（新建时默认 = MaxHp）
        public int MaxHp = 0;               // 最大生命值
        public int Gold = 0;
        public int Energy = 3;              // 初始能量（从 ModelDb 读取，未读成功时默认 3）
        public List<string> CardIds = new();
        public List<string> RelicIds = new();
        public List<string> PotionIds = new();
        public string ExportPath = "";       // 上次导出路径（Mod 目录相对路径）
    }
}
