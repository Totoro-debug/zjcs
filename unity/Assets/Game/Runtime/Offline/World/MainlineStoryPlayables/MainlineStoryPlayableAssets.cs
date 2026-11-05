using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.StoryNodes;
using Zjcs.Official.StoryPlayableRequests;

namespace Zjcs.Offline.World.MainlineStoryPlayables
{
    public readonly struct MainlineStoryPlayableCatalogEntry
    {
        public readonly string OfficialPath;
        public readonly UnityEngine.Object Asset;

        public MainlineStoryPlayableCatalogEntry(string officialPath, UnityEngine.Object asset)
        {
            OfficialPath = officialPath;
            Asset = asset;
        }
    }

    // Map configuration selects local prefab catalogs; request execution remains the original body.
    public sealed class MainlineStoryPlayableAssets : IProfessionImageResourceLoader
    {
        [Serializable] private sealed class CatalogConfig
        {
            public string manifest;
            public int mapClassId, expectedCount;
            public string[] catalogs;
        }
        [Serializable] private sealed class GuidField { public string mAssetGuid; }
        [Serializable] private sealed class Fields
        {
            public GuidField[] PlayablesProxy;
            public float PlayableDura;
            public int IsWaitPlayableStop, IsRunWhenSkip, IsStopAllPlayableWhenStoryFinish;
            public int IsStopEntitesPlayableSupportBeforeNodeRun, IsStartEntitiesPlayableSupportAfterNodeRun;
        }
        private readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private readonly int mapClassId;
        public int Count => prefabs.Count;

        private MainlineStoryPlayableAssets(int mapClassId) { this.mapClassId = mapClassId; }

        public static MainlineStoryPlayableAssets Load(int mapClassId, string expectedManifest,
            Func<string, IEnumerable<MainlineStoryPlayableCatalogEntry>> loadCatalog)
        {
            if (mapClassId <= 0 || string.IsNullOrWhiteSpace(expectedManifest) || loadCatalog == null)
                throw new ArgumentException("A mainline map id, pinned story manifest, and catalog adapter are required.");
            TextAsset configAsset = Resources.Load<TextAsset>("Map" + mapClassId + "/Native/story-playables");
            if (configAsset == null)
                throw new InvalidDataException("Original Map" + mapClassId + " story-playable configuration is missing.");
            CatalogConfig config = JsonUtility.FromJson<CatalogConfig>(configAsset.text);
            if (config == null || config.manifest != expectedManifest || config.mapClassId != mapClassId
                || config.expectedCount <= 0 || config.catalogs == null || config.catalogs.Length == 0
                || config.catalogs.Any(string.IsNullOrWhiteSpace)
                || config.catalogs.Distinct(StringComparer.Ordinal).Count() != config.catalogs.Length)
                throw new InvalidDataException("Original Map" + mapClassId + " story-playable configuration is stale or malformed.");
            var result = new MainlineStoryPlayableAssets(mapClassId);
            foreach (string name in config.catalogs)
            {
                IEnumerable<MainlineStoryPlayableCatalogEntry> bindings = loadCatalog(name);
                if (bindings == null)
                    throw new InvalidDataException("Original Map" + mapClassId + " playable catalog is missing or has no bindings: " + name);
                foreach (var binding in bindings)
                {
                    if (binding.OfficialPath?.Length != 32 || !(binding.Asset is GameObject prefab)
                        || !prefab.GetComponent<Zjcs.Official.StoryPlayables.MyPlayable>()
                        || !result.prefabs.TryAdd(binding.OfficialPath, prefab))
                        throw new InvalidDataException("Original Map" + mapClassId + " playable binding is invalid: " + binding.OfficialPath);
                }
            }
            if (result.Count != config.expectedCount)
                throw new InvalidDataException("Original Map" + mapClassId + " playable closure differs from its pinned catalog.");
            return result;
        }

        public PlayPlayableNode CreateRequest(string sourceJson)
        {
            var fields = JsonUtility.FromJson<Fields>(sourceJson);
            if (fields?.PlayablesProxy == null || fields.PlayablesProxy.Length == 0
                || !(fields.PlayableDura > 0f || fields.PlayableDura == -1f))
                throw new InvalidDataException("Original Map" + mapClassId + " request duration/asset list is invalid.");
            var request = new PlayPlayableNode
            {
                ResourceLoader = this,
                PlayableDura = fields.PlayableDura,
                IsWaitPlayableStop = Flag(fields.IsWaitPlayableStop),
                IsRunWhenSkip = Flag(fields.IsRunWhenSkip),
                IsStopAllPlayableWhenStoryFinish = Flag(fields.IsStopAllPlayableWhenStoryFinish),
                IsStopEntitesPlayableSupportBeforeNodeRun = Flag(fields.IsStopEntitesPlayableSupportBeforeNodeRun),
                IsStartEntitiesPlayableSupportAfterNodeRun = Flag(fields.IsStartEntitiesPlayableSupportAfterNodeRun),
                PlayablesProxy = new List<AssetGuidProxy<GameObject>>()
            };
            foreach (var field in fields.PlayablesProxy)
            {
                if (field == null || !prefabs.ContainsKey(field.mAssetGuid))
                    throw new InvalidDataException("Original Map" + mapClassId + " request asset is not bound: " + field?.mAssetGuid);
                request.PlayablesProxy.Add(JsonUtility.FromJson<AssetGuidProxy<GameObject>>(JsonUtility.ToJson(field)));
            }
            return request;
        }
        private static bool Flag(int value)
        {
            if (value != 0 && value != 1) throw new InvalidDataException("Original map request flag is invalid.");
            return value != 0;
        }
        public IProfessionAssetOperation<T> LoadAssetByGuid<T>(string guid) where T : UnityEngine.Object
        {
            if (!prefabs.TryGetValue(guid, out var prefab) || !(prefab is T value))
                throw new InvalidDataException("Original Map" + mapClassId + " playable GUID/type is unbound: " + guid);
            return new Lease<T>(value);
        }
        public IProfessionAssetOperation<T> LoadAssetAsyncByGuid<T>(string guid) where T : UnityEngine.Object => LoadAssetByGuid<T>(guid);
        public IProfessionAssetOperation<T> LoadAsset<T>(string path) where T : UnityEngine.Object => throw new InvalidOperationException("Map" + mapClassId + " playable requests use original asset GUIDs.");
        public IProfessionAssetOperation<T> LoadAssetAsync<T>(string path) where T : UnityEngine.Object => LoadAsset<T>(path);
        private sealed class Lease<T> : IProfessionAssetOperation<T> where T : UnityEngine.Object
        {
            private T asset;
            public Lease(T asset) { this.asset = asset; }
            public bool IsValid => asset;
            public int Status => asset ? 1 : 0;
            public T Result => asset ? asset : throw new ObjectDisposedException(nameof(Lease<T>));
            public event Action<IProfessionAssetOperation<T>> Completed { add { if (asset) value?.Invoke(this); } remove { } }
            public void Dispose() { asset = null; }
        }
    }
}
