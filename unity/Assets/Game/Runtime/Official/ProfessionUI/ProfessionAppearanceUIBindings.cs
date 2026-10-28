using System;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    public interface IProfessionAppearanceAnimation { string Name { get; } }
    public interface IProfessionAppearanceTrack { IProfessionAppearanceAnimation Animation { get; } }
    public interface IProfessionAppearanceAnimationState { IProfessionAppearanceTrack GetCurrent(int track); }
    public interface IProfessionAppearanceUIGraphic : IProfessionAppearanceSkeletonComponent, IProfessionSkinClipGraphic
    {
        bool allowMultipleCanvasRenderers { get; set; }
        ScriptableObject skeletonDataAsset { get; set; }
        string startingAnimation { get; }
        IProfessionAppearanceAnimationState AnimationState { get; }
        Material[] MaterialsMultipleCanvasRenderers { get; }
        Texture[] TexturesMultipleCanvasRenderers { get; }
        event Action<IProfessionAppearanceUIGraphic> OnCustomDemandLoading;
        event Action<int, Mesh[], Material[], Texture[]> AssignMeshOverrideMultipleRenderers;
        void Initialize(bool overwrite);
        void ApplyAnimation();
        void SyncSubmeshGraphicsWithCanvasRenderers();
        void MatchRectTransformWithBounds();
    }
    public interface IProfessionAppearanceUIAssetManager
    {
        void AddTextureLoaderRef(YooAssetTextureLoader loader, UnityEngine.Object owner);
        void RemoveTextureLoaderRef(YooAssetTextureLoader loader, UnityEngine.Object owner);
        Material GetUIMaterial();
    }
    public interface IProfessionAppearanceUIBindings
    {
        IProfessionAppearanceUIAssetManager AssetManager { get; }
        bool TryLazyLoadSpineAnimation(IProfessionAppearanceUIGraphic graphic, string name, out object animation);
        void PlayAnimation(IProfessionAppearanceUIGraphic graphic, string name, bool loop);
    }
    public static class ProfessionAppearanceUIBindings
    {
        public static IProfessionAppearanceUIBindings Current { get; set; }
        public static IProfessionAppearanceUIBindings Required => Current ??
            throw new InvalidOperationException("Original UI appearance assets/animations are unbound.");
    }
}
