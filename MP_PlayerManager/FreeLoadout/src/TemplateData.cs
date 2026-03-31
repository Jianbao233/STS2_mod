using System;
using Godot;

namespace MP_PlayerManager
{
    [Serializable]
    internal class TemplateData
    {
        public string Id = Guid.NewGuid().ToString();
        public string Name = "New Template";
        public string Description = "";
        public string CharacterId = "";
        public string CharacterName = "";
        public int CurHp = 0;
        public int MaxHp = 0;
        public int Gold = 0;
        public int Energy = 3;
        public System.Collections.Generic.List<string> CardIds = new();
        public System.Collections.Generic.List<string> RelicIds = new();
        public System.Collections.Generic.List<string> PotionIds = new();
        public string ExportPath = "";

        internal static TemplateData CreateDefault(string characterId = "CHARACTER.IRONCLAD")
        {
            return new TemplateData
            {
                Name = Loc.Get("tmpl.default_name", "New Template"),
                CharacterId = characterId
            };
        }
    }
}