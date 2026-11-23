using System;
using Spine.Unity;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.NativeSpine
{
    // Explicit package addresses and original GUIDs, not a replacement skin map.
    public sealed class NativeSpineAppearanceCatalog : ScriptableObject
    {
        [Serializable] public struct AssetBinding { public string Path; public ScriptableObject Asset; }
        [Serializable] public struct TextureBinding { public string Guid; public string Resource; }
        public AppearanceConfig Config;
        public AssetBinding[] Assets;
        public TextureBinding[] Textures;
        public SpineAtlasAsset[] Atlases;
    }
}
