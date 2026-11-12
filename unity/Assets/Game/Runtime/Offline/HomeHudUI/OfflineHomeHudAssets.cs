using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.HomeHudUI
{
    // Explicit packaged addresses for the recovered hierarchy and the original
    // FontAssetFontService. This catalog supplies resources, not UI geometry.
    public sealed class OfflineHomeHudAssets : ScriptableObject
    {
        public GameObject framePrefab, homePrefab;
        public TextAsset texts, originalFontConfig;
        public string defaultFontAsset;
        public TMP_FontAsset defaultFont;
        public FontBinding[] fonts;
        public SoundBinding[] sounds;

        [Serializable] public sealed class FontBinding
        {
            public string Name, FontPath, RawFontPath, Hash;
            public int Length;
            public Font source;
        }
        [Serializable] public sealed class SoundBinding { public uint eventId; public AudioClip clip; }
        [Serializable] private sealed class TextData { public TextRow[] texts; }
        [Serializable] private sealed class TextRow { public string key, text; }
        private Dictionary<string, string> localized;

        public string Text(string key)
        {
            if (localized == null)
            {
                localized = new Dictionary<string, string>();
                foreach (var row in JsonUtility.FromJson<TextData>(texts.text).texts)
                    localized.Add(row.key, row.text);
            }
            return localized.TryGetValue(key, out var value) ? value : key;
        }
        public AudioClip Sound(uint eventId)
        {
            foreach (var row in sounds) if (row.eventId == eventId) return row.clip;
            return null;
        }
        public FontAssetFontService CreateFontService()
            => new FontAssetFontService(new FontLoader(this), DeserializeFontConfig);

        private FontAssetFontConfig DeserializeFontConfig(string json)
        {
            if (!originalFontConfig || json != originalFontConfig.text)
                throw new InvalidOperationException("Packaged HomeUI font configuration differs.");
            var result = new FontAssetFontConfig { DefaultFontAsset = defaultFontAsset,
                FontAssetInfoDict = new Dictionary<string, FontAssetFontConfig.Info>() };
            foreach (var row in fonts)
                result.FontAssetInfoDict.Add(row.Name, new FontAssetFontConfig.Info {
                    FontPath = row.FontPath, RawFontPath = row.RawFontPath, Hash = row.Hash, Length = row.Length });
            return result;
        }
        private sealed class FontLoader : IProfessionImageResourceLoader
        {
            private readonly OfflineHomeHudAssets owner;
            public FontLoader(OfflineHomeHudAssets owner) { this.owner = owner; }
            public IProfessionAssetOperation<T> LoadAsset<T>(string path) where T : UnityEngine.Object
            {
                UnityEngine.Object resource = path == FontAssetFontService.kFontAssetFontConfigPath
                    ? owner.originalFontConfig : path == owner.defaultFontAsset ? owner.defaultFont : null;
                if (!resource) foreach (var row in owner.fonts)
                    if (row.FontPath == path) { resource = row.source; break; }
                if (!(resource is T value))
                    throw new InvalidOperationException("Unbound original HomeUI font resource: " + path);
                return new FontHandle<T>(value);
            }
            public IProfessionAssetOperation<T> LoadAssetAsync<T>(string path) where T : UnityEngine.Object => LoadAsset<T>(path);
            public IProfessionAssetOperation<T> LoadAssetByGuid<T>(string guid) where T : UnityEngine.Object
                => throw new InvalidOperationException("The original font service loads configured paths.");
            public IProfessionAssetOperation<T> LoadAssetAsyncByGuid<T>(string guid) where T : UnityEngine.Object => LoadAssetByGuid<T>(guid);
        }
        private sealed class FontHandle<T> : IProfessionAssetOperation<T> where T : UnityEngine.Object
        {
            private T value;
            public FontHandle(T value) { this.value = value; }
            public bool IsValid => value;
            public int Status => 1;
            public T Result => value ? value : throw new ObjectDisposedException(nameof(FontHandle<T>));
            public event Action<IProfessionAssetOperation<T>> Completed { add { value?.Invoke(this); } remove { } }
            public void Dispose() { value = null; }
        }
        public static OfflineHomeHudAssets LoadDefault()
            => Resources.Load<OfflineHomeHudAssets>("Home/OfflineHomeHudAssets")
                ?? throw new InvalidOperationException("The original HomeUI catalog is unavailable.");
    }
}
