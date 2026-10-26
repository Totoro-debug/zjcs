using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Presentation.Combat
{
    [Serializable] public sealed class BattleHudMaterialBinding { public string path; public Material material; }
    [Serializable] public sealed class BattleHudMaterialSource
    {
        public string manifest;
        public BattleHudMaterialReference[] bindings;
        public BattleTimelineMaterial[] materials;
        public BattleHudMaterialTexture[] textures;
        public BattleHudMaterialShader[] shaders;
    }
    [Serializable] public sealed class BattleHudMaterialReference { public string path, material; }
    [Serializable] public sealed class BattleHudMaterialShader { public string key, name, path, sha256; public string[] selectedKeywords; }
    [Serializable] public sealed class BattleHudMaterialTexture
    {
        public string key, path, sha256;
        public int width, height, filter, wrapU, wrapV, aniso;
        public float bias;
        public bool sRGB;
    }

    public sealed class OfficialBattleHudMaterialAssets : ScriptableObject
    {
        public BattleHudMaterialBinding[] bindings = Array.Empty<BattleHudMaterialBinding>();
        public string SourceSha256;
        private Dictionary<string, Material> byPath;
        private static OfficialBattleHudMaterialAssets current;

        public static OfficialBattleHudMaterialAssets Load()
        {
            if (current == null) current = Resources.Load<OfficialBattleHudMaterialAssets>("Combat/OfficialBattleHudMaterialAssets");
            if (current == null) throw new InvalidOperationException("Restore the original battle HUD Image materials.");
            return current;
        }
        public Material Find(string path)
        {
            if (byPath == null)
            {
                byPath = new Dictionary<string, Material>(StringComparer.Ordinal);
                foreach (BattleHudMaterialBinding binding in bindings) byPath.Add(binding.path, binding.material);
            }
            return byPath.TryGetValue(path, out Material material) ? material : null;
        }
    }
}
