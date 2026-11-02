using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    /// <summary>Selected AppearanceAnimUtils UI PlayAnimation default-argument branch.</summary>
    public sealed class OfflineProfessionUiAnimationBindings : IProfessionAppearanceUIBindings
    {
        public IProfessionAppearanceUIAssetManager AssetManager { get; }

        public OfflineProfessionUiAnimationBindings(IProfessionAppearanceUIAssetManager assetManager)
        { AssetManager = assetManager ?? throw new ArgumentNullException(nameof(assetManager)); }

        public bool TryLazyLoadSpineAnimation(IProfessionAppearanceUIGraphic graphic, string name, out object animation)
        {
            animation = null;
            if (graphic == null) return false;
            if (string.IsNullOrEmpty(name))
            { Debug.LogError("TryLazyLoadSpineAnimation animation name is empty."); return false; }
            if (!graphic.skeletonDataAsset) return false;
            if (!(graphic.skeletonDataAsset is ProfessionNativeSceneCharacterAsset asset))
                throw new NotSupportedException("Bind the verified current original UI character asset.");
            // Current native payloads contain every original split animation.
            // No SDK split-loader object, name alias or substitute clip is created.
            var animations = (IReadOnlyDictionary<string, object>)asset.Data.SourceFields["animations"];
            return animations.TryGetValue(name, out animation);
        }

        public void PlayAnimation(IProfessionAppearanceUIGraphic graphic, string name, bool loop)
        {
            if (string.IsNullOrEmpty(name)) return;
            if (!(graphic is ProfessionNativeUiGraphic native))
                throw new ArgumentException("Bind the original UI graphic to its native Canvas endpoint.", nameof(graphic));
            // Original pre-initialization branch. In particular, PlayerAnimationAppearance
            // selects its phase animation BEFORE loading a sex-specific skeleton.
            if (graphic.AnimationState == null)
            { native.startingLoop = loop; native.startingAnimation = name; return; }
            if (!TryLazyLoadSpineAnimation(graphic, name, out _))
            { Debug.LogWarning("Original UI animation is unavailable: " + name); return; }
            native.PlayAnimation(name, loop);
            native.ApplyAnimation();
        }
    }
}
