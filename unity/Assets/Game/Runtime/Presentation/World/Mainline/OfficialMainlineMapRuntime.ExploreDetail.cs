using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Progression;
using Zjcs.Official.WorldUI;
using static Zjcs.Offline.Progression.OfficialWorldExplorationCatalog;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private sealed class ExploreProbability
        {
            public OfficialExploreGroup Outer;
            public float Probability, Pity;
            public IReadOnlyList<OfficialExploreEntityGroup> Candidates;
        }
        private OfficialMap11UiAssets ExploreAssets => Resources.Load<OfficialMap11UiAssets>(NativeUiResource("DynamicItems"));
        private OfficialMap11UiComponent ExploreComponent(string panel, string type, string path = null) =>
            goddessUiData.components.Single(v => v.panel == panel && v.type == type && (path == null || v.path == path));
        private int ExploreQuality(OfficialExploreEntityGroup group) => group.Entitys.Min(v => ExploreService.Catalog.Parameters(v).EntityQuality);
        private static string ExplorePercent(double value) => (Math.Round(value, 6) * 100).ToString("0.0000", CultureInfo.InvariantCulture) + "%";
        private void ExploreQualityText(TMP_Text text, int quality, int theme)
        {
            var source = goddessUiData.qualityTextColors.Single(v => v.ThemeColorType == theme);
            var color = source.TextColors[Array.IndexOf(source.TypeList, quality)];
            var gradient = text.GetComponent<TMPColorGradient>();
            if (color.EnableGradient == 0)
            {
                if (gradient != null) gradient.enabled = false;
                text.color = color.Color;
            }
            else
            {
                // Original MultiLanguageText.SetColor(Gradient) only affects a
                // TMPColorGradient already attached to the original prefab.
                if (gradient == null) return;
                gradient.enabled = true;
                gradient.SetGradientColor(color.GradientColor.Value());
            }
        }
        private void ShowExploreDetail(int tokenId)
        {
            var window = NewExploreWindow("ExploreDetailDialog");
            var source = ExploreFields(window.Key, window.Key);
            var component = ExploreComponent(window.Key, window.Key);
            Action<int, bool> tab = (state, instant) =>
            {
                window.State(component, state);
                foreach (var value in Values(source, "mTabItems").Select(Object))
                {
                    Transform target = window.Find(Text(value, "TabItem"));
                    var animation = target.GetComponent<Animation>();
                    if (animation != null)
                    {
                        string clip = Int(value, "State") == state ? "ToggleOn" : "ToggleOff";
                        animation.Play(clip);
                        if (instant) { animation[clip].normalizedTime = 1; animation.Sample(); }
                    }
                }
            };
            foreach (var value in Values(source, "mTabItems").Select(Object))
            {
                string path = Text(value, "TabItem"); int state = Int(value, "State");
                var button = window.Find(path).GetComponent<Button>() ?? window.Find(path).gameObject.AddComponent<Button>();
                button.onClick.RemoveAllListeners(); button.onClick.AddListener(() => tab(state, false));
            }
            PopulateExploreProbability(window, Text(source, "mExploreProbabilityContent"), tokenId);
            PopulateExploreHistory(window, Text(source, "mExploreHistoryContent"), tokenId);
            tab(0, true); window.Show();
        }
        private void PopulateExploreProbability(OfficialMap11WitchWindow window, string path, int tokenId)
        {
            var context = ExploreContext();
            var token = ExploreService.Catalog.Data.tokenGroups.Single(v => v.ItemId == tokenId);
            var rows = new List<ExploreProbability>();
            for (int i = 0; i < token.ExploreGroups.Length; i++)
            {
                var candidates = ExploreService.Candidates(token.ExploreGroups[i], context);
                if (candidates.Count != 0) rows.Add(new ExploreProbability { Outer = ExploreService.Catalog.Data.exploreGroups.Single(v => v.Id == token.ExploreGroups[i]),
                    Probability = token.ExploreWeights[i], Pity = token.PityProbabilitys[i], Candidates = candidates });
            }
            if (rows.Count == 0) throw new InvalidOperationException("No original exploration probability candidates.");
            float sum = rows.Sum(v => v.Probability), pitySum = rows.Sum(v => v.Pity), used = 0, pityUsed = 0;
            for (int i = 1; i < rows.Count; i++)
            {
                float probability = Mathf.RoundToInt(10000 * rows[i].Probability / sum);
                float pity = Mathf.RoundToInt(10000 * rows[i].Pity / pitySum);
                used += probability; pityUsed += pity;
                rows[i].Probability = probability / 10000; rows[i].Pity = pity / 10000;
            }
            rows[0].Probability = (10000 - used) / 10000; rows[0].Pity = (10000 - pityUsed) / 10000;
            var fields = ExploreFields(window.Key, "ExploreProbabilityContent", path);
            string template = Text(fields, "mExploreProbabilityItem");
            Transform slot = window.Find(Text(fields, "mItemSlot"));
            foreach (Transform child in slot) Destroy(child.gameObject);
            foreach (var group in rows.OrderByDescending(v => v.Outer.GroupId).ThenByDescending(v => v.Probability)
                .ThenByDescending(v => v.Outer.Id).GroupBy(v => v.Outer.GroupId))
            {
                var row = new OfficialMap11WitchWindow(window, template, slot);
                var content = ExploreFields(window.Key, "ExploreProbabilityItem", template);
                float probability = group.Sum(v => v.Probability), pity = group.Sum(v => v.Pity);
                row.SetText(Text(content, "mTitleText"), window.Text("explore_entity_group_title_" + group.Key));
                FormatExploreText(row, Text(content, "mBaseProbabilityText"), ExplorePercent(probability));
                if (content["mPityProbabilityText"] != null) FormatExploreText(row, Text(content, "mPityProbabilityText"), ExplorePercent(pity));
                Transform itemSlot = row.Find(Text(content, "mItemSlot"));
                foreach (Transform child in itemSlot) Destroy(child.gameObject);
                var entries = group.SelectMany(outer => outer.Candidates.Select(candidate => new
                {
                    Group = candidate, Quality = ExploreQuality(candidate),
                    Weight = outer.Outer.Weights[Array.IndexOf(outer.Outer.EntityGroups, candidate.Group)] * outer.Probability
                })).OrderByDescending(v => v.Quality).ThenByDescending(v => v.Group.Id).ToArray();
                float total = entries.Sum(v => v.Weight);
                foreach (var entry in entries)
                {
                    var item = new OfficialMap11WitchWindow("ExploreRewardItem", itemSlot, goddessUiData, MapProfile.NativeUiRoot);
                    string iconPath = Text(ExploreFields(item.Key, item.Key), "mItemIcon");
                    var icon = item.Find(iconPath).GetComponent<WorldItemIcon>();
                    icon.Init(ExploreAssets.ExploreIcon(entry.Group.Icon), ExploreAssets.ExploreQuality(entry.Quality), string.Empty);
                    icon.myTextGo.SetActive(false); icon.SetBottomName(ExplorePercent(entry.Weight / total * probability));
                    var button = icon.GetComponent<Button>() ?? icon.gameObject.AddComponent<Button>();
                    button.enabled = true;
                    button.onClick.AddListener(() => ShowExploreRewardDetail(entry.Group, entry.Quality));
                    item.Show();
                }
                row.Show();
            }
            window.Find(template).gameObject.SetActive(false);
            string titlePath = Text(fields, "mRuleDetailTitle");
            window.SetText(titlePath, window.Text("explore_token_group_rule_title_" + tokenId));
            window.SetText(Text(fields, "mRuleDetailDesc"), ExploreRuleText("explore_token_group_rule_content_" + tokenId));
            var rules = ExploreFields(window.Key, "RuleDialog", Text(fields, "mRuleDialog"));
            string ruleTitle = Text(rules, "mTitle");
            window.SetText(ruleTitle, window.Text("rule_tip_MapExploreRule_0"));
            string tipTemplate = Text(rules, "mTipInfoPrefab");
            var tip = ExploreFields(window.Key, "TipInfo", tipTemplate);
            foreach (var rule in goddessUiData.explorationUi.rule.Where(v => v.Order > 0))
            {
                var item = new OfficialMap11WitchWindow(window, tipTemplate, window.Find(Text(rules, "mTipSlot")));
                item.SetText(Text(tip, "mInfoText"), ExploreRuleText("rule_tip_MapExploreRule_" + rule.Order)); item.Show();
            }
            window.Find(tipTemplate).gameObject.SetActive(false);
        }
        private void ShowExploreRewardDetail(OfficialExploreEntityGroup group, int quality)
        {
            var window = NewExploreWindow("ExploreRewardPreviewItemDetailDialog");
            var fields = ExploreFields(window.Key, window.Key);
            window.SetText(Text(fields, "mTitle"), window.Text("map_entity_group_explore_name_" + group.Id));
            window.SetText(Text(fields, "mDescription"), window.Text("map_entity_group_explore_desc_" + group.Id));
            window.Find(Text(fields, "mIcon")).GetComponent<Image>().sprite = ExploreAssets.ExploreIcon(group.Icon);
            var detail = ExploreFields(window.Key, "ItemDetailDialog", Text(fields, "mItemDetail"));
            if (detail["QualityBg"] != null) window.Find(Text(detail, "QualityBg")).GetComponent<Image>().sprite = ExploreAssets.ExploreQuality(quality, true);
            if (detail["QualityText"] != null)
            {
                var text = window.Find(Text(detail, "QualityText")).GetComponent<TMP_Text>();
                text.text = window.Text("Quality." + new[] { "None", "White", "Blue", "Purple", "Orange", "Gold", "Red", "Rainbow" }[quality]);
                ExploreQualityText(text, quality, 0);
            }
            window.Show();
        }
        private void PopulateExploreHistory(OfficialMap11WitchWindow window, string path, int token)
        {
            ExploreService.Cleanup(ExploreNow);
            var fields = ExploreFields(window.Key, "ExploreHistoryContent", path);
            string template = Text(fields, "mItemPrefab");
            var itemFields = ExploreFields(window.Key, "ExploreHistoryItem", template);
            Transform slot = window.Find(Text(fields, "mSlot"));
            var records = progressionSave.Save.World.Exploration.History.Where(v => v.TokenItemId == token)
                .OrderByDescending(v => v.CreatedAt).ThenByDescending(v => v.EntitySerials.FirstOrDefault()).ToArray();
            int count = Int(fields, "mCountPerPage"), total = Mathf.CeilToInt((float)records.Length / count), page = total > 0 ? 1 : 0;
            var paging = ExploreFields(window.Key, "TurnPageUI", Text(fields, "mTurnPageUI"));
            Action refresh = () =>
            {
                foreach (Transform child in slot) Destroy(child.gameObject);
                foreach (var history in records.Skip(Math.Max(0, page - 1) * count).Take(count))
                {
                    var item = new OfficialMap11WitchWindow(window, template, slot);
                    item.SetText(Text(itemFields, "mGroupText"), window.Text("explore_entity_group_name_" + history.ExploreGroup));
                    item.SetText(Text(itemFields, "mNameText"), window.Text("map_entity_group_explore_name_" + history.Variant));
                    item.SetText(Text(itemFields, "mTimeText"), DateTimeOffset.FromUnixTimeSeconds(history.CreatedAt).LocalDateTime.ToString(window.Text("DateTimeFormat_yyyyMMddHHmmss"), CultureInfo.CurrentCulture));
                    int quality = ExploreQuality(ExploreService.Catalog.Data.entityGroups.Single(v => v.Id == history.Variant));
                    foreach (string field in new[] { "mGroupText", "mNameText", "mTimeText" }) ExploreQualityText(item.Find(Text(itemFields, field)).GetComponent<TMP_Text>(), quality, 3);
                    item.Show();
                }
                FormatExploreText(window, Text(paging, "mCurPageText"), OfficialPropFormat.Compact(page));
                FormatExploreText(window, Text(paging, "mTotalPageText"), OfficialPropFormat.Compact(total));
                window.State(ExploreComponent(window.Key, "TurnPageBtn", Text(paging, "mPrevBtn")), page > 1 ? 1 : 0);
                window.State(ExploreComponent(window.Key, "TurnPageBtn", Text(paging, "mNextBtn")), page < total ? 1 : 0);
            };
            var prev = ExploreFields(window.Key, "TurnPageBtn", Text(paging, "mPrevBtn"));
            var next = ExploreFields(window.Key, "TurnPageBtn", Text(paging, "mNextBtn"));
            window.BindButton(Text(prev, "Btn"), () => { if (page > 1) { page--; refresh(); } });
            window.BindButton(Text(next, "Btn"), () => { if (page < total) { page++; refresh(); } });
            window.Find(template).gameObject.SetActive(false); refresh();
        }
    }
}
