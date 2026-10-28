using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Zjcs.Offline.World.NativeMap13;
using Zjcs.Official.ProfessionUI;
using Zjcs.Official.StoryNodes;
using Zjcs.Official.StoryPlayableRequests;

namespace Zjcs.Offline.World.NativeMap13StoryPlayables
{
    // Resource/serialization boundary only. Execution is the original request body.
    public sealed class NativeMap13StoryPlayableAssets : IProfessionImageResourceLoader
    {
        [Serializable] private sealed class GuidField { public string mAssetGuid; }
        [Serializable] private sealed class Fields
        {
            public GuidField[] PlayablesProxy;
            public float PlayableDura;
            public int IsWaitPlayableStop, IsRunWhenSkip, IsStopAllPlayableWhenStoryFinish;
            public int IsStopEntitesPlayableSupportBeforeNodeRun, IsStartEntitiesPlayableSupportAfterNodeRun;
        }
        private readonly Dictionary<string, GameObject> prefabs = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        public int Count => prefabs.Count;

        public static NativeMap13StoryPlayableAssets Load()
        {
            var result = new NativeMap13StoryPlayableAssets();
            foreach (string name in new[] { "native-npc-story-playables-catalog", "native-npc-appearance-playables-catalog" })
            {
                var catalog = Resources.Load<NativeMap13AssetCatalog>("Map13/Native/" + name);
                if (!catalog) throw new InvalidDataException("Original Map13 playable catalog is missing: " + name);
                foreach (var binding in catalog.Bindings)
                {
                    if (binding.OfficialPath?.Length != 32 || !(binding.Asset is GameObject prefab)
                        || !prefab.GetComponent<Zjcs.Official.StoryPlayables.MyPlayable>()
                        || !result.prefabs.TryAdd(binding.OfficialPath, prefab))
                        throw new InvalidDataException("Original Map13 playable binding is invalid: " + binding.OfficialPath);
                }
            }
            if (result.Count != 12) throw new InvalidDataException("Original Map13 NPC playable closure differs.");
            return result;
        }

        public PlayPlayableNode CreateRequest(string sourceJson)
        {
            var fields = JsonUtility.FromJson<Fields>(sourceJson);
            if (fields?.PlayablesProxy == null || fields.PlayablesProxy.Length == 0
                || !(fields.PlayableDura > 0f || fields.PlayableDura == -1f))
                throw new InvalidDataException("Original Map13 request duration/asset list is invalid.");
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
                    throw new InvalidDataException("Original Map13 request asset is not bound: " + field?.mAssetGuid);
                request.PlayablesProxy.Add(JsonUtility.FromJson<AssetGuidProxy<GameObject>>(JsonUtility.ToJson(field)));
            }
            return request;
        }
        private static bool Flag(int value)
        {
            if (value != 0 && value != 1) throw new InvalidDataException("Original Map13 request flag is invalid.");
            return value != 0;
        }
        public IProfessionAssetOperation<T> LoadAssetByGuid<T>(string guid) where T : UnityEngine.Object
        {
            if (!prefabs.TryGetValue(guid, out var prefab) || !(prefab is T value))
                throw new InvalidDataException("Original Map13 playable GUID/type is unbound: " + guid);
            return new Lease<T>(value);
        }
        public IProfessionAssetOperation<T> LoadAssetAsyncByGuid<T>(string guid) where T : UnityEngine.Object => LoadAssetByGuid<T>(guid);
        public IProfessionAssetOperation<T> LoadAsset<T>(string path) where T : UnityEngine.Object => throw new InvalidOperationException("Map13 playable requests use original asset GUIDs.");
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
