using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace Zjcs.Presentation.Character
{
    // Keep the recovered font/atlas/material immutable while TMP fills dynamic glyphs.
    internal sealed class OfficialSkillFontLease : MonoBehaviour
    {
        private sealed class SharedFont
        {
            public TMP_FontAsset Font;
            public Material Material;
            public int Users;
        }

        private static readonly Dictionary<TMP_FontAsset, SharedFont> Fonts = new Dictionary<TMP_FontAsset, SharedFont>();
        private TMP_FontAsset sourceFont;
        private SharedFont shared;

        public TMP_FontAsset Acquire(TMP_FontAsset source)
        {
            if (shared != null) return shared.Font;
            if (source == null) throw new InvalidOperationException("Original skill UI font is not bound.");
            if (!Fonts.TryGetValue(source, out shared))
            {
                TMP_FontAsset font = Instantiate(source);
                Material material = Instantiate(source.material);
                var atlases = new Texture2D[source.atlasTextures.Length];
                for (int i = 0; i < atlases.Length; i++)
                    if (source.atlasTextures[i] != null) atlases[i] = Instantiate(source.atlasTextures[i]);
                font.atlasTextures = atlases;
                font.material = material;
                material.mainTexture = atlases[0];
                shared = new SharedFont { Font = font, Material = material };
                Fonts.Add(source, shared);
            }
            sourceFont = source;
            shared.Users++;
            return shared.Font;
        }

        private void OnDestroy()
        {
            if (shared == null) return;
            if (--shared.Users == 0)
            {
                Fonts.Remove(sourceFont);
                foreach (Texture2D texture in shared.Font.atlasTextures)
                    if (texture != null) Release(texture);
                Release(shared.Material);
                Release(shared.Font);
            }
            shared = null;
            sourceFont = null;
        }

        private static void Release(UnityEngine.Object value)
        {
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
