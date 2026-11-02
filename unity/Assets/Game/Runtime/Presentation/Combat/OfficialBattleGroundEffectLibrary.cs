using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.StoryGroundEffects;

namespace Zjcs.Presentation.Combat
{
    /// <summary>Local native prefab loading for the original ApplyByGroundType.</summary>
    public sealed class OfficialBattleGroundEffectLibrary : IPlayableGroundEffectHost
    {
        [Serializable] private sealed class Catalog { public string manifest; public Entry[] effects; }
        [Serializable] private sealed class Entry { public string guid, resource; }
        private readonly Dictionary<string, string> resources = new Dictionary<string, string>();
        private readonly Func<IPlayableGroundSurface> ground;

        public OfficialBattleGroundEffectLibrary(Func<IPlayableGroundSurface> resolveGround)
        {
            ground = resolveGround ?? throw new ArgumentNullException(nameof(resolveGround));
            var asset = Resources.Load<TextAsset>("Combat/ground-effects");
            if (asset == null) throw new InvalidOperationException("Missing original battle ground effect catalog.");
            var data = JsonUtility.FromJson<Catalog>(asset.text);
            if (data == null || data.manifest != "884_156692" || data.effects == null)
                throw new InvalidOperationException("Invalid original battle ground effect catalog.");
            foreach (var entry in data.effects)
            {
                if (entry == null || string.IsNullOrEmpty(entry.guid) || string.IsNullOrEmpty(entry.resource)
                    || resources.ContainsKey(entry.guid))
                    throw new InvalidOperationException("Invalid original ground effect resource binding.");
                resources.Add(entry.guid, entry.resource);
            }
        }

        public IPlayableGroundSurface ResolveGround() => ground();

        public IPlayableGroundEffectAsset LoadAssetByGuid(string guid)
        {
            if (guid == null || !resources.TryGetValue(guid, out var path))
                throw new KeyNotFoundException("Unbound original battle ground effect: " + guid);
            var prefab = Resources.Load<GameObject>(path);
            if (prefab == null) throw new InvalidOperationException("Missing native ground effect prefab: " + path);
            return new Asset(prefab);
        }

        public GameObject Instantiate(GameObject prefab) => UnityEngine.Object.Instantiate(prefab);

        // Resources owns the imported asset. A playable lease releases its
        // managed reference; it must not unload/destroy a prefab shared by
        // another simultaneous battle or by a live instance.
        private sealed class Asset : IPlayableGroundEffectAsset
        {
            private GameObject value;
            public Asset(GameObject prefab) { value = prefab; }
            public GameObject Result => value != null ? value : throw new ObjectDisposedException(nameof(Asset));
            public void Dispose() { value = null; }
        }
    }
}
