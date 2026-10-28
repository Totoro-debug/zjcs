using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Zjcs.Presentation.Character
{
    public sealed partial class CharacterSkillPanel
    {
        private GameObject auxiliaryDialog;
        public event Action<string> NavigationRequested;

        private Dictionary<string, object> AuxiliaryData => CharacterModelPackage.AsObject(skillUiTemplates["auxiliary_data"], "original auxiliary skill data");
        private static Dictionary<string, object> AuxObject(object value) => CharacterModelPackage.AsObject(value, "original auxiliary skill binding");
        private static int AuxInt(Dictionary<string, object> row, string key) => Convert.ToInt32(row[key], CultureInfo.InvariantCulture);

        private void CloseAuxiliary()
        {
            auxiliaryReturn = null;
            CloseSkillVideo();
            if (auxiliaryDialog != null) { auxiliaryDialog.SetActive(false); Destroy(auxiliaryDialog); }
            auxiliaryDialog = null;
            GetComponentInParent<OfficialCharacterPanelView>()?.SetSkillAuxiliaryFullScreen(false);
        }

        private GameObject BuildAuxiliary(string key, out Dictionary<string, RectTransform> windowNodes, out Dictionary<string, Button> windowButtons)
        {
            CloseAuxiliary();
            auxiliaryDialog = BuildOfficialDialog(key, out windowNodes, out windowButtons);
            GetComponentInParent<OfficialCharacterPanelView>()?.SetSkillAuxiliaryFullScreen(
                key == "damage_test" || key == "damage_test_result" || key == "damage_test_report");
            var root = AuxObject(AuxObject(skillUiTemplates[key])["root"]);
            foreach (var node in AuxiliaryNodes(root))
            {
                if (!node.TryGetValue("original_bindings", out object raw) || !AuxObject(raw).TryGetValue("MultiLanguageText", out object language)) continue;
                string path = Convert.ToString(node["path"]);
                if (!windowNodes.TryGetValue(path, out var target)) continue;
                string textKey = Convert.ToString(AuxObject(language)["myKey"]);
                SetAuxiliaryText(target, OriginalDetailText(textKey) ?? textKey);
            }
            foreach (Graphic graphic in auxiliaryDialog.GetComponentsInChildren<Graphic>(true))
                if (graphic is TMP_Text || graphic is Text) graphic.raycastTarget = false;
            return auxiliaryDialog;
        }

        private static IEnumerable<Dictionary<string, object>> AuxiliaryNodes(Dictionary<string, object> root)
        {
            yield return root;
            if (root.TryGetValue("children", out object raw))
                foreach (object child in (List<object>)raw)
                    foreach (var node in AuxiliaryNodes(AuxObject(child))) yield return node;
        }

        private static void SetAuxiliaryText(RectTransform node, string value)
        {
            if (node.TryGetComponent(out TMP_Text tmp)) tmp.text = value;
            else if (node.TryGetComponent(out Text text)) text.text = value;
        }

        private void BindOriginalDetailSources(int skillId, Dictionary<string, object> root, Dictionary<string, RectTransform> detailNodes)
        {
            // ItemSourceDataGachaAgent adds Gacha for every SkillTypes/SkillPieceTypes item.
            // The pinned ItemSourceInfo table contains no extra entries for player skills.
            var binding = FindOriginalComponent(root, "ItemSourceGo");
            RectTransform slot = detailNodes[Convert.ToString(binding["ItemSourceGetSlot"])];
            foreach (Transform child in slot) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            var itemTemplate = AuxObject(skillUiTemplates["skill_source_item"]);
            var itemRoot = AuxObject(itemTemplate["root"]);
            var rowBinding = FindOriginalComponent(itemRoot, "ItemSourceItem");
            var rowNodes = new Dictionary<string, RectTransform>();
            var rowButtons = new Dictionary<string, Button>();
            factory.Build(CharacterPanelNodeFactory.ReadRoot(itemTemplate), slot, rowNodes, rowButtons);
            SetAuxiliaryText(rowNodes[Convert.ToString(rowBinding["TypeText"])], OriginalDetailText("ItemSourceType.Gacha"));
            SetOfficialState("skill_source_item", "ItemSourceItem", "mStateSwitcher", 1, rowNodes);
            foreach (object path in (List<object>)rowBinding["SwitchToBtns"])
                rowNodes[Convert.ToString(path)].GetComponent<Button>().onClick.AddListener(() => {
                    CloseSkillDetail();
                    NavigationRequested?.Invoke("skill-gacha");
                });
        }

        private void OpenBlessLevelRule(int baseLevel, int level, Vector2 screenPosition)
        {
            int season = progression.Catalog.Rows("astrological_season_config")
                .Where(row => AuxInt(row, "Rank") == progression.Player.Rank)
                .Select(row => AuxInt(row, "Season")).DefaultIfEmpty(-1).First();
            string seasonName = OriginalDetailText("astrological_season_" + season + "_name");
            var contents = new[] {
                string.Format(CultureInfo.InvariantCulture, OriginalDetailText("ui_blessing_skill_level_tip_0"), baseLevel, level - baseLevel),
                OriginalDetailText("ui_blessing_skill_level_tip_1"),
                string.Format(CultureInfo.InvariantCulture, OriginalDetailText("ui_blessing_player_level_tip_2"), seasonName),
                OriginalDetailText("ui_blessing_skill_level_tip_3") };
            OpenOriginalRuleTip(OriginalDetailText("ui_blessing_level"), contents, screenPosition);
        }

        private void OpenOriginalRuleTip(string title, IEnumerable<string> contents, Vector2 screenPosition)
        {
            var dialog = BuildAuxiliary("skill_rule_tip", out var tipNodes, out _);
            var root = AuxObject(AuxObject(skillUiTemplates["skill_rule_tip"])["root"]);
            var binding = FindOriginalComponent(root, "PopTipMutilDesc");
            SetAuxiliaryText(tipNodes[Convert.ToString(binding["mTitle"])], title);
            RectTransform slot = tipNodes[Convert.ToString(binding["mContentSlot"])];
            foreach (Transform child in slot) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            var item = AuxObject(skillUiTemplates["skill_rule_tip_item"]);
            var itemBinding = FindOriginalComponent(AuxObject(item["root"]), "TipInfo");
            foreach (string content in contents)
            {
                var itemNodes = new Dictionary<string, RectTransform>();
                factory.Build(CharacterPanelNodeFactory.ReadRoot(item), slot, itemNodes, new Dictionary<string, Button>());
                SetAuxiliaryText(itemNodes[Convert.ToString(itemBinding["mInfoText"])], content);
                itemNodes[Convert.ToString(itemBinding["mImage"])].gameObject.SetActive(true);
            }
            StartCoroutine(PositionOriginalTip(root, tipNodes, screenPosition));
            dialog.AddComponent<OfficialSkillPopupMask>().Show(skillUiTemplates, 2, true, CloseAuxiliary, null);
        }

        private IEnumerator PositionOriginalTip(Dictionary<string, object> root, Dictionary<string, RectTransform> tipNodes, Vector2 screenPosition)
        {
            yield return null;
            var binding = FindOriginalComponent(root, "PopTipNormal");
            var target = tipNodes[Convert.ToString(binding["mTransform"])];
            yield return PositionOriginalTipRect(target, screenPosition);
        }

        private IEnumerator PositionOriginalTipRect(RectTransform target, Vector2 screenPosition)
        {
            if (target == null) yield break;
            var parent = (RectTransform)target.parent;
            Canvas.ForceUpdateCanvases();
            RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screenPosition, null, out Vector2 point);
            // PopTipNormal -> UIUtils.SetRectPopPosInScreen (default yOffset = 10).
            target.anchorMin = target.anchorMax = new Vector2(.5f, .5f);
            bool right = point.x > 0f, top = point.y > 0f;
            target.pivot = new Vector2(right ? 1f : 0f, top ? 1f : 0f);
            Vector2 half = parent.rect.size * .5f;
            point.y += top ? -10f : 10f;
            point.x = Mathf.Clamp(point.x, right ? -half.x + target.rect.width : -half.x,
                right ? half.x : half.x - target.rect.width);
            point.y = Mathf.Clamp(point.y, top ? -half.y + target.rect.height : -half.y,
                top ? half.y : half.y - target.rect.height);
            target.anchoredPosition = point;
        }

        private void BindOriginalPromoteProperties(int skillId, int rank, int level, Dictionary<string, RectTransform> promoteNodes)
        {
            if (detailProperties == null) detailProperties = new OfficialSkillDetailProperties(progression.Catalog, skillAssets);
            var root = AuxObject(AuxObject(skillUiTemplates["skill_promote"])["root"]);
            int subRank = Math.Max(1, progression.Player.SubRank);
            int maxRank = progression.SkillDefinition(skillId).MaximumRank;
            foreach (var node in AuxiliaryNodes(root))
            {
                if (!node.TryGetValue("original_bindings", out object raw) || !AuxObject(raw).TryGetValue("SkillPanelPropPanel", out object panelBinding)) continue;
                bool byRank = Convert.ToString(node["path"]).Contains("/RankUpDetailContent/");
                int digits = AuxInt(AuxObject(panelBinding), "mValidDecimalPlaces");
                bool hasNextLevel = progression.SkillRules.SlotLevelCosts.Any(value => value.Level == Math.Max(1, level));
                var from = detailProperties.Calculate(skillId, rank, Math.Max(1, level), subRank, digits);
                var to = detailProperties.Calculate(skillId, byRank ? Math.Min(rank + 1, maxRank) : rank,
                    byRank || !hasNextLevel ? Math.Max(1, level) : Math.Max(1, level) + 1, subRank, digits);
                var paths = (List<object>)AuxObject(panelBinding)["mSkillPropItems"];
                for (int i = 0; i < paths.Count; i++)
                {
                    string path = Convert.ToString(paths[i]);
                    promoteNodes[path].gameObject.SetActive(i < from.Count);
                    if (i >= from.Count) continue;
                    var rowSource = AuxiliaryNodes(root).First(value => Convert.ToString(value["path"]) == path);
                    var row = FindOriginalComponent(rowSource, "PlayerPropItem");
                    void Text(string field, string value) { if (row.TryGetValue(field, out object rawPath) && rawPath is string key) SetAuxiliaryText(promoteNodes[key], value); }
                    Text("myPropTypeText", from[i].Name);
                    Text("myPropValueText", from[i].Value);
                    Text("PropValueFromText", from[i].Value);
                    Text("PropValueToText", to[i].Value);
                    bool showPreview = !string.Equals(from[i].Value, to[i].Value, StringComparison.Ordinal);
                    if (row.TryGetValue("myPropValueText", out object normal) && normal is string normalPath) promoteNodes[normalPath].gameObject.SetActive(!showPreview);
                    if (row.TryGetValue("myPropPreviewObj", out object preview) && preview is string previewPath) promoteNodes[previewPath].gameObject.SetActive(showPreview);
                }
            }
        }
    }
}
