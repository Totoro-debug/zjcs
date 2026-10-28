using System;
using System.Linq;
using TMPro;
using UnityEngine;

namespace Zjcs.Offline.StoryBubbles
{
    // Keep source assets immutable. An empty Android dynamic atlas can retain
    // serialized glyph records; rebuild those records from the same official TTF.
    public sealed class BubbleFontInstance : IDisposable
    {
        public TMP_FontAsset Font { get; private set; }
        public BubbleFontInstance(TMP_FontAsset source)
        {
            if (!source) throw new ArgumentNullException(nameof(source));
            Font = UnityEngine.Object.Instantiate(source); Font.hideFlags = HideFlags.DontSave;
            Font.atlasTextures = source.atlasTextures.Select(texture =>
            {
                var copy = UnityEngine.Object.Instantiate(texture); copy.hideFlags = HideFlags.DontSave; return copy;
            }).ToArray();
            Font.material = new Material(source.material) { hideFlags = HideFlags.DontSave };
            Font.material.mainTexture = Font.atlasTextures[0];
            Font.ReadFontAssetDefinition();
            if (Font.atlasTextures[0].width == 0 && Font.characterTable.Count != 0)
            {
                var initial = Font.characterTable.Select(c => new { c.unicode, c.glyphIndex, c.glyph.metrics }).ToArray();
                string characters = string.Concat(initial.Select(c => char.ConvertFromUtf32((int)c.unicode)));
                Font.ClearFontAssetData(true);
                if (!Font.TryAddCharacters(characters, out string missing) || !string.IsNullOrEmpty(missing))
                    throw new InvalidOperationException("Original empty-atlas glyph initialization failed: " + missing);
                foreach (var entry in initial)
                {
                    var glyph = Font.characterLookupTable[entry.unicode].glyph;
                    if (glyph.index != entry.glyphIndex || !glyph.metrics.Equals(entry.metrics))
                        throw new InvalidOperationException("Original glyph metrics changed while rebuilding the empty atlas: " + entry.unicode);
                }
                Font.material.mainTexture = Font.atlasTextures[0];
            }
        }
        public void Dispose()
        {
            if (!Font) return;
            foreach (var texture in Font.atlasTextures) if (texture) UnityEngine.Object.Destroy(texture);
            UnityEngine.Object.Destroy(Font.material); UnityEngine.Object.Destroy(Font); Font = null;
        }
    }
}
