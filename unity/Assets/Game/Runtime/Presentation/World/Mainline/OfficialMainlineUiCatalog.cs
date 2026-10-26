using System;
using System.IO;
using System.Linq;
using UnityEngine;

namespace Zjcs.Presentation.World
{
    /// <summary>The active map's official UI payload, shared by all world modules.</summary>
    internal sealed class OfficialMainlineUiCatalog
    {
        public OfficialMap11UiData Data { get; }

        public OfficialMainlineUiCatalog(OfficialMainlineMapProfile profile)
        {
            if (profile == null) throw new ArgumentNullException(nameof(profile));
            string path = profile.NativeUi("catalog");
            TextAsset source = Resources.Load<TextAsset>(path);
            Data = source == null ? null : JsonUtility.FromJson<OfficialMap11UiData>(source.text);
            if (Data?.texts == null || Data.components == null)
                throw new InvalidDataException("Official world UI catalog is missing or malformed: " + path);
        }

        public string Text(string key)
        {
            OfficialMap11UiText text = Data.texts.FirstOrDefault(value => value.key == key);
            if (text == null) throw new InvalidDataException("Official world UI text is missing: " + key);
            return text.value.Replace("{item_1_name}", Data.texts.Single(value => value.key == "item_1_name").value);
        }
    }
}
