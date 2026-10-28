using System;
using UnityEngine;
using Zjcs.Offline.World.NativeMap13EntityAssets;
using Zjcs.Official.World.Map13EntityRendering;

namespace Zjcs.Offline.World.NativeMap13EntityViews
{
    public sealed class NativeMap13SlicedSpriteBinding : MonoBehaviour
    {
        public string SourceKey, PackageKey, AssetName;
        public SlicedFilledRender Renderer;
        public void Bind(NativeMap13EntitySpriteLibrary library)
        {
            if (!Renderer) throw new InvalidOperationException("Original sliced renderer target is absent.");
            var sprite = library.Package(PackageKey).LoadAsset<Sprite>(AssetName);
            if (!sprite) throw new InvalidOperationException("Original sliced Sprite is absent: " + SourceKey);
            Renderer.BindOriginalSprite(sprite);
        }
    }
}
