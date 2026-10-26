using System;
using UnityEngine;

namespace Zjcs.Presentation.Combat
{
    [Serializable] public sealed class BattleWorldTipData
    {
        public string manifest;
        public BattleWorldTipBinding[] bindings;
        public BattleWorldTipSpriteAsset[] spriteAssets;
        public BattleWorldTipReplayFlag[] replaySkills;
    }
    [Serializable] public sealed class BattleWorldTipBinding
    {
        public string key;
        public BattleCustomActionWorldRuntimeData prefab;
        public BattleElementTextBinding[] texts;
        public BattleElementMaterialBinding[] sprites;
    }
    [Serializable] public sealed class BattleWorldTipReplayFlag { public int entity; public bool show; }
    [Serializable] public sealed class BattleWorldTipSpriteAsset
    {
        public string key, sourceFile, sourcePath, sourceSha256;
        public BattleElementRectangle rect;
        public BattleElementPoint pivot;
        public float pixelsPerUnit;
        public float[] border;
        public BattleElementPoint[] vertices;
        public ushort[] triangles;
    }

    public static class OfficialBattleWorldTips
    {
        private static BattleWorldTipData current;
        public static BattleWorldTipData Load()
        {
            if (current==null)
            {
                var asset=Resources.Load<TextAsset>("Combat/world-tips");
                if (asset==null) throw new InvalidOperationException("Original world damage tips are missing.");
                var data=JsonUtility.FromJson<BattleWorldTipData>(asset.text);
                if (data==null || data.manifest!="854_156383" || data.bindings==null || data.bindings.Length!=6
                    || data.replaySkills==null || data.replaySkills.Length!=1906)
                    throw new FormatException("Original world damage tip bindings changed.");
                current=data;
            }
            return current;
        }

        public static bool CanShowReplay(int entityId)
        {
            foreach (var flag in Load().replaySkills) if (flag.entity==entityId) return flag.show;
            // A legacy synthetic cast has no original prefab identity. Preserve the original field default.
            if (entityId==0) return true;
            throw new InvalidOperationException("Original skill replay display flag is missing: "+entityId);
        }

        public static BattleWorldTipBinding Find(string key)
        {
            foreach (var binding in Load().bindings)
                if (binding.key==key) return binding;
            return null;
        }
    }
}
