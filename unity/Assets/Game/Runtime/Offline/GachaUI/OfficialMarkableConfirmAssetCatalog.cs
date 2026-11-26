using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Offline.GachaUI
{
    public sealed class OfficialMarkableConfirmAssetCatalog : ScriptableObject
    {
        [SerializeField] private GameObject prefab;
        [SerializeField] private TextAsset texts;
        private Dictionary<string, string> language;
        public GameObject Prefab => prefab;
        public string Text(string key)
        {
            if (language == null)
            {
                language = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (var row in JsonUtility.FromJson<Language>(texts.text).texts) language.Add(row.key, row.text);
            }
            return key != null && language.TryGetValue(key, out var value) ? value : key;
        }
        public static OfficialMarkableConfirmAssetCatalog LoadDefault()
        {
            var value = Resources.Load<OfficialMarkableConfirmAssetCatalog>("PetGacha/OfficialMarkableConfirmAssetCatalog");
            if (!value || !value.prefab || !value.texts)
                throw new InvalidOperationException("Original markable confirmation resources are unbound.");
            return value;
        }
        [Serializable] private class Language { public Entry[] texts; }
        [Serializable] private class Entry { public string key, text; }
    }
}
