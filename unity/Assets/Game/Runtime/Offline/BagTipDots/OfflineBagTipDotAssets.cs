using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Offline.BagTipDots
{
    public sealed class OfflineBagTipDotAssets : ScriptableObject
    {
        public TipDotNodeGraph Graph;
        public string GraphName, GraphPath;
        public TipDotGraphType GraphType;
        public TipDotGraphScope GraphScope;
        public int[] EquipTypes, ShowCanUseRedDotItemTypes, NewDayRefreshRedDotItemType;
        public static OfflineBagTipDotAssets LoadDefault()
        {
            var asset = Resources.Load<OfflineBagTipDotAssets>("Home/BagTipDots/Assets");
            if (!asset || !asset.Graph || asset.Graph.name != asset.GraphName || asset.EquipTypes == null
                || asset.ShowCanUseRedDotItemTypes == null || asset.NewDayRefreshRedDotItemType == null)
                throw new InvalidOperationException("The complete original Bag graph/configuration is unbound.");
            return asset;
        }
        public IEnumerable<PartnerTipDotGraphInfo> Definitions()
        {
            yield return new PartnerTipDotGraphInfo { GraphName = GraphName, GraphType = GraphType, GraphScope = GraphScope };
        }
        public TipDotNodeGraph LoadGraph(string name) => name == GraphName ? Graph : null;
    }
}
