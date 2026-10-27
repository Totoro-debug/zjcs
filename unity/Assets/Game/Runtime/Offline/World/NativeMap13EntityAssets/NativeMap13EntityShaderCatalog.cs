using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Offline.World.NativeMap13EntityAssets
{
    public sealed class NativeMap13EntityShaderCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string sourceCab;
            public string sourcePathId;
            public string originalName;
            public string originalFallback;
            public Shader shader;
        }

        public string manifest;
        public Entry[] entries = Array.Empty<Entry>();
        private Dictionary<string, Shader> lookup;

        public Shader Require(string cab, string pathId)
        {
            if (lookup == null)
            {
                var created = new Dictionary<string, Shader>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in entries)
                {
                    if (entry.shader == null || string.IsNullOrEmpty(entry.sourceCab) ||
                        string.IsNullOrEmpty(entry.sourcePathId))
                        throw new InvalidOperationException("Incomplete native Map13 shader binding.");
                    created.Add(entry.sourceCab + ":" + entry.sourcePathId, entry.shader);
                }
                lookup = created;
            }
            if (!lookup.TryGetValue(cab + ":" + pathId, out var shader))
                throw new KeyNotFoundException("Native Map13 shader is not bound: " + cab + ":" + pathId);
            return shader;
        }

        private void OnEnable() { lookup = null; }
    }
}
