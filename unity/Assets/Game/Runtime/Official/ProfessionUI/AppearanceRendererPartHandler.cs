using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    // Migrated from the official 884_156692 application, replacing SDK/framework calls with ports.
    public class AppearanceRendererPartHandler : AppearancePartHandler
    {
        [SerializeField] protected Component mSkeletonRenderer;
        [SerializeField] private Transform mSlot;
        [SerializeField] private string mSortingLayer;
        [SerializeField] private int mDefaultSortingOrder;
        public ProfessionPartSharedMaterial OnGetSharedMaterialDelegate;

        private Dictionary<SkinType, string> mLoadedSkinDict = new Dictionary<SkinType, string>();
        private Dictionary<SkinType, AppearanceSkeletonPartEntry> mLoadedSkeletonDict = new Dictionary<SkinType, AppearanceSkeletonPartEntry>();
        private Dictionary<SkinType, AppearanceSkeletonPartEntry> mLoadedSkeletonDictBuffer = new Dictionary<SkinType, AppearanceSkeletonPartEntry>();
        private Dictionary<SkinType, EffectPartEntry> mLoadedEffectDict = new Dictionary<SkinType, EffectPartEntry>();
        private Dictionary<SkinType, EffectPartEntry> mLoadedEffectDictBuffer = new Dictionary<SkinType, EffectPartEntry>();
        private readonly List<BaseSkeletonFollower> mEffectSkeletonFollowers = new List<BaseSkeletonFollower>();
        private ICharacterPartEntryProvider mPartEntryProvider;
        protected IProfessionAppearancePartRenderer PartRenderer => (IProfessionAppearancePartRenderer)mSkeletonRenderer;
        public Dictionary<SkinType, AppearanceSkeletonPartEntry> LoadedSkeletonDict => mLoadedSkeletonDict;

        public void ConfigureRenderer(Component renderer, Transform slot, ICharacterPartEntryProvider provider,
            string sortingLayer, int defaultSortingOrder)
        {
            if (!(renderer is IProfessionAppearancePartRenderer)) throw new ArgumentException("Original scene appearance renderer port is required.", nameof(renderer));
            mSkeletonRenderer = renderer;
            mSlot = slot ? slot : throw new ArgumentNullException(nameof(slot));
            mPartEntryProvider = provider ?? throw new ArgumentNullException(nameof(provider));
            mSortingLayer = sortingLayer;
            mDefaultSortingOrder = defaultSortingOrder;
        }

        private void OnDestroy() { Release(); }
        protected virtual void OnEnable() { }
        protected virtual void OnDisable() { }

        protected override void InternalSetSkins(Dictionary<SkinType, string> skinDict)
        {
            foreach (var item in mLoadedSkeletonDict)
            {
                if (skinDict.TryGetValue(item.Key, out var name) && mLoadedSkinDict[item.Key].Equals(name))
                { mLoadedSkeletonDictBuffer.Add(item.Key, item.Value); continue; }
                mPartEntryProvider.ReturnPart(item.Value.PoolEntry, false);
                mLoadedSkinDict.Remove(item.Key);
            }
            mLoadedSkeletonDict.Clear();
            var skeletonBuffer = mLoadedSkeletonDict;
            mLoadedSkeletonDict = mLoadedSkeletonDictBuffer;
            mLoadedSkeletonDictBuffer = skeletonBuffer;
            foreach (var item in mLoadedEffectDict)
            {
                if (skinDict.TryGetValue(item.Key, out var name) && mLoadedSkinDict[item.Key].Equals(name))
                { mLoadedEffectDictBuffer.Add(item.Key, item.Value); continue; }
                item.Value.Release();
                mLoadedSkinDict.Remove(item.Key);
            }
            mLoadedEffectDict.Clear();
            mEffectSkeletonFollowers.Clear();
            var effectBuffer = mLoadedEffectDict;
            mLoadedEffectDict = mLoadedEffectDictBuffer;
            mLoadedEffectDictBuffer = effectBuffer;
            var config = PartBindings.Config;
            foreach (var item in skinDict)
            {
                if (mLoadedSkeletonDict.ContainsKey(item.Key)) continue;
                if (mLoadedEffectDict.TryGetValue(item.Key, out var existing))
                {
                    var transform = existing.Go.transform;
                    for (int i = 0; i < transform.childCount; i++)
                        if (transform.GetChild(i).TryGetComponent<BaseSkeletonFollower>(out var follower)) mEffectSkeletonFollowers.Add(follower);
                }
                else
                {
                    if (item.Value == "emptySkin") continue;
                    if (config.GetPartConfig(item.Key) != null)
                    {
                        bool usePool = item.Key == SkinType.backDecoration || item.Key == SkinType.pWeapon;
                        var entry = mPartEntryProvider.RentIntegratedPart(item.Key, item.Value, mSlot, usePool, null);
                        if (entry == null)
                        { Debug.LogError($"AppearanceRendererPartHandler.InternalSetSkins 部位加载失败. skinType:{item.Key} skinName:{item.Value}"); continue; }
                        entry.ResetLocalScale();
                        if (entry.GoPart == null) continue;
                        var part = entry.GoPart.GetComponent<IAppearanceSkeletonPart>();
                        if (part == null)
                        {
                            Debug.LogError($"AppearanceRendererPartHandler.InternalSetSkins 部位缺少IAppearanceSkeletonPart. skinType:{item.Key} skinName:{item.Value} assetPath:{entry.AssetPath}");
                            mPartEntryProvider.ReturnPart(entry, false);
                        }
                        else
                        {
                            part.Init(PartRenderer.GameObject);
                            mLoadedSkeletonDict.Add(item.Key, new AppearanceSkeletonPartEntry(part, entry));
                            mLoadedSkinDict.Add(item.Key, item.Value);
                        }
                    }
                    else
                    {
                        if (!PartBindings.HasEffect(item.Value)) continue;
                        var entry = LoadEffect(item.Value);
                        if (entry.Go == null) continue;
                        var transform = entry.Go.transform;
                        for (int i = 0; i < transform.childCount; i++)
                        {
                            if (!transform.GetChild(i).TryGetComponent<BaseSkeletonFollower>(out var follower)) continue;
                            follower.InitFollowTarget(PartRenderer.GameObject);
                            follower.ForceUpdate();
                            mEffectSkeletonFollowers.Add(follower);
                        }
                        mLoadedEffectDict.Add(item.Key, entry);
                        mLoadedSkinDict.Add(item.Key, item.Value);
                    }
                }
            }
            OnSkinUpdate();
        }

        public override void Release(bool tryUnloadAllAssets = false)
        {
            foreach (var item in mLoadedSkeletonDict)
            {
                if (tryUnloadAllAssets && item.Value.Part.SkeletonTarget.TryGetComponent<IProfessionAppearanceUnloadable>(out var unloadable))
                    unloadable.MarkUnloadTextureImmediately();
                mPartEntryProvider.ReturnPart(item.Value.PoolEntry, tryUnloadAllAssets);
            }
            mLoadedSkeletonDict.Clear();
            foreach (var item in mLoadedEffectDict) item.Value.Release(tryUnloadAllAssets);
            mLoadedEffectDict.Clear();
            mLoadedSkinDict.Clear();
            mEffectSkeletonFollowers.Clear();
            if (tryUnloadAllAssets) PartRenderer.Separator?.ClearSharedMaterials();
        }

        protected virtual void OnSkinUpdate()
        {
            UpdateSeparatorSlots();
            var separator = PartRenderer.Separator;
            if (separator != null) separator.ReInitPartRenderers(SortingLayer.NameToID(mSortingLayer), 0, 0, mDefaultSortingOrder);
            else
            {
                separator = PartRenderer.AddSeparator(SortingLayer.NameToID(mSortingLayer), 0, 0, mDefaultSortingOrder);
                separator.OnGetSharedMaterial += OnGetSharedMaterialDelegate;
            }
            PartRenderer.LateUpdateMesh();
            mSlot.SetAsLastSibling();
            foreach (var item in mLoadedSkeletonDict) item.Value.Part.SkeletonFollower.Initialize();
            foreach (var follower in mEffectSkeletonFollowers) follower.Initialize();
        }

        private EffectPartEntry LoadEffect(string skinName)
        {
            string path = ProfessionAppearancePartPaths.GetSkinEffectPath(AppearanceRenderType.Renderer, skinName);
            var handle = PartBindings.LoadPrefab(path);
            var prefab = handle.Result;
            if (prefab == null) { Debug.LogError("prefab为空:" + path); return default; }
            var go = PartBindings.Instantiate(prefab, mSlot, false);
            go.transform.localPosition = Vector3.zero;
            return new EffectPartEntry(go, handle);
        }

        private void UpdateSeparatorSlots()
        {
            PartRenderer.SeparatorSlots.Clear();
            foreach (var item in mLoadedSkeletonDict) TryAddSeparatorSlot(item.Value.Part.SkeletonFollower.SlotName);
            foreach (var follower in mEffectSkeletonFollowers) TryAddSeparatorSlot(follower.SlotName);
            if (DynamicSeparatorSlots.Count <= 0 || PartRenderer.Skeleton == null) return;
            foreach (string name in DynamicSeparatorSlots)
            {
                if (string.IsNullOrEmpty(name)) continue;
                var slot = PartRenderer.Skeleton.FindSlot(name);
                if (slot == null) Debug.LogWarning("动态SeparatorSlot未找到 slot:" + name);
                else if (!PartRenderer.SeparatorSlots.Contains(slot)) PartRenderer.SeparatorSlots.Add(slot);
            }
        }

        protected override void ApplyDynamicSeparatorSlots()
        {
            if (mSkeletonRenderer == null || PartRenderer.Skeleton == null) return;
            UpdateSeparatorSlots();
            var separator = PartRenderer.Separator;
            if (separator != null) separator.ReInitPartRenderers(SortingLayer.NameToID(mSortingLayer), 0, 0, mDefaultSortingOrder);
            else
            {
                separator = PartRenderer.AddSeparator(SortingLayer.NameToID(mSortingLayer), 0, 0, mDefaultSortingOrder);
                separator.OnGetSharedMaterial += OnGetSharedMaterialDelegate;
                mSlot.SetAsLastSibling();
            }
            PartRenderer.LateUpdateMesh();
        }

        private void TryAddSeparatorSlot(string name)
        {
            if (string.IsNullOrEmpty(name)) return;
            var slot = PartRenderer.Skeleton.FindSlot(name);
            if (slot != null && !PartRenderer.SeparatorSlots.Contains(slot)) PartRenderer.SeparatorSlots.Add(slot);
        }
    }
}
