using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Presentation.Combat
{
    [Serializable]
    public sealed class OfficialBattleNpcPortraitBinding
    {
        public int npcClassId;
        public string icon;
        public Sprite sprite;
    }

    /// <summary>Original NpcInfo.Icon bindings used by GameRoundCharacterItemScene.RefreshNpcIcon.</summary>
    public sealed class OfficialBattleNpcPortraitCatalog : ScriptableObject
    {
        public const string ResourcePath = "Combat/OfficialBattleNpcPortraitCatalog";
        public OfficialBattleNpcPortraitBinding[] bindings = Array.Empty<OfficialBattleNpcPortraitBinding>();
        private Dictionary<int, Sprite> byClassId;

        public static OfficialBattleNpcPortraitCatalog Load()
        {
            var catalog = Resources.Load<OfficialBattleNpcPortraitCatalog>(ResourcePath);
            if (catalog == null) throw new InvalidOperationException("Restore the original battle NPC portrait bindings.");
            return catalog;
        }

        public Sprite Find(int npcClassId)
        {
            if (byClassId == null)
            {
                byClassId = new Dictionary<int, Sprite>();
                foreach (OfficialBattleNpcPortraitBinding binding in bindings)
                    byClassId.Add(binding.npcClassId, binding.sprite);
            }
            return byClassId.TryGetValue(npcClassId, out Sprite sprite) ? sprite : null;
        }
    }
}
