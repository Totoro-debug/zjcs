using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Official.WorldUI;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Character
{
    public sealed partial class CharacterSkillPanel
    {
        private Action auxiliaryReturn;

        // GameEndPanelBase -> FightEndReportPanelProvider -> FightEndReportPanel.
        private void OpenOriginalBattleReport(FightBattleCheckpoint checkpoint, int maxRound, Action back)
        {
            var report = FightBattleReportBuilder.Build(checkpoint);
            var dialog = BuildAuxiliary("damage_test_report", out var reportNodes, out _);
            var root = AuxObject(AuxObject(skillUiTemplates["damage_test_report"])["root"]);
            // Keep the popup wrapper alive while its original blur mask prepares.
            // Disabling it would stop Prepare and leave its raycaster disabled.
            var contentRoot = reportNodes[Convert.ToString(root["path"])].gameObject;
            contentRoot.SetActive(false);
            var binding = FindOriginalComponent(root, "FightEndReportPanel");
            var playerBinding = FindOriginalComponent(root, "FightEndReportPlayerTabView");
            var skillBinding = FindOriginalComponent(root, "FightEndReportSkillTabView");
            RectTransform Bound(string field) => reportNodes[Convert.ToString(binding[field])];
            SetAuxiliaryText(Bound("RoundCountText"), string.Format(CultureInfo.InvariantCulture,
                WorldBattleReportTextService.GetText("ui_battle_data_round"), Math.Min(checkpoint.Timeline.Round, maxRound)));
            Bound("CloseBtn").GetComponent<Button>().onClick.AddListener(() => { CloseAuxiliary(); back?.Invoke(); });
            auxiliaryReturn = back;

            var playerSlot = reportNodes[Convert.ToString(playerBinding["PlayerSlot"])];
            foreach (Transform child in playerSlot) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            var records = OfficialBattleReportPlayerRowBinder.PlayerRows(FightBattleReportGrouping.Build(report, 1),
                OfficialCombatAssetCatalog.LoadDefault().LoadReportScoreSettings());
            var rows = new List<WorldBattleReportPlayerRow>();
            var reportFont = dialog.AddComponent<OfficialSkillFontLease>().Acquire(skillAssets.NativeUiFont);
            foreach (var record in records) {
                var row = Instantiate(Resources.Load<GameObject>("Combat/BattleReport/FightEndReportPlayerTabViewItem"), playerSlot, false);
                BindReportFont(row, reportFont);
                var view = row.GetComponent<WorldBattleReportPlayerRow>();
                // The exported PlayerIconMe script reference is unavailable in
                // Windows. Keep its exact prefab GameObject as the local provider target.
                if (view.PlayerIcon == null) view.PlayerIcon = row.transform.Find("PublicPlayerIcon");
                if (view.PlayerIcon == null)
                    throw new InvalidOperationException("The original report player icon binding is missing.");
                rows.Add(view);
            }
            OfficialBattleReportPlayerRowBinder.InitRows(rows, records, 1, (icon, id) => BindReportPlayerIcon(icon));
            var skillRows = report.Skills.Where(value => value.UnitId == 1).ToArray();
            var scrollBinding = FindOriginalComponent(root, "LoopVerticalScrollRect");
            var prefab = Resources.Load<GameObject>("Combat/BattleReport/FightEndReportSkillTabViewItem");
            reportNodes[Convert.ToString(AuxObject(scrollBinding["DefaultPrefabSource"])["Prefab"])] = prefab.GetComponent<RectTransform>();
            var scrollNode = reportNodes[Convert.ToString(skillBinding["mItemScrollRect"])];
            var scroll = scrollNode.gameObject.AddComponent<LoopVerticalScrollRect>();
            OfficialSkillComponentBindings.Apply(scroll, scrollBinding, reportNodes);
            var source = dialog.AddComponent<OfficialSkillEntryDataSource>();
            source.Bind = (item, index) => {
                BindReportFont(item.gameObject, reportFont);
                var skill = skillRows[index];
                item.GetComponent<WorldBattleReportSkillRow>().Refresh(OfficialBattleReportSkillRowBinder.Values(skill), false, slot => {
                    foreach (Transform old in slot) { old.gameObject.SetActive(false); Destroy(old.gameObject); }
                    var owner = checkpoint.Units.First(value => value.Id == skill.UnitId);
                    var definition = owner.Skills.Concat(owner.ReportSkillSnapshots ?? new List<FightBattleSkillDefinition>())
                        .FirstOrDefault(value => value.ItemId == skill.SkillItemId);
                    var icon = BuildSkillIcon(skill.SkillItemId, slot, "ReportSkillIcon", definition?.Rank ?? 0, definition?.SkillSkinId ?? 0);
                    var iconButton = icon.GetComponent<Button>();
                    if (iconButton != null) {
                        iconButton.enabled = true;
                        if (iconButton.targetGraphic != null) iconButton.targetGraphic.raycastTarget = true;
                    }
                    foreach (Button button in slot.GetComponentsInChildren<Button>(true)) {
                        button.onClick.RemoveAllListeners();
                        if (definition != null) button.onClick.AddListener(() => OpenSnapshotSkillDetail(definition));
                    }
                });
            };
            scroll.dataSource = source;
            scroll.totalCount = skillRows.Length;
            var titleNode = reportNodes[Convert.ToString(skillBinding["mTitleScrollRect"])];
            var titleScroll = titleNode.GetComponent<ScrollRect>() ?? titleNode.gameObject.AddComponent<ScrollRect>();
            var titleSource = AuxiliaryNodes(root).Single(value => Convert.ToString(value["path"]) == Convert.ToString(skillBinding["mTitleScrollRect"]));
            OfficialSkillComponentBindings.Apply(titleScroll, AuxObject(AuxObject(titleSource["original_bindings"])["ScrollRect"]), reportNodes);
            var syncNode = reportNodes[Convert.ToString(binding["SkillTabView"])];
            var sync = syncNode.gameObject.AddComponent<WorldBattleReportSkillScrollSync>();
            OfficialSkillComponentBindings.Apply(sync, skillBinding, reportNodes);
            sync.SetGMState(false);
            SetOfficialState("damage_test_report", "FightEndReportSkillTabView", "mShowEquipTermReportSwitcher", 0, reportNodes);
            reportNodes[Convert.ToString(skillBinding["mTabGroupGO"])].gameObject.SetActive(report.Units.Any(value => value.CharacterType == 5 && value.Side == 1));
            var tabs = AuxiliaryNodes(root).Where(value => AuxObject(value.TryGetValue("original_bindings", out var raw) ? raw : new Dictionary<string, object>()).ContainsKey("FightEndReportPanelTabItem")).ToArray();
            void Select(int tab) {
                SetOfficialState("damage_test_report", "FightEndReportPanel", "TabTypeSwitcher", tab, reportNodes);
                foreach (var node in tabs) {
                    var target = reportNodes[Convert.ToString(node["path"])];
                    bool selected = AuxInt(FindOriginalComponent(node, "FightEndReportPanelTabItem"), "mTabType") == tab;
                    target.Find("Selected")?.gameObject.SetActive(selected);
                    if (target.TryGetComponent(out Toggle toggle)) toggle.SetIsOnWithoutNotify(selected);
                    if (node.TryGetValue("animations", out object animations)) {
                        var clip = ((List<object>)animations).Select(AuxObject).FirstOrDefault(value =>
                            Convert.ToString(value["name"]).Contains(selected ? "_On" : "_Off"));
                        if (clip != null && TryBuildAnimationTracks(clip, reportNodes, out var tracks, out float duration))
                            StartCoroutine(PlaySkillAnimation(tracks, duration, false));
                    }
                }
                if (tab == 1) { scroll.RefillCells(); sync.Initialize(); sync.RefreshLayout(); }
            }
            foreach (var node in tabs) {
                int tab = AuxInt(FindOriginalComponent(node, "FightEndReportPanelTabItem"), "mTabType");
                RectTransform target = reportNodes[Convert.ToString(node["path"])];
                var button = target.GetComponent<Button>() ?? target.gameObject.AddComponent<Button>();
                button.onClick.AddListener(() => Select(tab));
            }
            contentRoot.SetActive(true);
            Select(0);
        }

        // SkillDetailDialog.Init(SnapshotSkill): a report opens the generic,
        // read-only prefab with its recorded level, rank, sub-rank and skin.
        private void OpenSnapshotSkillDetail(FightBattleSkillDefinition snapshot)
        {
            CloseSkillDetail();
            detailDialog = BuildOfficialDialog("skill_plain_detail", out var detailNodes, out _);
            int effectId = snapshot.UseItemId > 0 ? snapshot.UseItemId : snapshot.ItemId;
            var item = progression.Catalog.FindItem(effectId);
            SetTextValue(DialogNode(detailNodes, "NameText")?.GetComponent<TMP_Text>(), Convert.ToString(item["Name"]));
            BindOriginalSkillDetail(snapshot.ItemId, detailNodes, false, snapshot, "skill_plain_detail");
            var root = AuxObject(AuxObject(skillUiTemplates["skill_plain_detail"])["root"]);
            var binding = FindOriginalComponent(root, "SkillDetailDialog");
            var slot = detailNodes[Convert.ToString(binding["ItemIconSlot"])];
            var icon = BuildSkillIcon(snapshot.ItemId, slot, "SnapshotSkillIcon", snapshot.Rank, snapshot.SkillSkinId);
            foreach (string name in new[] { "SkillStarInfo", "QualityImg", "BottomText", "ShadowObj" })
                FindDeep(icon.transform, name)?.gameObject.SetActive(false);
            var itemBinding = FindOriginalComponent(root, "ItemDetailDialog");
            foreach (string field in new[] { "ItemCountGo", "ItemSourceObj" })
                if (itemBinding.TryGetValue(field, out var path) && path is string key && detailNodes.TryGetValue(key, out var node))
                    node.gameObject.SetActive(false);
            var share = FindOriginalComponent(root, "ShareItemBtn");
            if (share != null && share.TryGetValue("ShareBtn", out var sharePath) && sharePath is string shareKey)
                detailNodes[shareKey].gameObject.SetActive(false);
        }

        private void BindReportPlayerIcon(Component icon)
        {
            if (icon == null) return;
            foreach (TMP_Text text in icon.GetComponentsInChildren<TMP_Text>(true)) {
                if (text.name.IndexOf("Name", StringComparison.OrdinalIgnoreCase) >= 0) text.text = progression.Player.CharacterName;
                else if (text.name.IndexOf("Level", StringComparison.OrdinalIgnoreCase) >= 0) text.text = progression.Player.Level.ToString(CultureInfo.InvariantCulture);
            }
            var head = icon.GetComponentsInChildren<RectTransform>(true).First(value => value.name == "HeadImg");
            var raw = head.GetComponent<RawImage>() ?? head.gameObject.AddComponent<RawImage>();
            OfficialSkillComponentBindings.Apply(raw, AuxObject(AuxiliaryData["report_portrait"]), new Dictionary<string, RectTransform>());
            // Report windows can open before the shared skill header exists.
            // Photograph this profile instead of selecting a placeholder HeadImg
            // from another window with the same node name.
            var portrait = OfficialCharacterSkillHeader.CaptureOriginalPortrait(progression, profileSlotId, profileSavesRoot);
            icon.gameObject.AddComponent<OfficialSkillScreenshotLease>().Texture = portrait;
            raw.texture = portrait;
            raw.enabled = true;
            for (Transform node = head; node != icon.transform && node != null; node = node.parent)
                node.gameObject.SetActive(true);
        }

        private static void BindReportFont(GameObject row, TMP_FontAsset font)
        {
            foreach (TMP_Text text in row.GetComponentsInChildren<TMP_Text>(true)) {
                text.font = font;
                text.fontSharedMaterial = font.material;
            }
        }

    }

    internal sealed class OfficialSkillScreenshotLease : MonoBehaviour
    {
        public Texture2D Texture;
        private void OnDestroy() { if (Texture != null) Destroy(Texture); }
    }
}
