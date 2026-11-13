using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.HomeHangUpTips;
using Zjcs.Official.HomeUI;

namespace Zjcs.Offline.HomeHangUpUI
{
    [Serializable]
    public sealed class OfflineHomeHangUpTipConfiguration
    {
        [Serializable] public sealed class Record { public int id; public EFurniture furniture; }
        public string manifest;
        public List<HomeHangUpTipSettings.HangUpTipSetting> settings;
        public float expForLevelUpPercentTip;
        public float directionGuideFocusDuration;
        public List<Record> records;

        public static OfflineHomeHangUpTipConfiguration LoadDefault()
        {
            var asset = Resources.Load<TextAsset>("Home/hangup-tips");
            if (!asset) throw new InvalidOperationException("Selected home tip configuration is absent.");
            var value = JsonUtility.FromJson<OfflineHomeHangUpTipConfiguration>(asset.text);
            if (value == null || value.manifest != "884_156692" || value.settings == null || value.records == null)
                throw new InvalidOperationException("Invalid selected home tip configuration.");
            return value;
        }

        public HomeHangUpTipSettings CreateSettings() => new HomeHangUpTipSettings(settings, expForLevelUpPercentTip);
        public IReadOnlyDictionary<EFurniture, List<int>> CreateRecordIds()
        {
            var result = new Dictionary<EFurniture, List<int>>();
            foreach (var record in records)
            {
                if (!result.TryGetValue(record.furniture, out var ids)) result.Add(record.furniture, ids = new List<int>());
                ids.Add(record.id);
            }
            return result;
        }
    }
}
