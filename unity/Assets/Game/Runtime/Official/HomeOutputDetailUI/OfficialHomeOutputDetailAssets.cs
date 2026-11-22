using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zjcs.Official.HomeOutputDetailUI
{
    public sealed class OfficialHomeOutputDetailAssets : ScriptableObject
    {
        [Serializable] sealed class TextRow { public string key, text; }
        [Serializable] sealed class TextData { public TextRow[] texts; }
        public GameObject popup;
        public TextAsset texts;
        Dictionary<string, string> localized;

        public static OfficialHomeOutputDetailAssets LoadDefault()
        {
            var assets = Resources.Load<OfficialHomeOutputDetailAssets>("Home/OfficialHomeOutputDetailAssets");
            if (!assets || !assets.popup || !assets.texts)
                throw new InvalidOperationException("The original output-addition detail assets are unavailable.");
            return assets;
        }

        public string Text(string key)
        {
            if (localized == null)
            {
                localized = new Dictionary<string, string>(StringComparer.Ordinal);
                var data = JsonUtility.FromJson<TextData>(texts.text);
                if (data?.texts == null) throw new InvalidOperationException("The original output-detail texts are invalid.");
                foreach (var row in data.texts) localized.Add(row.key, row.text);
            }
            return localized.TryGetValue(key, out var value) ? value : key;
        }
    }
}
