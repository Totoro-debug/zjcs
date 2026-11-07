using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    // Own the mutable TMP glyph cache. The selected regional font, metrics,
    // source TTF, material parameters and initial atlas pixels stay original.
    public sealed class OfflineUiFontInstance : IDisposable
    {
        public TMP_FontAsset Font { get; private set; }
        private Material material;
        private readonly HashSet<Texture2D> borrowed = new HashSet<Texture2D>();
        private readonly List<Texture2D> copies = new List<Texture2D>();
        private bool disposed;
        public OfflineUiFontInstance(TMP_FontAsset source)
        {
            if (!source || source.atlasTextures == null || source.atlasTextures.Length == 0 || !source.material)
                throw new ArgumentException("An original font, atlas and material are required.", nameof(source));
            if (source.fallbackFontAssetTable != null && source.fallbackFontAssetTable.Count != 0)
                throw new NotSupportedException("This regional font requires owned fallback font instances.");
            foreach (var atlas in source.atlasTextures) borrowed.Add(atlas);
            try
            {
                Font = UnityEngine.Object.Instantiate(source);
                Font.name = source.name; Font.hideFlags = HideFlags.DontSave;
                foreach (var texture in source.atlasTextures)
                {
                    var copy = UnityEngine.Object.Instantiate(texture);
                    copies.Add(copy); copy.name = texture.name; copy.hideFlags = HideFlags.DontSave;
                }
                Font.atlasTextures = copies.ToArray();
                material = UnityEngine.Object.Instantiate(source.material);
                material.name = source.material.name; material.hideFlags = HideFlags.DontSave;
                material.mainTexture = Font.atlasTextures[0]; Font.material = material;
                if (Font.atlasTexture != Font.atlasTextures[0])
                    throw new InvalidOperationException("The regional font retained a shared atlas cache.");
                Font.ReadFontAssetDefinition();
            }
            catch { Dispose(); throw; }
        }
        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            var atlases = Font ? copies.Concat(Font.atlasTextures) : copies;
            foreach (var atlas in atlases.Distinct())
                if (atlas && !borrowed.Contains(atlas)) Release(atlas);
            Release(material); material = null;
            Release(Font); Font = null;
            copies.Clear(); borrowed.Clear();
        }
        private static void Release(UnityEngine.Object value)
        {
            if (!value) return;
            if (Application.isPlaying) UnityEngine.Object.Destroy(value);
            else UnityEngine.Object.DestroyImmediate(value);
        }
    }
}
