using System;
using System.Linq;
using UnityEngine;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private void InitializeNativeWorldHud()
        {
            var data = JsonUtility.FromJson<OfficialMap11UiData>(Resources.Load<TextAsset>(NativeUiResource("catalog")).text);
            Map10RuntimeWorldInfo world = GetWorldInfo();
            foreach (OfficialMap11TeamPanelBinding binding in data.worldBindings.teamPanels)
                ui.Find("WorldCanvas", binding.path).GetComponent<TeamMiniPanel>().Refresh(
                    world.Type != 0 && world.AllowPrivateTeam, world.AllowPublicTeam, false);
            // No message has been sent when entering the local world. The original
            // cooldown controller owns subsequent countdown rendering.
            foreach (string path in data.worldBindings.quickMessageMasks)
                ui.Find("WorldCanvas", path).GetComponent<ChatQuickMsgEntryPanel>().Refresh(0, 0);
            foreach (string path in data.worldBindings.actionEndButtons)
                ui.Find("WorldCanvas", path).SetActive(false);
            StartCoroutine(CapturePlayerHead(data));
            InitializeWorldTopBar(data);
            InitializePinnedWorldTask(data);
            InitializeWorldHudProviders(data);
            foreach (var tracker in data.components.Where(value => value.panel == "WorldCanvas"
                && (value.type == "ShowMapRequireItemCount" || value.type == "ShowMapRequireTaskProcess"
                    || value.type == "ShowMapRequireDemonInvade" || value.type == "ShowMapArrestProcess")))
                if (!string.IsNullOrEmpty(tracker.fields.Content)) ui.Find("WorldCanvas", tracker.fields.Content).SetActive(false);
            var exp = data.components.Single(value => value.type == "PlayerExpSlider").fields;
            var slider = ui.Find("WorldCanvas", exp.mSlider).GetComponent<UnityEngine.UI.Slider>();
            int level = progression.Player.Level;
            long start = progression.Catalog.CumulativeLevelExperience[level];
            long end = progression.Catalog.CumulativeLevelExperience[Math.Min(level + 1, progression.Catalog.CumulativeLevelExperience.Count - 1)];
            slider.normalizedValue = Mathf.Lerp(exp.mSliderMinValue, exp.mSliderMaxValue,
                end > start ? (float)(progression.Player.Experience - start) / (end - start) : 1);
        }

        private void CreateNativeFirstPayEntry()
        {
            var data = JsonUtility.FromJson<OfficialMap11UiData>(Resources.Load<TextAsset>(NativeUiResource("catalog")).text);
            Transform slot = ui.Find("WorldCanvas",
                "/WorldCanvas/Content/Content/WorldPanel/Anim/Right/EventAndPurchaseEntrys").transform;
            var entry = new OfficialMap11WitchWindow("FirstPayBtn", slot, data, MapProfile.NativeUiRoot);
            firstPayEntry = entry.Root;
            var source = entry.Source("FirstPayIconEntry").fields;
            entry.Find(source.mEntryBtn).GetComponent<UnityEngine.UI.Button>().onClick.AddListener(OpenCommerceFirstPay);
            // The native VerticalLayoutGroup positions the original entry; no
            // replacement art, title or hand-written anchored position is used.
            RefreshCommerceHud();
        }

        private void InitializeWorldHudProviders(OfficialMap11UiData data)
        {
            foreach (var entry in data.components.Where(value => value.panel == "WorldCanvas"))
            {
                var fields = entry.fields;
                // These providers have no active local record yet. Preserve the
                // official null/closed branch instead of the prefab preview.
                if (entry.type == "TeamChallengeIconEntry" || entry.type == "LimitedTimeGiftIconEntry")
                {
                    ui.Find("WorldCanvas", fields.mDisplay).SetActive(false);
                    ui.Find("WorldCanvas", fields.mLayoutElement).GetComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
                }
                else if (entry.type == "DemonInvadeIconEntry" || entry.type == "VoidFissureIconEntry")
                {
                    ApplyWorldHudState(fields, fields.mStateSwitcher, entry.type == "DemonInvadeIconEntry" ? 2 : 0);
                    ui.Find("WorldCanvas", fields.mLayoutElement).GetComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
                }
                else if (entry.type == "FightDemonBattlefieldAutoPushEndNotifyUI")
                    ApplyWorldHudState(fields, fields.StateSwitcher, 0);
                else if (entry.type == "TipDotNodeLinker")
                {
                    ApplyWorldHudState(fields, fields.mTipLevelState, 0);
                    ui.Find("WorldCanvas", fields.mNodeDataText).SetActive(false);
                }
                else if (entry.type == "MapNpcTipHandler")
                    foreach (string path in fields.Guiders) ui.Find("WorldCanvas", path).SetActive(false);
                else if (entry.type == "WorldExploreBtn")
                {
                    // The local map has not entered the Public region state.
                    // WorldExploreBtn.CanShowBtn rejects Private/None regions.
                    ui.Find("WorldCanvas", fields.mCostMoneyContent).SetActive(false);
                    ui.Find("WorldCanvas", fields.mLayoutElement).GetComponent<UnityEngine.UI.LayoutElement>().ignoreLayout = true;
                }
            }
        }

        private void ApplyWorldHudState(OfficialMap11UiComponentFields owner, OfficialMap11StateSwitcher state, int value)
        {
            foreach (var id in state.mStateConfigs)
            {
                var config = owner.references.RefIds.Single(entry => entry.rid == id.rid).data;
                if (config.mStateGameObjectData?.GoConfig == null) continue;
                var rows = config.mStateGameObjectData.GoConfig;
                var enabled = rows.Where(row => row.State.mValue == value).SelectMany(row => row.GameObjects).ToArray();
                foreach (string path in rows.SelectMany(row => row.GameObjects).Distinct())
                    ui.Find("WorldCanvas", path).SetActive(enabled.Contains(path));
            }
        }
    }
}
