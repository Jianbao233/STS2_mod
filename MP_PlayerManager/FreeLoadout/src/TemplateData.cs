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
        public string Name = "New Template";
        public string Description = "";
        public List<string> CardIds = new();
        public List<string> RelicIds = new();
        public List<string> PotionIds = new();
        public int Gold = 0;
        public int MaxHp = 0;
        public int Energy = 0;
    }
}
