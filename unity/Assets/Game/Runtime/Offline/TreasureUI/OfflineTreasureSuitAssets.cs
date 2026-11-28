using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Offline.ProfessionUI;

namespace Zjcs.Offline.TreasureUI
{
    public sealed class OfflineTreasureSuitAssets : ScriptableObject
    {
        [Serializable] public sealed class TextBinding { public string Key, Value; }
        public GameObject Prefab;
        public TextAsset PropertyConfiguration;
        public OfflineProfessionImageCatalog PropertyImages;
        public TextBinding[] Texts;
        private Dictionary<string,string> texts;
        public static OfflineTreasureSuitAssets LoadDefault()
            => Resources.Load<OfflineTreasureSuitAssets>("Treasure/NativeSuit/OfflineTreasureSuitAssets")
               ?? throw new InvalidOperationException("Original treasure suit resources are unavailable.");
        public string Text(string key)
        {
            if(texts==null){texts=new Dictionary<string,string>();foreach(var row in Texts)texts.Add(row.Key,row.Value);}
            return texts.TryGetValue(key,out var value)?value:key;
        }
    }
}
