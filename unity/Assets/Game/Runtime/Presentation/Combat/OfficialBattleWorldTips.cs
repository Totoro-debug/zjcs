using System;
using UnityEngine;

namespace Zjcs.Presentation.Combat
{
    [Serializable] public sealed class BattleWorldTipData
    {
        public string manifest;
        public BattleWorldTipBinding[] bindings;
    }
    [Serializable] public sealed class BattleWorldTipBinding
    {
        public string key;
        public BattleCustomActionWorldRuntimeData prefab;
        public BattleElementTextBinding[] texts;
    }

    public static class OfficialBattleWorldTips
    {
        private static BattleWorldTipData current;
        public static BattleWorldTipBinding Find(string key)
        {
            if (current==null)
            {
                var asset=Resources.Load<TextAsset>("Combat/world-tips");
                if (asset==null) throw new InvalidOperationException("Original world damage tips are missing.");
                var data=JsonUtility.FromJson<BattleWorldTipData>(asset.text);
                if (data==null || data.manifest!="854_156383" || data.bindings==null || data.bindings.Length!=3)
                    throw new FormatException("Original world damage tip bindings changed.");
                current=data;
            }
            foreach (var binding in current.bindings)
                if (binding.key==key) return binding;
            return null;
        }
    }
}
