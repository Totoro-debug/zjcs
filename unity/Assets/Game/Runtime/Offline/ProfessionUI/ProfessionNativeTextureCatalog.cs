using System;
using UnityEngine;

namespace Zjcs.Offline.ProfessionUI
{
    // Explicit local payloads selected through the original high-resolution
    // placeholder map. Unconverted pages stay visible as missing bindings.
    public sealed class ProfessionNativeTextureCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Page
        {
            public string Sex, Name, MainGuid, DyeGuid;
            public Texture2D Placeholder, Main, Dye;
        }
        [SerializeField] private Page[] pages = Array.Empty<Page>();
        public int Count => pages.Length;
        public Page At(int index) => pages[index];
        public Page Find(string sex, string page)
        {
            foreach (var entry in pages) if (entry.Sex == sex && entry.Name == page) return entry;
            throw new InvalidOperationException("Original character texture page is not locally bound: " + sex + "/" + page);
        }
        public Texture2D ByGuid(string guid)
        {
            if (string.IsNullOrEmpty(guid)) throw new ArgumentException("Original texture GUID is empty.", nameof(guid));
            foreach (var entry in pages)
            {
                if (entry.MainGuid == guid) return entry.Main ? entry.Main : throw new InvalidOperationException("Original main texture binding is unavailable: " + guid);
                if (entry.DyeGuid == guid) return entry.Dye ? entry.Dye : throw new InvalidOperationException("Original dye texture binding is unavailable: " + guid);
            }
            throw new InvalidOperationException("Original character texture GUID is not locally bound: " + guid);
        }
    }
}
