using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.Character
{
    /// <summary>
    /// Original <c>CharacterUpgradePanel</c>: current/next sub-rank, star division, level limits,
    /// base property preview, rank-up materials and the limit states of the upgrade button.
    /// </summary>
    public sealed class CharacterRankPanel : MonoBehaviour
    {
        private static readonly string[] QualityBackgrounds =
        {
            "ImgQuality_White-031a03cab0.png", "ImgQuality_White-031a03cab0.png",
            "ImgQuality_Blue-42b8cb9630.png", "ImgQuality_Purple-0b40a53021.png",
            "ImgQuality_Orange-78887320b0.png", "ImgQuality_Gold-5f018c42e0.png",
            "ImgQuality_Red-caa09e6943.png", "ImgQuality_Multicolour-93374ebc65.png"
        };

        private OfficialCharacterAssetCatalog assets;
        private OfflineCharacterProgressionService progression;
        private CharacterPanelNodeFactory factory;
        private OfficialCharacterPanelNode template;
        private Dictionary<string, object> rankTemplates;
        private readonly Dictionary<string, RectTransform> nodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
        private readonly Dictionary<string, Button> buttons = new Dictionary<string, Button>(StringComparer.Ordinal);
        private GameObject dialog;
        private Text fromText;
        private Text toText;
        private Text levelText;
        private Text levelLimitText;
        private RectTransform starGroup;
        private RectTransform propsSlot;
        private RectTransform materialSlot;
        private Button upgradeButton;
        private GameObject cannotMake;
        private GameObject maxCannotMake;
        private GameObject useItemBg;
        private GameObject useItemTitle;
        private readonly List<GameObject> starItems = new List<GameObject>();
        private readonly List<GameObject> propRows = new List<GameObject>();
        private readonly List<GameObject> materialIcons = new List<GameObject>();
        private Dictionary<string, object> itemTemplate;
        private Dictionary<string, object> rowTemplate;
        private string levelFormat;
        private string limitFormat;

        public event Action Upgraded;
        public string LastMessage { get; private set; } = string.Empty;

        public bool IsOpen => dialog != null;

        public void Initialize(OfficialCharacterAssetCatalog assetCatalog,
            OfflineCharacterProgressionService characterProgression)
        {
            assets = assetCatalog ?? throw new ArgumentNullException(nameof(assetCatalog));
            progression = characterProgression ?? throw new ArgumentNullException(nameof(characterProgression));
            factory = new CharacterPanelNodeFactory(assets);
            Dictionary<string, object> panels = CharacterModelPackage.AsObject(
                OfficialEquipmentJson.Parse(assets.CharacterPanels.text), "panels");
            Dictionary<string, object> screens = CharacterModelPackage.AsObject(panels["screens"], "screens");
            template = CharacterPanelNodeFactory.ReadRoot(
                CharacterModelPackage.AsObject(screens["rank_upgrade"], "rank_upgrade"));
            itemTemplate = CharacterModelPackage.AsObject(screens["item"], "item");
            rowTemplate = CharacterModelPackage.AsObject(screens["character_prop_row"], "character_prop_row");
            levelFormat = ReadTemplateText(template, "/UpgradeLevelGO/LevelPreText", "等级 {0}/{1}");
            limitFormat = ReadTemplateText(template, "/UpgradeLevelGO/Value/LevelPostText", "等级上限开放至{0}级");
            rankTemplates = panels.TryGetValue("bindings", out object bindingRaw)
                && bindingRaw is Dictionary<string, object> bindingMap
                && bindingMap.TryGetValue("rank", out object rankRaw)
                && rankRaw is Dictionary<string, object> rankMap
                && rankMap.TryGetValue("CharacterUpgradePanel", out object panelRaw)
                ? panelRaw as Dictionary<string, object> : null;
            if (rankTemplates == null || (!rankTemplates.ContainsKey("mySubRankUpPropSlot")))
                throw new FormatException("Original rank upgrade panel bindings are missing.");
        }

        public void Open(Transform parent)
        {
            if (dialog != null) return;
            nodes.Clear();
            buttons.Clear();
            dialog = new GameObject("CharacterUpgradePanel", typeof(RectTransform));
            dialog.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)dialog.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            factory.Build(template, rect, nodes, buttons);
            Bind();
            Refresh();
        }

        public void Close()
        {
            if (dialog != null) Destroy(dialog);
            dialog = null;
            nodes.Clear();
            buttons.Clear();
            starItems.Clear();
            propRows.Clear();
            materialIcons.Clear();
        }

        private void Bind()
        {
            fromText = TextAt(String("mySubRankText"));
            toText = TextAt(String("myNextSubRankText"));
            levelText = TextAt(String("mySubRankLevelText"));
            levelLimitText = TextAt(String("mySubRankUnlockLevelText"));
            starGroup = Find(String("StarGroup"));
            propsSlot = Find(String("mySubRankUpPropSlot"));
            materialSlot = Find("/UseItemContent/Bg/MaterialGrid");
            cannotMake = Find("/UseItemContent/CannotMakeObj")?.gameObject;
            maxCannotMake = Find("/UseItemContent/MaxCannotMakeObj")?.gameObject;
            useItemBg = Find("/UseItemContent/Bg")?.gameObject;
            useItemTitle = Find("/UseItemContent/Title")?.gameObject;
            upgradeButton = ButtonEnding("/UseItemBtn");
            if (upgradeButton != null) upgradeButton.onClick.AddListener(Upgrade);
            Button close = ButtonEnding("/CloseBtn");
            if (close != null) close.onClick.AddListener(Close);
            if (starGroup != null)
                foreach (Transform child in starGroup)
                    starItems.Add(child.gameObject);
        }

        private void Refresh()
        {
            if (dialog == null) return;
            OfflinePlayerProgress player = progression.Player;
            int subRank = player.SubRank;
            int baseLimit = BaseLevelLimit(subRank);
            int nextBaseLimit = BaseLevelLimit(subRank + 1);
            Dictionary<string, object> currentRank = RankRecord(subRank);
            Dictionary<string, object> nextRank = RankRecord(subRank + 1);
            bool isMax = nextRank == null || IsMaxSubRank(subRank);
            if (fromText != null) fromText.text = RankName(currentRank);
            if (toText != null)
            {
                toText.gameObject.SetActive(!isMax);
                toText.text = RankName(nextRank);
            }
            RectTransform arrow = Find(String("myNextJinjieArrowGo"));
            if (arrow != null) arrow.gameObject.SetActive(!isMax);
            if (levelText != null)
                levelText.text = levelFormat.Replace("{0}", player.Level.ToString(CultureInfo.InvariantCulture))
                    .Replace("{1}", baseLimit.ToString(CultureInfo.InvariantCulture));
            if (levelLimitText != null)
                levelLimitText.text = isMax ? string.Empty
                    : limitFormat.Replace("{0}", nextBaseLimit.ToString(CultureInfo.InvariantCulture));
            RefreshStars(subRank);
            RefreshProps(subRank, isMax);
            RefreshMaterials(player, nextRank, isMax);
            ApplyLimitState(player, isMax, baseLimit);
            if (maxCannotMake != null) maxCannotMake.SetActive(isMax);
        }

        private void RefreshStars(int subRank)
        {
            if (starGroup == null) return;
            List<int> division = SubRanksOfRank(subRank);
            bool show = division.Count > 1;
            starGroup.gameObject.SetActive(show);
            if (!show) return;
            for (int index = 0; index < starItems.Count; index++)
            {
                bool active = index < division.Count;
                starItems[index].SetActive(active);
                if (!active) continue;
                int target = division[index];
                int delta = target - subRank;
                Transform light = starItems[index].transform.Find("LightImg");
                bool lit = delta <= 1;
                if (light != null) light.gameObject.SetActive(lit);
                if (delta == 1) starItems[index].transform.localScale = Vector3.one * 1.08f;
                else starItems[index].transform.localScale = Vector3.one;
            }
        }

        private void RefreshProps(int subRank, bool isMax)
        {
            if (propsSlot == null) return;
            foreach (GameObject row in propRows) if (row != null) Destroy(row);
            propRows.Clear();
            int level = Math.Max(1, progression.Player.Level);
            IReadOnlyDictionary<int, long> before = progression.Catalog.CalcCharacterBaseBattleProperties(level, subRank);
            IReadOnlyDictionary<int, long> after = isMax ? before
                : progression.Catalog.CalcCharacterBaseBattleProperties(level, subRank + 1);
            foreach (OfficialPropDefinition definition in progression.Catalog.Props.ByCatalog(0))
            {
                long from = CharacterPropFormula.Raw(before, definition.PropType);
                long to = CharacterPropFormula.Raw(after, definition.PropType);
                if (!isMax && from == to) continue;
                GameObject row = PropRow(propsSlot, definition.Name,
                    isMax ? OfficialPropFormat.Value(definition, from)
                        : OfficialPropFormat.Value(definition, from) + "→" + OfficialPropFormat.Value(definition, to));
                if (row != null) propRows.Add(row);
            }
        }

        private void RefreshMaterials(OfflinePlayerProgress player, Dictionary<string, object> nextRank, bool isMax)
        {
            if (materialSlot == null) return;
            foreach (GameObject icon in materialIcons) if (icon != null) Destroy(icon);
            materialIcons.Clear();
            if (isMax || nextRank == null) return;
            if (!(nextRank.TryGetValue("Items", out object itemsRaw) && itemsRaw is Dictionary<string, object> items))
                return;
            foreach (KeyValuePair<string, object> entry in items)
            {
                int itemId = int.Parse(entry.Key, CultureInfo.InvariantCulture);
                long required = Convert.ToInt64(entry.Value, CultureInfo.InvariantCulture);
                long owned = player.Inventory.Where(stack => stack.ItemId == itemId).Sum(stack => stack.Count);
                GameObject icon = ItemIcon(itemId, owned, required);
                if (icon != null) materialIcons.Add(icon);
            }
        }

        private void ApplyLimitState(OfflinePlayerProgress player, bool isMax, int baseLimit)
        {
            bool levelEnough = player.Level >= baseLimit;
            if (upgradeButton != null) upgradeButton.interactable = !isMax && levelEnough;
            if (useItemBg != null) useItemBg.SetActive(!isMax);
            if (useItemTitle != null) useItemTitle.SetActive(!isMax);
            if (cannotMake != null) cannotMake.SetActive(!isMax && !levelEnough);
        }

        private void Upgrade()
        {
            ProgressionResult result = progression.UpgradeSubRank("character-rank-up-" + Guid.NewGuid().ToString("N"));
            LastMessage = result.Message ?? result.Status.ToString();
            if (!result.IsApplied) return;
            Refresh();
            Upgraded?.Invoke();
        }

        private GameObject PropRow(RectTransform parent, string name, string value)
        {
            OfficialCharacterPanelNode node = CharacterPanelNodeFactory.ReadRoot(rowTemplate);
            var rowNodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            var rowButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
            RectTransform rect = factory.Build(node, parent, rowNodes, rowButtons);
            Text nameText = rowNodes.Values.Select(value2 => value2.GetComponent<Text>())
                .FirstOrDefault(text => text != null && text.name == "Key");
            Text valueText = rowNodes.Values.Select(value2 => value2.GetComponent<Text>())
                .FirstOrDefault(text => text != null && text.name == "Text" || text != null && text.name == "Value");
            if (nameText == null || valueText == null)
            {
                Destroy(rect.gameObject);
                return null;
            }
            nameText.text = name;
            valueText.text = value;
            return rect.gameObject;
        }

        private GameObject ItemIcon(int itemId, long owned, long required)
        {
            if (itemTemplate == null) return null;
            OfficialCharacterPanelNode node = CharacterPanelNodeFactory.ReadRoot(itemTemplate);
            var iconNodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            var iconButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
            RectTransform rect = factory.Build(node, materialSlot, iconNodes, iconButtons);
            Dictionary<string, object> item = progression.Catalog.FindItem(itemId);
            int quality = item == null ? 0 : Convert.ToInt32(item["Quality"], CultureInfo.InvariantCulture);
            RectTransform qualityImage = iconNodes.Values.FirstOrDefault(value => value.name == "QualityImg");
            if (qualityImage != null)
            {
                Image image = qualityImage.GetComponent<Image>();
                int index = Mathf.Clamp(quality, 0, QualityBackgrounds.Length - 1);
                if (image != null) image.sprite = assets.FindSprite(
                    "res://assets/official/sprites/" + QualityBackgrounds[index]);
            }
            RectTransform icon = iconNodes.Values.FirstOrDefault(value => value.name == "icon");
            if (icon != null)
            {
                Image image = icon.GetComponent<Image>();
                string uri = progression.Catalog.ItemIconUri(itemId);
                if (image != null) image.sprite = string.IsNullOrEmpty(uri) ? null : assets.FindSprite(uri);
            }
            Text count = iconNodes.Values.Select(value => value.GetComponent<Text>())
                .FirstOrDefault(text => text != null && text.name == "CountText");
            if (count != null) count.text = owned + "/" + required;
            return rect.gameObject;
        }

        private int BaseLevelLimit(int subRank)
        {
            List<int> levels = progression.Catalog.Rows("system_level_limit")
                .Where(row => Integer(row, "SubRank") == subRank)
                .Select(row => Integer(row, "PlayerLevel")).ToList();
            return levels.Count == 0 ? 0 : levels.Max();
        }

        private bool IsMaxSubRank(int subRank)
        {
            return RankRecord(subRank + 1) == null;
        }

        private List<int> SubRanksOfRank(int subRank)
        {
            Dictionary<string, object> current = RankRecord(subRank);
            if (current == null) return new List<int>();
            int rank = Integer(current, "rank");
            return progression.Catalog.Rows("player_subrank")
                .Select(row => Integer(row, "SubRank"))
                .Where(value => RankRecord(value) != null && Integer(RankRecord(value), "rank") == rank)
                .OrderBy(value => value).ToList();
        }

        private Dictionary<string, object> RankRecord(int subRank)
        {
            Dictionary<string, object> overview = CharacterModelPackage.AsObject(
                OfficialEquipmentJson.Parse(assets.CharacterOverview.text), "overview");
            if (!(overview.TryGetValue("ranks", out object ranksRaw)
                && ranksRaw is Dictionary<string, object> ranks)) return null;
            return ranks.TryGetValue(subRank.ToString(CultureInfo.InvariantCulture), out object value)
                ? value as Dictionary<string, object> : null;
        }

        private static string RankName(Dictionary<string, object> record)
        {
            if (record == null) return string.Empty;
            string name = Convert.ToString(record["name"], CultureInfo.InvariantCulture);
            string sub = Convert.ToString(record["sub"], CultureInfo.InvariantCulture);
            return string.IsNullOrEmpty(sub) ? name : name + "·" + sub;
        }

        private string String(string key)
        {
            return rankTemplates != null && rankTemplates.TryGetValue(key, out object value)
                ? Convert.ToString(value, CultureInfo.InvariantCulture) : string.Empty;
        }

        private RectTransform Find(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            if (nodes.TryGetValue(path, out RectTransform exact)) return exact;
            return nodes.Where(pair => pair.Key.EndsWith(path, StringComparison.Ordinal))
                .Select(pair => pair.Value).FirstOrDefault();
        }

        private Text TextAt(string path)
        {
            RectTransform rect = Find(path);
            return rect == null ? null : rect.GetComponent<Text>();
        }

        private Button ButtonEnding(string suffix)
        {
            if (buttons.TryGetValue(suffix, out Button exact)) return exact;
            return buttons.Where(pair => pair.Key.EndsWith(suffix, StringComparison.Ordinal))
                .Select(pair => pair.Value).FirstOrDefault();
        }

        private static string ReadTemplateText(OfficialCharacterPanelNode node, string suffix, string fallback)
        {
            if (node == null) return fallback;
            if (node.path.EndsWith(suffix, StringComparison.Ordinal) && node.text != null)
                return string.IsNullOrEmpty(node.text.value) ? fallback : node.text.value;
            foreach (OfficialCharacterPanelNode child in node.children ?? Array.Empty<OfficialCharacterPanelNode>())
            {
                string value = ReadTemplateText(child, suffix, null);
                if (!string.IsNullOrEmpty(value)) return value;
            }
            return fallback;
        }

        private static int Integer(Dictionary<string, object> row, string key)
        {
            return row.TryGetValue(key, out object value)
                ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : 0;
        }
    }
}
