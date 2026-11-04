using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.PartnersUI
{
    // A serialized binding beside the original graphic. Runs before original
    // application OnEnable methods so their skin/texture order stays intact.
    [ExecuteAlways, DefaultExecutionOrder(-1000), DisallowMultipleComponent]
    public sealed class PartnerNativeUiActor : MonoBehaviour, IPartnerCombineSkinBindings,
        IPartnerCombineSkeletonComponent, IPartnerLazyUiGraphic, IPartnerSequenceGraphic
    {
        [SerializeField] private ProfessionNativeUiGraphic graphic;
        [SerializeField] private Transform renderRoot;
        private PartnerNativeUiPose pose;
        private bool renderersBound;
        public ProfessionNativeUiGraphic Graphic => graphic;
        public PartnerNativeUiPose Pose => pose;
        public ScriptableObject SkeletonDataAsset => graphic.skeletonDataAsset;
        public IProfessionAppearanceSkeleton Skeleton => graphic.State;
        IPartnerSequenceSkeleton IPartnerSequenceGraphic.Skeleton => pose?.Skeleton;
        public IPartnerSequenceAnimationState AnimationState => pose?.AnimationState;
        public event Action<IPartnerSequenceGraphic> OnAnimationRebuild;
        public IPartnerSequenceAnimation GetOrLoadAnimation(string name) => pose?.GetOrLoadAnimation(name);
        public void UpdateAnimation() => graphic.ApplyAnimation();
        public string StartingAnimation => graphic.startingAnimation;
        public bool StartingLoop => graphic.startingLoop;
        public bool IsValid => graphic.State != null;
        public bool TryGetAnimation(string name) => pose?.GetOrLoadAnimation(name) != null;
        public bool ContainsSplitAnimation(string name) =>
            ((PartnerNativeUiSkeletonAsset)graphic.skeletonDataAsset).ContainsSplitAnimation(name);
        public void PlayAnimation(string name, bool loop) => graphic.PlayAnimation(name, loop);

        private sealed class SkinData : IPartnerCombineSkinData
        {
            private readonly ProfessionNativeSkeletonData data;
            internal SkinData(ProfessionNativeSkeletonData value) { data = value; }
            public IEnumerable<string> SkinNames => data.SkinNames;
            public IProfessionAppearanceSkin DefaultSkin => data.SkinCatalog.FindSkin("default");
            public IProfessionAppearanceSkin FindSkin(string name) => data.SkinCatalog.FindSkin(name);
        }
        private void Awake()
        {
            if (!graphic) throw new InvalidOperationException("Bind the original partner UI graphic.");
            BindRenderers();
            foreach (var item in GetComponentsInChildren<Zjcs.Official.PartnersUI.AppearanceCombineSkin>(true)) item.Configure(this);
            if (graphic.skeletonDataAsset && pose == null) SelectAsset((PartnerNativeUiSkeletonAsset)graphic.skeletonDataAsset);
        }
        private void BindRenderers()
        {
            if (renderersBound) return;
            graphic.BindOriginalCanvasRenderers(renderRoot); renderersBound = true;
        }
        public void SelectAsset(PartnerNativeUiSkeletonAsset asset, bool initialize = true)
        {
            if (!asset) throw new ArgumentNullException(nameof(asset));
            BindRenderers();
            graphic.Release(); pose?.Dispose(); pose = null;
            graphic.skeletonDataAsset = asset;
            pose = new PartnerNativeUiPose(asset, graphic, MaterialForPage, TextureForPage);
            pose.OnAnimationRebuild += _ => OnAnimationRebuild?.Invoke(this);
            graphic.Configure(pose);
            if (initialize) graphic.Initialize(true);
        }
        private Texture TextureForPage(string name)
        {
            return ((PartnerNativeUiSkeletonAsset)graphic.skeletonDataAsset).TextureForPage(name);
        }
        private Material MaterialForPage(string name, int blend)
        {
            TextureForPage(name);
            return blend switch
            {
                0 => graphic.material,
                1 => graphic.additiveMaterial,
                2 => graphic.multiplyMaterial,
                3 => graphic.screenMaterial,
                _ => throw new ArgumentOutOfRangeException(nameof(blend))
            };
        }
        public Component GetGraphic(Component owner) => RequireOwner(owner);
        public Component GetRenderer(Component owner) { RequireOwner(owner); return null; }
        public IPartnerCombineSkeletonComponent GetSkeleton(Component owner) { RequireOwner(owner); return this; }
        public IPartnerCombineAppearanceComponent GetAppearance(Component owner) { RequireOwner(owner); return null; }
        public IPartnerCombineSkinData GetData(ScriptableObject asset, bool quiet)
        {
            if (asset != graphic.skeletonDataAsset || asset is not PartnerNativeUiSkeletonAsset native)
                throw new ArgumentException("Bind the selected original partner UI skeleton.");
            return new SkinData(native.Data);
        }
        public IProfessionAppearanceSkin CreateSkin(string name)
        {
            if (graphic.State == null) throw new InvalidOperationException("Initialize before applying the original combined skin.");
            return graphic.State.CreateSkin(name);
        }
        public void TrimRenderers(Component value)
        { RequireGraphic(value); graphic.ApplyAnimation(); graphic.SyncSubmeshGraphicsWithCanvasRenderers(); }
        public void MatchRectTransformWithBounds(Component value)
        { RequireGraphic(value); graphic.MatchRectTransformWithBounds(); }
        public void UpdateRenderer(Component renderer) => throw new ArgumentException("This original prefab has a Canvas graphic.");
        public int RandomRange(int minimum, int maximum) => UnityEngine.Random.Range(minimum, maximum);
        private ProfessionNativeUiGraphic RequireOwner(Component owner)
        {
            if (!owner || owner.gameObject != graphic.gameObject)
                throw new ArgumentException("Bind the original partner component on this graphic.");
            return graphic;
        }
        private void RequireGraphic(Component value)
        { if (value != graphic) throw new ArgumentException("Expected this exact original partner UI graphic."); }
        private void OnDestroy() { pose?.Dispose(); pose = null; }
    }
}
