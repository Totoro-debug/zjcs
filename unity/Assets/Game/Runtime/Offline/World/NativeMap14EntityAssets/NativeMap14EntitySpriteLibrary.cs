using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;

namespace Zjcs.Offline.World.NativeMap14EntityAssets
{
    public sealed class NativeMap14EntitySpriteLibrary : IDisposable
    {
        private readonly NativeMap14EntitySpriteCatalog catalog;
        private readonly Dictionary<string, NativeMap14EntitySpriteCatalog.Package> packages;
        private readonly Dictionary<string, NativeMap14EntitySpriteCatalog.SpriteEntry> sprites;
        private readonly Dictionary<string, NativeMap14EntitySpriteCatalog.RendererEntry> renderers;
        private readonly Dictionary<string, AssetBundle> loaded = new Dictionary<string, AssetBundle>();
        private readonly Dictionary<string, Sprite> cached = new Dictionary<string, Sprite>();
        private bool disposed;
        public NativeMap14EntitySpriteLibrary(NativeMap14EntitySpriteCatalog catalog)
        {
            this.catalog = catalog ? catalog : throw new ArgumentNullException(nameof(catalog));
            if (catalog.manifest != "871_156598") throw new InvalidDataException("Original Map14 sprite version differs.");
            packages = catalog.packages.ToDictionary(p => p.key);
            sprites = catalog.sprites.ToDictionary(s => s.key);
            renderers = catalog.renderers.ToDictionary(r => r.sourceKey);
        }
        public Sprite ForRenderer(string sourceKey)
        {
            if (disposed) throw new ObjectDisposedException(nameof(NativeMap14EntitySpriteLibrary));
            if (!renderers.TryGetValue(sourceKey, out var renderer)) throw new InvalidDataException("Original SpriteRenderer identity is absent: " + sourceKey);
            return string.IsNullOrEmpty(renderer.spriteKey) ? null : Require(renderer.spriteKey);
        }
        public Sprite Require(string key)
        {
            if (disposed) throw new ObjectDisposedException(nameof(NativeMap14EntitySpriteLibrary));
            if (cached.TryGetValue(key, out var value)) return value;
            if (!sprites.TryGetValue(key, out var entry)) throw new InvalidDataException("Original sprite identity is absent: " + key);
            value = Package(entry.package).LoadAsset<Sprite>(entry.assetName);
            if (!value || value.name != entry.originalName) throw new InvalidDataException("Original packed sprite failed to load: " + key);
            cached.Add(key, value); return value;
        }
        public AssetBundle Package(string key)
        {
            if (disposed) throw new ObjectDisposedException(nameof(NativeMap14EntitySpriteLibrary));
            if (loaded.TryGetValue(key, out var bundle)) return bundle;
            if (!packages.TryGetValue(key, out var package) || !package.data) throw new InvalidDataException("Original sprite package is not retained: " + key);
            byte[] bytes = package.data.bytes;
            using (var hash = SHA256.Create())
                if (BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant() != package.sha256)
                    throw new InvalidDataException("Native sprite package bytes differ: " + key);
            bundle = AssetBundle.LoadFromMemory(bytes);
            if (!bundle) throw new InvalidDataException("Native Windows sprite package failed to load: " + key);
            loaded.Add(key, bundle); return bundle;
        }
        public void Dispose()
        {
            if (disposed) return; disposed = true;
            cached.Clear(); foreach (var bundle in loaded.Values) if (bundle) bundle.Unload(true); loaded.Clear();
        }
    }
}
