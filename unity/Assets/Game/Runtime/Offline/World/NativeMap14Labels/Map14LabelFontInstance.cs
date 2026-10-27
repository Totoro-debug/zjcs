using System;
using System.Linq;
using TMPro;
using UnityEngine;
using Zjcs.Official.World.NativeLabelUI;

namespace Zjcs.Offline.World.NativeMap14Labels
{
    // Keep the original font settings/atlas/material while isolating dynamic glyph writes
    // from the source assets in the editor and from subsequent map sessions.
    public sealed class Map14LabelFontInstance : IMultiLanguageFontService, IDisposable
    {
        public TMP_FontAsset Font { get; private set; }
        public Map14LabelFontInstance(TMP_FontAsset source)
        {
            if (!source) throw new ArgumentNullException(nameof(source));
            Font = UnityEngine.Object.Instantiate(source);
            Font.hideFlags = HideFlags.DontSave;
            Font.atlasTextures = source.atlasTextures.Select(texture =>
            {
                var copy = UnityEngine.Object.Instantiate(texture);
                copy.hideFlags = HideFlags.DontSave;
                return copy;
            }).ToArray();
            Font.material = new Material(source.material) { hideFlags = HideFlags.DontSave };
            Font.material.mainTexture = Font.atlasTextures[0];
        }
        public MultiLanguageFontAsset GetMultiLanguageFontAsset() => new MultiLanguageFontAsset(Font);
        public void Dispose()
        {
            if (!Font) return;
            foreach (var texture in Font.atlasTextures) if (texture) UnityEngine.Object.Destroy(texture);
            UnityEngine.Object.Destroy(Font.material);
            UnityEngine.Object.Destroy(Font);
            Font = null;
        }
    }
}
