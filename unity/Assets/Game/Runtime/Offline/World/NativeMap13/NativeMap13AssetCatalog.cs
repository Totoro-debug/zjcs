using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Offline.World.NativeMap13
{
    // Local bindings for the official PartitionData resource paths; no network loader.
    public sealed class NativeMap13AssetCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Binding
        {
            public string OfficialPath;
            public UnityEngine.Object Asset;
        }

        public Binding[] Bindings = Array.Empty<Binding>();
        private Dictionary<string, UnityEngine.Object> assets;

        public T Load<T>(string officialPath) where T : UnityEngine.Object
        {
            if (assets == null)
            {
                assets = new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);
                foreach (Binding binding in Bindings)
                {
                    if (binding.Asset == null || !assets.TryAdd(binding.OfficialPath, binding.Asset))
                        throw new InvalidOperationException("Invalid Map13 native resource binding: " + binding.OfficialPath);
                }
            }
            if (!assets.TryGetValue(officialPath, out UnityEngine.Object asset) || !(asset is T value))
                throw new InvalidOperationException("Map13 native resource is missing or has the wrong type: " + officialPath);
            return value;
        }
    }
}
