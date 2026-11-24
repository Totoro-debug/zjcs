using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.HomeTipDots;
using Zjcs.Official.HomeMainUiControls;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeFeatureRuntime
    {
        private OfflineHomeTipDotAssets homeTipAssets;
        private OfflineHomeTipDotServices homeTipServices;

        public OfflineHomeTipDotServices HomeTipServices => homeTipServices;

        private void InitializeHomeTipDots()
        {
            homeTipAssets = OfflineHomeTipDotAssets.LoadDefault();
            homeTipServices = new OfflineHomeTipDotServices(() => profile.Save, session, rules);
        }

        private IEnumerable<PartnerTipDotGraphInfo> CreateAdditionalTipDotGraphs()
        {
            if (bagTipAssets != null)
            {
                foreach (PartnerTipDotGraphInfo info in bagTipAssets.Definitions())
                    yield return info;
            }
            if (homeTipAssets != null)
            {
                foreach (PartnerTipDotGraphInfo info in homeTipAssets.CreateGraphInfos())
                    yield return info;
            }
        }

        private TipDotNodeGraph LoadAdditionalTipDotGraph(string graphName)
        {
            if (bagTipAssets != null && graphName == bagTipAssets.GraphName) return bagTipAssets.Graph;
            return homeTipAssets != null ? homeTipAssets.LoadGraph(graphName) : null;
        }

        private void BindAdditionalTipDotNode(TipDotNodeBase node)
        {
            bagTipServices?.BindNode(node);
            homeTipServices?.BindNode(node);
        }

        public void BindHomeTipNativeTree(GameObject root)
        {
            if (!root || homeTipAssets == null || partnerTipDots == null) return;
            foreach (var tipObj in root.GetComponentsInChildren<TipDotObject>(true))
                tipObj.TipDotSystem = partnerTipDots.System;
            foreach (var linker in root.GetComponentsInChildren<TipDotNodeLinker>(true))
            {
                string graphName = linker.ConfiguredNodeKey.GraphName;
                if (homeTipAssets.ContainsGraph(graphName)) partnerTipDots.Bind(linker);
            }
        }

        private void SynchronizeHomeTipDots()
        {
            bagTipServices?.Synchronize();
            homeTipServices?.Synchronize();
            partnerTipDots?.Flush();
        }

        private void DisposeHomeTipDots()
        {
            homeTipServices?.Dispose();
            homeTipServices = null;
            homeTipAssets = null;
        }
    }
}
