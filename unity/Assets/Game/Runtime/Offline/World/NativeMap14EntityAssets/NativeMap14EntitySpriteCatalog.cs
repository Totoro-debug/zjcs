using System;
using UnityEngine;

namespace Zjcs.Offline.World.NativeMap14EntityAssets
{
    // Explicit source identities for original renderer fields and native Sprite
    // payloads. The Windows packages retain original Sprite/Atlas object bytes.
    public sealed class NativeMap14EntitySpriteCatalog : ScriptableObject
    {
        [Serializable] public sealed class Package
        { public string key, sha256; public TextAsset data; }
        [Serializable] public sealed class SpriteEntry
        { public string key, package, assetName, originalName; }
        [Serializable] public sealed class RendererEntry
        { public string sourceKey, spriteKey, kind; }
        public string manifest;
        public Package[] packages;
        public SpriteEntry[] sprites;
        public RendererEntry[] renderers;
    }
}
