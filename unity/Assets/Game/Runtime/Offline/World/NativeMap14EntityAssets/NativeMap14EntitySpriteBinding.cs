using System;
using UnityEngine;

namespace Zjcs.Offline.World.NativeMap14EntityAssets
{
    // The entity owner binds before activation; transforms, renderer flags,
    // materials and sorting remain on the restored native prefab components.
    public sealed class NativeMap14EntitySpriteBinding : MonoBehaviour
    {
        public string SourceKey;
        public SpriteRenderer Renderer;
        public SpriteMask Mask;
        public void Bind(NativeMap14EntitySpriteLibrary library)
        {
            if ((Renderer ? 1 : 0) + (Mask ? 1 : 0) != 1) throw new InvalidOperationException("Original sprite component target is not unique.");
            var sprite = library.ForRenderer(SourceKey);
            if (Renderer) Renderer.sprite = sprite; else Mask.sprite = sprite;
        }
    }
}
