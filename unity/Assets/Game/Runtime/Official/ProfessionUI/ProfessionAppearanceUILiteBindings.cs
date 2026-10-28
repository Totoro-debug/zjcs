using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.ProfessionUI
{
    // Application-owned AppearanceUILite operations, independent of any SDK.
    public interface IProfessionAppearanceUILiteGraphic
    {
        string SkeletonDataName { get; }
        bool allowMultipleCanvasRenderers { get; }
        Texture mainTexture { get; }
        Texture OverrideTexture { get; set; }
        Dictionary<Texture, Texture> CustomTextureOverride { get; }
        Texture[] TexturesMultipleCanvasRenderers { get; }
        void UpdateMesh();
    }
    public interface IProfessionAppearanceUILiteBindings
    {
        AppearanceConfig Config { get; }
        IProfessionAppearanceUIAssetManager AssetManager { get; }
        IProfessionAppearanceCharacterAsset LoadAsset(string path);
    }
    public static class ProfessionAppearanceUILiteBindings
    {
        public static IProfessionAppearanceUILiteBindings Current { get; set; }
        public static IProfessionAppearanceUILiteBindings Required => Current ??
            throw new InvalidOperationException("Original lightweight UI appearance resources are unbound.");
    }
}
