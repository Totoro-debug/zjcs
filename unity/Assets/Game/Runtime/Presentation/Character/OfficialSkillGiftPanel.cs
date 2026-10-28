using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Presentation.Skills;

namespace Zjcs.Presentation.Character
{
    // AchievementPanelWithTypes / AchievementPanelBase / AchievementPanelItem's two
    // skill data keys. All nodes, states, rows, tabs and icons come from the original prefabs.
    internal sealed class OfficialSkillGiftPanel
    {
        private const string Root = "/CharacterPanelSkillPanel/Content/AchievementPanelWithTypes";
        private readonly Dictionary<string, RectTransform> nodes;
        private readonly Dictionary<string, object> bindings, templates, texts, icons;
        private readonly OfflineCharacterProgressionService progression;
        private readonly OfficialCharacterAssetCatalog assets;
        private readonly OfficialSkillAssetCatalog skillAssets;
        private readonly CharacterPanelNodeFactory factory;
        private readonly OfficialSkillAchievementRules rules;
        private readonly Action<Button, string> animate;
        private readonly Action changed;
        private readonly Action<int> openItem;
        private readonly Dictionary<Button, int> tabs = new Dictionary<Button, int>();
        private readonly List<GameObject> rewardIcons = new List<GameObject>();
        private int group = 33;

        public OfficialSkillGiftPanel(Dictionary<string, RectTransform> sourceNodes, Dictionary<string, object> ui,
            OfflineCharacterProgressionService service, OfficialCharacterAssetCatalog catalog,
            OfficialSkillAssetCatalog skillCatalog, Action<Button, string> tabAnimation, Action onChanged, Action<int> onOpenItem)
        {
            nodes = sourceNodes; templates = ui; progression = service; assets = catalog; skillAssets = skillCatalog;
            bindings = Object(ui["gift_bindings"]); texts = Object(ui["gift_texts"]); icons = Object(ui["gift_item_icons"]);
            factory = new CharacterPanelNodeFactory(assets); rules = new OfficialSkillAchievementRules(service.SkillRules);
            animate = tabAnimation; changed = onChanged;
            openItem = onOpenItem;
            foreach (var entry in bindings)
            {
                var components = Object(entry.Value);
                if (!components.TryGetValue("AchievementToggleType", out var raw)) continue;
                var param = Object(Object(raw)["ParamItem"]);
                int type = Integer(param["AchievementType"]);
                if (type == 0) continue;
                int selected = type == 4 ? 33 : type == 6 ? 9 : throw new FormatException("Unsupported original skill gift type.");
                Button button = nodes[entry.Key].GetComponent<Button>();
                if (button == null) throw new InvalidOperationException("Original skill achievement tab is unavailable.");
                tabs.Add(button, selected);
                button.onClick.AddListener(() => { group = selected; Refresh(); });
            }
            foreach (string path in Rows())
            {
                var fields = Component(path, "AchievementPanelItem");
                nodes[Text(fields["GetRewardBtn"])].GetComponent<Button>().onClick.AddListener(Claim);
            }
            var scroll = nodes.Values.FirstOrDefault(value => value != null && value.name == "ScrollRect" && value.transform.IsChildOf(nodes[Root]));
            if (scroll != null)
            {
                ScrollRect rect = scroll.GetComponent<ScrollRect>() ?? scroll.gameObject.AddComponent<ScrollRect>();
                rect.viewport = scroll.Find("ViewPort") as RectTransform;
                rect.content = rect.viewport.Find("Grid") as RectTransform;
                rect.horizontal = false; rect.vertical = true;
            }
        }
        public void Show() { group = 33; Refresh(); }
        public void Refresh()
        {
            foreach (GameObject icon in rewardIcons) if (icon != null) UnityEngine.Object.Destroy(icon);
            rewardIcons.Clear();
            foreach (var tab in tabs) animate(tab.Key, tab.Value == group ? "TabBottomBarCell_On 3" : "TabBottomBarCell_Off 3");
            string typeName = group == 33 ? "CostItem_42200" : "SkillScoreCount";
            var panel = Component(Root, "AchievementPanelBase");
            string title = Text(panel["mPanelTitle"]);
            SetText(Text(Component(title, "PanelTitle")["TitleTxt"]), Text(texts["ui_achievement_title_" + typeName]));
            var recorded = new HashSet<string>(progression.Player.AppliedCharacterEventIds, StringComparer.Ordinal);
            var visible = rules.Visible(progression.Player.Skills, group, recorded,
                Integer(panel["mIsShowReceivedItem"]) != 0, Integer(panel["mIsShowProcessingItem"]) != 0);
            ApplyState(panel, "EmptyStateSwitcher", visible.Count == 0 ? 1 : 0);
            long current = rules.Current(progression.Player.Skills, group);
            string[] rows = Rows();
            for (int index = 0; index < rows.Length; index++)
            {
                var node = nodes[rows[index]];
                node.gameObject.SetActive(index < visible.Count);
                if (index >= visible.Count) continue;
                var info = visible[index];
                var fields = Component(rows[index], "AchievementPanelItem");
                SetText(Text(fields["TitleText"]), Text(texts["ui_achievement_content_" + typeName]));
                SetText(Text(fields["ProgressText"]), OfficialPropFormat.Compact(current) + "/" + OfficialPropFormat.Compact(info.RequiredCount));
                int state = recorded.Contains(info.EventId) ? 2 : current >= info.RequiredCount ? 1 : 0;
                ApplyState(fields, "StateSwitcher", state);
                // FunctionLimitUtils.IsAchievementGroupLimited: no server/calendar lock in the confirmed offline policy.
                ApplyState(fields, "mTipDotStateSwitcher", 0);
                RectTransform slot = nodes[Text(fields["RewardSlot"])];
                foreach (OfflineSkillItemCount reward in info.Rewards) CreateReward(slot, reward);
            }
        }
        private void Claim()
        {
            ProgressionResult result = progression.ClaimSkillAchievements(group);
            if (!result.IsApplied) return;
            Refresh(); changed();
        }
        private void CreateReward(RectTransform slot, OfflineSkillItemCount reward)
        {
            var template = CharacterModelPackage.AsObject(templates["item"], "original default item icon");
            var source = CharacterPanelNodeFactory.ReadRoot(template);
            var itemNodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            var itemButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
            RectTransform instance = factory.Build(source, slot, itemNodes, itemButtons);
            rewardIcons.Add(instance.gameObject);
            foreach (Graphic graphic in instance.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
            var target = instance.GetComponent<Image>() ?? instance.gameObject.AddComponent<Image>();
            target.color = Color.clear;
            target.raycastTarget = true;
            target.canvasRenderer.cullTransparentMesh = false;
            var button = instance.GetComponent<Button>() ?? instance.gameObject.AddComponent<Button>();
            button.targetGraphic = target;
            button.onClick.AddListener(() => openItem(reward.ItemId));
            Transform content = instance.Find("Content");
            Image icon = content.Find("mask/icon").GetComponent<Image>();
            icon.sprite = skillAssets.FindSprite(Text(icons[reward.ItemId.ToString(CultureInfo.InvariantCulture)]));
            if (icon.sprite == null) throw new InvalidOperationException("Official skill gift reward icon is unavailable: " + reward.ItemId);
            icon.color = Color.white;
            var definition = progression.Catalog.FindItem(reward.ItemId);
            int quality = definition == null ? 0 : Integer(definition["Quality"]);
            Image qualityImage = content.Find("QualityImg").GetComponent<Image>();
            Sprite qualitySprite = assets.FindQualityBackgroundSprite(quality);
            if (qualitySprite != null) { qualityImage.sprite = qualitySprite; qualityImage.color = Color.white; }
            content.Find("mask/FakeIcon").gameObject.SetActive(false);
            content.Find("Progress").gameObject.SetActive(false);
            content.Find("BottomText").gameObject.SetActive(false);
            content.Find("BgCountText").gameObject.SetActive(reward.Count != 1);
            Text count = content.Find("BgCountText/CountText").GetComponent<Text>();
            count.text = OfficialPropFormat.Compact(reward.Count);
        }
        private string[] Rows() => ((List<object>)Component(Root, "AchievementPanelBase")["mPanelItemList"]).Select(Text).ToArray();
        private Dictionary<string, object> Component(string path, string name) => Object(Object(bindings[path])[name]);
        private void SetText(string path, string value) { var label = nodes[path].GetComponent<Text>(); if (label != null) label.text = value; }
        private void ApplyState(Dictionary<string, object> fields, string name, int selected)
        {
            var switcher = Object(fields[name]);
            var ids = new HashSet<string>(((List<object>)switcher["mStateConfigs"]).Select(value => Text(Object(value)["rid"])));
            var active = new Dictionary<string, bool>();
            foreach (var raw in (List<object>)Object(fields["references"])["RefIds"])
            {
                var reference = Object(raw);
                if (!ids.Contains(Text(reference["rid"]))) continue;
                var data = Object(reference["data"]);
                if (data.TryGetValue("mStateGameObjectData", out var go))
                    foreach (var entryRaw in (List<object>)Object(go)["GoConfig"])
                    {
                        var entry = Object(entryRaw); bool on = Integer(Object(entry["State"])["mValue"]) == selected;
                        foreach (var path in ((List<object>)entry["GameObjects"]).Select(Text))
                            active[path] = (active.TryGetValue(path, out bool previous) && previous) || on;
                    }
                if (data.TryGetValue("mStateColorData", out var colors))
                    foreach (var entryRaw in (List<object>)Object(colors)["GraphicConfigs"])
                    {
                        var entry = Object(entryRaw);
                        var buffer = (List<object>)Object(entry["Color"])["mSerializeBuffer"];
                        var chosen = buffer.Select(Object).FirstOrDefault(value => Integer(Object(value["Key"])["mValue"]) == selected);
                        if (chosen == null) continue;
                        var value = Object(chosen["Value"]);
                        var color = new Color(Single(value["r"]), Single(value["g"]), Single(value["b"]), Single(value["a"]));
                        foreach (var path in ((List<object>)entry["Graphics"]).Select(Text))
                            if (nodes.TryGetValue(path, out var node) && node.GetComponent<Graphic>() is Graphic graphic) graphic.color = color;
                    }
            }
            foreach (var entry in active) if (nodes.TryGetValue(entry.Key, out var target)) target.gameObject.SetActive(entry.Value);
        }
        private static Dictionary<string, object> Object(object value) => CharacterModelPackage.AsObject(value, "official skill achievement binding");
        private static string Text(object value) => Convert.ToString(value, CultureInfo.InvariantCulture);
        private static int Integer(object value) => Convert.ToInt32(value, CultureInfo.InvariantCulture);
        private static float Single(object value) => Convert.ToSingle(value, CultureInfo.InvariantCulture);
    }
}
