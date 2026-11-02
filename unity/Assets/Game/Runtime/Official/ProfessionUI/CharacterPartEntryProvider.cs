using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Zjcs.Official.ProfessionUI
{
    // Official 884_156692 CharacterPartEntryProvider application lifecycle.
    // Framework loading/DI/scene subscriptions are explicit native ports;
    // path caching, LIFO reuse, timers, counts and release order remain here.
    public sealed class CharacterPartEntryProvider : ICharacterPartEntryProvider, IDisposable
    {
        private readonly Dictionary<string, List<CharacterPartPoolEntry>> mDicEntryPool = new Dictionary<string, List<CharacterPartPoolEntry>>();
        private readonly Dictionary<string, IProfessionCharacterPartPrefab> mDicHandleCache = new Dictionary<string, IProfessionCharacterPartPrefab>();
        private readonly Dictionary<string, int> mDicInstanceCounts = new Dictionary<string, int>();
        private readonly List<CharacterPartPoolEntry> mListWillReleaseItems = new List<CharacterPartPoolEntry>();
        private readonly List<string> mTempListKeys = new List<string>();
        private readonly List<BaseSkeletonFollower> mTempListFollowers = new List<BaseSkeletonFollower>();
        private readonly IProfessionCharacterPartPoolBindings bindings;
        private Transform mPoolRoot;
        private float mUpdateTimer;
        private bool mPoolReceiveObj;
        public bool UsePool => bindings.UsePool;

        public CharacterPartEntryProvider(IProfessionCharacterPartPoolBindings bindings)
        {
            this.bindings = bindings ?? throw new ArgumentNullException(nameof(bindings));
            var go = new GameObject("[CharacterPartPool]");
            if (Application.isPlaying) Object.DontDestroyOnLoad(go);
            mPoolRoot = go.transform; go.SetActive(false);
            Application.lowMemory += OnLowMemory;
            mPoolReceiveObj = true;
        }

        public void Update() => Update(Time.deltaTime);
        public void Update(float deltaTime)
        {
            mUpdateTimer += deltaTime;
            float elapsed = mUpdateTimer;
            if (mUpdateTimer < 1f) return;
            mUpdateTimer = 0f;
            foreach (var item in mDicEntryPool)
            {
                var value = item.Value;
                for (int i = value.Count - 1; i >= 0; i--)
                {
                    var entry = value[i]; entry.LeftAliveTime -= elapsed;
                    if (entry.LeftAliveTime <= 0f) { mListWillReleaseItems.Add(entry); value.RemoveAt(i); }
                }
            }
            foreach (var entry in mListWillReleaseItems) ReleaseEntry(entry, false);
            mListWillReleaseItems.Clear();
        }

        public CharacterPartPoolEntry RentIntegratedPart(SkinType type, string skin, Transform parent, bool usePool, Action<GameObject> onBeforeSetParent)
            => GetPart(ProfessionAppearancePartPaths.GetPlayerPartPrefabRenderer(PlayerPartMode.Integrated, skin), parent, usePool, onBeforeSetParent);
        public CharacterPartPoolEntry RentStandalonePart(SkinType type, string skin, Transform parent, bool isUI, bool usePool)
            => GetPart(isUI ? ProfessionAppearancePartPaths.GetPlayerPartPrefabUI(PlayerPartMode.Standalone, skin)
                : ProfessionAppearancePartPaths.GetPlayerPartPrefabRenderer(PlayerPartMode.Standalone, skin), parent, usePool);

        public void ReturnPart(CharacterPartPoolEntry entry, bool tryUnloadAllAssets)
        {
            if (entry == null || !entry.Valid) { Debug.LogError("CharacterPartEntryProvider ReleasePart entry == null || !entry.Valid"); return; }
            var go = entry.GoPart;
            TryClearBaseSkeletonFollowerState(go);
            bindings.PrepareReturn(go);
            if (entry.InPool && mPoolReceiveObj)
            {
                string path = entry.AssetPath; go.transform.SetParent(mPoolRoot);
                if (!mDicEntryPool.TryGetValue(path, out var list)) { list = new List<CharacterPartPoolEntry>(); mDicEntryPool.Add(path, list); }
                entry.ResetAliveTime(); list.Add(entry);
            }
            else ReleaseEntry(entry, tryUnloadAllAssets);
        }

        public void ClearPool()
        {
            mTempListKeys.Clear(); mTempListKeys.AddRange(mDicEntryPool.Keys);
            foreach (string key in mTempListKeys)
            {
                var list = mDicEntryPool[key];
                for (int i = list.Count - 1; i >= 0; i--)
                { var entry = list[i]; list.RemoveAt(i); ReleaseEntry(entry, false); }
            }
        }

        private void ReleaseEntry(CharacterPartPoolEntry entry, bool tryUnloadAllAssets)
        {
            if (!entry.Valid) { Debug.LogError("CharacterPartEntryProvider ReleaseEntry entry.Valid == false"); return; }
            if (entry.GoPart) bindings.Destroy(entry.GoPart, tryUnloadAllAssets);
            string path = entry.AssetPath;
            bool found = mDicInstanceCounts.TryGetValue(path, out int count);
            if (found) mDicInstanceCounts[path] = --count;
            if (found && count <= 0 && mDicHandleCache.TryGetValue(path, out var handle))
            {
                handle.Release(tryUnloadAllAssets); mDicHandleCache.Remove(path);
                mDicInstanceCounts.Remove(path); mDicEntryPool.Remove(path);
            }
        }

        public void BeforeSceneLoad()
        {
            try { ClearPool(); mPoolReceiveObj = false; }
            catch (Exception error) { Debug.LogException(error); }
        }
        public void AfterSceneLoad() => mPoolReceiveObj = true;
        public void OnLowMemory() => ClearPool();

        private CharacterPartPoolEntry GetPart(string path, Transform parent, bool usePool, Action<GameObject> onBeforeSetParent = null)
        {
            usePool = usePool && UsePool;
            CharacterPartPoolEntry entry = null;
            bool reused = usePool && TryGetFromPool(path, out entry);
            if (reused)
            {
                var go = entry.GoPart; onBeforeSetParent?.Invoke(go);
                go.transform.SetParent(parent); go.transform.localPosition = Vector3.zero;
                bindings.PrepareRent(go, parent); go.SetActive(true);
                entry.ResetAliveTime(); return entry;
            }
            var handle = GetHandle(path);
            if (!handle.Result) { Debug.LogError("CharacterPartEntryProvider GetPart prefab为空:" + path); return null; }
            var instance = bindings.Instantiate(handle, parent);
            onBeforeSetParent?.Invoke(instance); instance.transform.localPosition = Vector3.zero;
            bindings.PrepareRent(instance, parent);
            mDicInstanceCounts[path]++;
            return new CharacterPartPoolEntry(instance, path, handle, usePool);
        }

        private IProfessionCharacterPartPrefab GetHandle(string path)
        {
            if (mDicHandleCache.TryGetValue(path, out var value)) return value;
            value = bindings.LoadPrefab(path); mDicHandleCache.Add(path, value);
            if (!mDicInstanceCounts.ContainsKey(path)) mDicInstanceCounts.Add(path, 0);
            return value;
        }
        private bool TryGetFromPool(string path, out CharacterPartPoolEntry entry)
        {
            if (mDicEntryPool.TryGetValue(path, out var list) && list.Count > 0)
            { int index = list.Count - 1; entry = list[index]; list.RemoveAt(index); return true; }
            entry = null; return false;
        }
        private void TryClearBaseSkeletonFollowerState(GameObject go)
        {
            if (!go) return;
            mTempListFollowers.Clear(); go.GetComponentsInChildren(true, mTempListFollowers);
            foreach (var follower in mTempListFollowers) if (follower) follower.Clear();
        }
        public void Dispose()
        {
            Application.lowMemory -= OnLowMemory;
            ClearPool();
            if (mPoolRoot)
            {
                if (Application.isPlaying) Object.Destroy(mPoolRoot.gameObject); else Object.DestroyImmediate(mPoolRoot.gameObject);
                mPoolRoot = null;
            }
        }
    }
}
