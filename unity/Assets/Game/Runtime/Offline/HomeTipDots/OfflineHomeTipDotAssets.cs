using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.HomeTipDots
{
    public sealed class OfflineHomeTipDotAssets : ScriptableObject
    {
        [Serializable]
        public sealed class Entry
        {
            public string GraphName;
            public string GraphPath;
            public TipDotGraphType GraphType;
            public TipDotGraphScope GraphScope;
            public TipDotNodeGraph Graph;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public IReadOnlyList<Entry> Entries => entries;

        public static OfflineHomeTipDotAssets LoadDefault()
        {
            var assets = Resources.Load<OfflineHomeTipDotAssets>("Home/HomeTipDots/Assets");
            if (!assets)
                throw new InvalidOperationException("Home tip-dot catalog asset is missing at Resources/Home/HomeTipDots/Assets.");
            if (assets.entries == null)
                throw new InvalidOperationException("Home tip-dot catalog entries are unbound.");
            foreach (Entry entry in assets.entries)
            {
                if (entry == null || string.IsNullOrEmpty(entry.GraphName) || !entry.Graph)
                    throw new InvalidOperationException("Home tip-dot catalog contains an invalid graph entry.");
            }
            return assets;
        }

        public bool ContainsGraph(string graphName)
            => !string.IsNullOrEmpty(graphName) && entries.Any(entry => entry.GraphName == graphName);

        public TipDotNodeGraph LoadGraph(string graphName)
            => entries.FirstOrDefault(entry => entry.GraphName == graphName)?.Graph;

        public IEnumerable<PartnerTipDotGraphInfo> CreateGraphInfos()
        {
            foreach (Entry entry in entries)
            {
                yield return new PartnerTipDotGraphInfo
                {
                    GraphName = entry.GraphName,
                    GraphType = entry.GraphType,
                    GraphScope = entry.GraphScope
                };
            }
        }
    }
}
