using System;
using TMPro;
using UnityEngine;

namespace Zjcs.Presentation.Character
{
    // Keep the recovered font/atlas/material immutable while TMP fills dynamic glyphs.
    internal sealed class OfficialSkillFontLease : MonoBehaviour
    {
        private TMP_FontAsset font;
        private Material material;
        private Texture2D atlas;

        public TMP_FontAsset Acquire(TMP_FontAsset source)
        {
            if (font != null) return font;
            if (source == null) throw new InvalidOperationException("Original skill UI font is not bound.");
            font = Instantiate(source);
            material = Instantiate(source.material);
            atlas = Instantiate(source.atlasTexture);
            font.atlasTextures = new[] { atlas };
            font.material = material;
            material.mainTexture = atlas;
            return font;
        }

        private void OnDestroy()
        {
            if (font != null)
                foreach (Texture2D texture in font.atlasTextures)
                    if (texture != null) Release(texture);
            if (material != null) Release(material);
            if (font != null) Release(font);
        }

        private static void Release(UnityEngine.Object value)
        {
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
