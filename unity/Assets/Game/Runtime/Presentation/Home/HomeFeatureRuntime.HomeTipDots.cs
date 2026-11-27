using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.HomeTipDots;
using Zjcs.Offline.Progression;
using Zjcs.Offline.HomeExperienceUI;
using Zjcs.Official.HomeExperienceRules;
using Zjcs.Official.HomeMainUiControls;
using Zjcs.Official.PartnersUI;
using Zjcs.Official.ProfessionUI;

namespace Zjcs.Presentation.Home
{
    public sealed partial class HomeFeatureRuntime
    {
        private OfflineHomeTipDotAssets homeTipAssets;
        private OfflineHomeTipDotServices homeTipServices;
        private OfflineHomeTreasureTipBinding treasureTipBinding;
        public OfflineTreasureTipService TreasureTips { get; private set; }

        public OfflineHomeTipDotServices HomeTipServices => homeTipServices;

        private void InitializeHomeTipDots()
        {
            homeTipAssets = OfflineHomeTipDotAssets.LoadDefault();
            homeTipServices = new OfflineHomeTipDotServices(() => profile.Save, session, rules,
                null, () => BlessingLevel, () => context);
            var slots = new OfflineTreasureSlotService(profile, OfflineTreasureSlotCatalog.LoadDefault(),
                OfflineTreasureSlotLevelCatalog.LoadDefault(), ReadTreasureTipGrowth);
            TreasureTips = new OfflineTreasureTipService(profile, OfflineTreasureCatalog.Load(),
                OfflineTreasureTipCatalog.LoadDefault(), slots);
            treasureTipBinding = new OfflineHomeTreasureTipBinding(homeTipServices, TreasureTips, () => profile.Save.Player);
        }

        private TreasureSlotLevelEnvironment ReadTreasureTipGrowth(OfflinePlayerProgress saved)
        {
            EnsureProgression();
            homeExperienceTables ??= OfflineHomeExperienceConfiguration.LoadDefault().CreateTables();
            var calculator = progression.PropertyCalculator;
            var source = new OfflineHomeExperienceEnvironmentSource(() => saved, homeExperienceTables,
                () => rules.IsFunctionLimited(173), candidate => {
                    var snapshot = calculator.Evaluate(candidate, profile.Save.World, Now());
                    return CharacterPropFormula.Value(snapshot.NonBattleProps,
                        calculator.Props.Id("PlayerLevelLimit"), calculator.Props);
                });
            var state = source.Capture();
            var growth = OfflineHomeExperienceProjection.Capture(saved, homeExperienceTables, state);
            return new TreasureSlotLevelEnvironment { BaseLevel = growth.BaseLevel, Level = growth.Level,
                PlayerBlessLevel = growth.GetCurBlessPlayerLevel(), WorldRank = (int)state.PlanesRank,
                ServerRecordSeason = state.ServerRecordSeason, ActiveRecordSeason = state.CurrentRecordData?.Season };
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
            treasureTipBinding?.Synchronize();
            homeTipServices?.Synchronize();
            partnerTipDots?.Flush();
        }

        private void DisposeHomeTipDots()
        {
            treasureTipBinding?.Dispose();
            treasureTipBinding = null;
            TreasureTips = null;
            homeTipServices?.Dispose();
            homeTipServices = null;
            homeTipAssets = null;
        }
    }
}
