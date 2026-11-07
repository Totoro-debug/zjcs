using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.Combat
{
    public sealed class OfficialBattleLeaveAssetCatalog : ScriptableObject
    {
        [SerializeField] private GameObject dialogPrefab, maskPrefab;
        [SerializeField] private TextAsset texts;
        [Serializable] private sealed class Texts { public Entry[] texts, offlineTexts; }
        [Serializable] private sealed class Entry { public string key, text; }
        [NonSerialized] private Dictionary<string, string> wording;
        public GameObject DialogPrefab => dialogPrefab;
        public GameObject MaskPrefab => maskPrefab;
        public string Localize(string key)
        {
            if (wording == null)
            {
                wording = new Dictionary<string, string>(StringComparer.Ordinal);
                var content = JsonUtility.FromJson<Texts>(texts.text);
                foreach (Entry row in content.texts)
                    wording.Add(row.key, WorldBattleReportTextService.ProcessText(row.text));
                foreach (Entry row in content.offlineTexts ?? Array.Empty<Entry>())
                    wording[row.key] = WorldBattleReportTextService.ProcessText(row.text);
            }
            return wording.TryGetValue(key, out string value) ? value : key;
        }
        public static OfficialBattleLeaveAssetCatalog LoadDefault()
        {
            var value = Resources.Load<OfficialBattleLeaveAssetCatalog>("Combat/BattleLeave/OfficialBattleLeaveAssetCatalog");
            if (value == null || value.dialogPrefab == null || value.maskPrefab == null || value.texts == null)
                throw new InvalidOperationException("Original battle leave dialog resources are unbound.");
            return value;
        }
    }
}
