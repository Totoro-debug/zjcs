using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.PartnersUI
{
    // Selected 884 application component; native skin operations are explicit ports.
    [ExecuteAlways, DisallowMultipleComponent, DefaultExecutionOrder(-1)]
    public sealed class AppearanceCombineSkin : MonoBehaviour
    {
        public bool WorkOnPlaying = true;
        public bool IsUI, UseRandomSkin;
        public List<string> CombineSkins = new List<string>();
        public Action OnApplySkin, OnPostApplySkin;
        private Component mSkeletonGraphic, mSkeletonRenderer;
        private IPartnerCombineSkeletonComponent mSkeletonComponent;
        private IPartnerCombineAppearanceComponent mAppearanceComponent;
        private bool mInited;
        private IPartnerCombineSkinBindings bindings;
        private IPartnerCombineSkinBindings Bindings => bindings ?? PartnerCombineSkinBindings.Required;
        public bool Init => mInited;
        public void Configure(IPartnerCombineSkinBindings value)
        { bindings = value ?? throw new ArgumentNullException(nameof(value)); }
        private void OnEnable()
        {
            if (mInited) return;
            InitSkeleton();
            if (WorkOnPlaying)
            {
                if (UseRandomSkin) UseAllSkin(true, false);
                if (CombineSkins.Count > 0) ApplyCombineSkinInRuntime();
            }
            mInited = true;
        }
        private void InitSkeleton()
        {
            mSkeletonRenderer = Bindings.GetRenderer(this);
            mSkeletonGraphic = Bindings.GetGraphic(this);
            mSkeletonComponent = Bindings.GetSkeleton(this);
            mAppearanceComponent = Bindings.GetAppearance(this);
            IsUI = mSkeletonGraphic != null;
        }
        public void SetSkin(string skinName)
        { CombineSkins.Clear(); CombineSkins.Add(skinName); ApplyCombineSkinInRuntime(); }
        public void SetSkins(IEnumerable<string> skinNames)
        { CombineSkins.Clear(); CombineSkins.AddRange(skinNames); ApplyCombineSkinInRuntime(); }
        public void AddSkin(string skinName)
        {
            if (CombineSkins.Contains(skinName)) return;
            CombineSkins.Add(skinName); ApplyCombineSkinInRuntime();
        }
        public void ReplaceSkin(string skinName)
        {
            string value = skinName.Split('_')[0];
            for (int i = 0, count = CombineSkins.Count; i < count; i++)
                if (CombineSkins[i].StartsWith(value))
                { CombineSkins[i] = skinName; ApplyCombineSkinInRuntime(); break; }
        }
        public void RemoveSkin(string skinName)
        { if (CombineSkins.Remove(skinName)) ApplyCombineSkinInRuntime(); }
        private void ApplyCombineSkinInRuntime()
        {
            if (mAppearanceComponent != null)
            { mAppearanceComponent.Release(); mAppearanceComponent.SetSkins(CombineSkins); }
            else ApplySkinToSkeleton(CreateNewSkin(CombineSkins));
        }
        private IProfessionAppearanceSkin CreateNewSkin(List<string> skins)
        {
            var skin = Bindings.CreateSkin("combineSkin"); skin.Clear();
            var data = Bindings.GetData(mSkeletonComponent.SkeletonDataAsset, true);
            foreach (string name in skins)
            {
                var part = data.FindSkin(name);
                if (part != null) skin.AddSkin(part);
            }
            return skin;
        }
        public void UseAllSkin(bool isRandom, bool includeDefault)
        {
            var component = Bindings.GetSkeleton(this);
            if (component != null) UseAllSkin(isRandom, includeDefault, component.SkeletonDataAsset, ref CombineSkins, Bindings);
        }
        public static void UseAllSkin(bool isRandom, bool includeDefault, ScriptableObject skeletonDataAsset,
            ref List<string> result, IPartnerCombineSkinBindings bindings = null)
        {
            bindings ??= PartnerCombineSkinBindings.Required;
            var groups = new Dictionary<string, List<string>>();
            foreach (string name in bindings.GetData(skeletonDataAsset, true).SkinNames)
            {
                if ((name == "default" && !includeDefault) || name == "combineSkin") continue;
                string key = name.Split('_')[0];
                if (!groups.TryGetValue(key, out var names)) groups.Add(key, names = new List<string>());
                names.Add(name);
            }
            result.Clear();
            foreach (var item in groups)
                result.Add(item.Value[isRandom ? bindings.RandomRange(0, item.Value.Count) : 0]);
        }
        public void Clear()
        {
            CombineSkins.Clear();
            ApplySkinToSkeleton(Bindings.GetData(mSkeletonComponent.SkeletonDataAsset, true).DefaultSkin);
        }
        private void ApplySkinToSkeleton(IProfessionAppearanceSkin skin)
        {
            var skeleton = mSkeletonComponent.Skeleton;
            skeleton.SetSkin(skin); skeleton.SetToSetupPose();
            if (mSkeletonGraphic)
            { Bindings.TrimRenderers(mSkeletonGraphic); Bindings.MatchRectTransformWithBounds(mSkeletonGraphic); }
            if (mSkeletonRenderer) Bindings.UpdateRenderer(mSkeletonRenderer);
            OnApplySkin?.Invoke();
        }
    }
}
