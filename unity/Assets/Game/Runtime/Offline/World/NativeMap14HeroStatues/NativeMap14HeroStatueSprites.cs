using System;
using UnityEngine;
using Zjcs.Offline.World.NativeMap14EntityAssets;

namespace Zjcs.Offline.World.NativeMap14HeroStatues
{
    // The prefab retains original renderer fields. Packed Sprite/Atlas objects
    // are loaded from the verified Windows library before the host activates it.
    public sealed class NativeMap14HeroStatueSprites : MonoBehaviour
    {
        [Serializable] public sealed class Binding
        {
            public Component target;
            public string sourceKey;
        }
        public Binding[] bindings;

        public void Bind(NativeMap14EntitySpriteLibrary library)
        {
            if (library == null) throw new ArgumentNullException(nameof(library));
            foreach (var binding in bindings)
            {
                var sprite = library.ForRenderer(binding.sourceKey);
                if (binding.target is SpriteRenderer renderer) renderer.sprite = sprite;
                else if (binding.target is SpriteMask mask) mask.sprite = sprite;
                else throw new InvalidOperationException("Original sprite renderer binding is absent: " + binding.sourceKey);
            }
        }
    }
}
