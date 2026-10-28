using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.ProfessionUI
{
    // Packaging decodes the original JSON dictionary into serializable entries.
    // The original service still loads its JSON first; reject any version drift.
    public sealed class OfflineProfessionFontConfig : ScriptableObject
    {
        [Serializable] public sealed class Entry
        {
            public string Name, FontPath, RawFontPath, Hash;
            public int Length;
        }
        public TextAsset OriginalJson;
        public string DefaultFontAsset;
        public Entry[] Entries;
        public FontAssetFontConfig Deserialize(string json)
        {
            if (OriginalJson == null || json != OriginalJson.text || Entries == null)
                throw new InvalidOperationException("Original profession font config differs from its packaged dictionary.");
            var result = new FontAssetFontConfig { DefaultFontAsset = DefaultFontAsset,
                FontAssetInfoDict = new Dictionary<string, FontAssetFontConfig.Info>() };
            foreach (var entry in Entries)
                result.FontAssetInfoDict.Add(entry.Name, new FontAssetFontConfig.Info {
                    FontPath = entry.FontPath, RawFontPath = entry.RawFontPath,
                    Hash = entry.Hash, Length = entry.Length });
            return result;
        }
    }
}
