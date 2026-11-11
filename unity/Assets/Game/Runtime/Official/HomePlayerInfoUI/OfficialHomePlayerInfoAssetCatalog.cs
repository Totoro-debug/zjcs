using UnityEngine;
using System;
using System.Collections.Generic;
using TMPro;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Official.HomePlayerInfoUI
{
    public sealed class OfficialHomePlayerInfoAssetCatalog : ScriptableObject
    {
        [SerializeField] private GameObject namePrefab;
        [SerializeField] private GameObject levelPrefab;
        [SerializeField] private GameObject combatRatingPrefab;
        [SerializeField] private TextAsset originalParentContext;
        [SerializeField] private TextAsset localizedTexts;
        [SerializeField] private Font sourceFont;
        [SerializeField] private string sourceFontPath;
        [SerializeField] private TextAsset originalFontConfig;
        [SerializeField] private string defaultFontAsset;
        [SerializeField] private FontEntry[] fontEntries;
        [Serializable] public sealed class FontEntry
        {
            public string Name, FontPath, RawFontPath, Hash;
            public int Length;
        }
        public GameObject NamePrefab => namePrefab;
        public GameObject LevelPrefab => levelPrefab;
        public GameObject CombatRatingPrefab => combatRatingPrefab;
        public TextAsset OriginalParentContext => originalParentContext;
        public TextAsset LocalizedTexts => localizedTexts;
        public FontAssetFontService CreateFontService()
            => new FontAssetFontService(new Fonts(this), DeserializeFontConfig);
        private FontAssetFontConfig DeserializeFontConfig(string json)
        {
            if (!originalFontConfig || json != originalFontConfig.text)
                throw new InvalidOperationException("Packaged original home font configuration differs.");
            var result = new FontAssetFontConfig { DefaultFontAsset = defaultFontAsset,
                FontAssetInfoDict = new Dictionary<string, FontAssetFontConfig.Info>() };
            foreach (var entry in fontEntries)
                result.FontAssetInfoDict.Add(entry.Name, new FontAssetFontConfig.Info {
                    FontPath=entry.FontPath, RawFontPath=entry.RawFontPath, Hash=entry.Hash, Length=entry.Length });
            return result;
        }
        private sealed class Fonts : IProfessionImageResourceLoader
        {
            private readonly OfficialHomePlayerInfoAssetCatalog owner;
            public Fonts(OfficialHomePlayerInfoAssetCatalog owner) { this.owner=owner; }
            public IProfessionAssetOperation<T> LoadAsset<T>(string path) where T : UnityEngine.Object
            {
                UnityEngine.Object result=path==FontAssetFontService.kFontAssetFontConfigPath?owner.originalFontConfig:
                    path==owner.sourceFontPath?owner.sourceFont:
                    path==owner.defaultFontAsset?owner.namePrefab.GetComponent<TMP_Text>().font:null;
                if (!(result is T value)) throw new InvalidOperationException("Packaged original home font resource is unavailable: "+path);
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
            public FontHandle(T value) { this.value=value; }
            public bool IsValid => value;
            public int Status => 1;
            public T Result => value ? value : throw new ObjectDisposedException(nameof(FontHandle<T>));
            public event Action<IProfessionAssetOperation<T>> Completed { add { value?.Invoke(this); } remove { } }
            public void Dispose() { value=null; }
        }
        public static OfficialHomePlayerInfoAssetCatalog LoadDefault()
            => Resources.Load<OfficialHomePlayerInfoAssetCatalog>("Home/OfficialHomePlayerInfoAssetCatalog")
                ?? throw new System.InvalidOperationException("Packaged original home player-info catalog is unavailable.");
    }
}
