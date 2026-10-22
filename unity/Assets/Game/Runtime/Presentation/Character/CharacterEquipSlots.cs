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
    /// Original five character equipment slots (<c>CharacterPanelEquipItem</c>): equipped item icon,
    /// secondary-weapon unlock state and the <c>CharacterPanelEquipListPanel</c> candidate list.
    /// </summary>
    public sealed class CharacterEquipSlots : MonoBehaviour
    {
        private static readonly (int Position, string Node)[] Slots =
        {
            (10, "WeaponEquipItem"), (14, "SecondWeaponEquipItem"), (11, "HelmetEquipItem"),
            (12, "ClothesEquipItem"), (13, "BootEquipItem")
        };

        private OfficialCharacterAssetCatalog assets;
        private OfflineCharacterProgressionService progression;
        private OfficialEquipmentCatalog equipment;
        private OfflineEquipmentPropSource propSource;
        private CharacterPanelNodeFactory factory;
        private Dictionary<string, object> itemTemplate;
        private Dictionary<string, object> rowTemplate;
        private Dictionary<string, object> listTemplate;
        private Dictionary<string, object> compareTemplate;
        private GameObject listDialog;
        private GameObject compareDialog;
        private long compareInstanceId;
        private readonly Dictionary<string, RectTransform> listNodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
        private readonly Dictionary<string, Button> listButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
        private RectTransform listContent;
        private GameObject emptyTip;
        private readonly List<GameObject> rows = new List<GameObject>();
        private int selectedPosition;

        public event Action Changed;
        public string LastMessage { get; private set; } = string.Empty;

        public bool IsOpen => listDialog != null;

        public void Initialize(OfficialCharacterAssetCatalog assetCatalog,
            OfflineCharacterProgressionService characterProgression, OfficialEquipmentCatalog equipmentCatalog)
        {
            assets = assetCatalog ?? throw new ArgumentNullException(nameof(assetCatalog));
            progression = characterProgression ?? throw new ArgumentNullException(nameof(characterProgression));
            equipment = equipmentCatalog;
            propSource = equipment == null ? null : new OfflineEquipmentPropSource(equipment,
                progression.Catalog.RankOfSubRank);
            factory = new CharacterPanelNodeFactory(assets);
            Dictionary<string, object> panels = CharacterModelPackage.AsObject(
                OfficialEquipmentJson.Parse(assets.CharacterPanels.text), "panels");
            Dictionary<string, object> screens = CharacterModelPackage.AsObject(panels["screens"], "screens");
            itemTemplate = CharacterModelPackage.AsObject(screens["item"], "item");
            rowTemplate = CharacterModelPackage.AsObject(screens["equip_row"], "equip_row");
            listTemplate = CharacterModelPackage.AsObject(screens["equipment"], "equipment");
            compareTemplate = CharacterModelPackage.AsObject(screens["equip_compare"], "equip_compare");
            compareTemplate = CharacterModelPackage.AsObject(screens["equip_compare"], "equip_compare");
        }

        public void Refresh(Transform characterRoot)
        {
            if (characterRoot == null) return;
            foreach ((int position, string nodeName) in Slots)
            {
                Transform slot = FindDeep(characterRoot, nodeName);
                if (slot == null) continue;
                RectTransform mount = FindDeep(slot, "Slot") as RectTransform;
                if (mount == null) continue;
                for (int i = mount.childCount - 1; i >= 0; i--) Destroy(mount.GetChild(i).gameObject);
                OfflineEquippedSlot equipped = progression.Player.Equipment.Equipped
                    .FirstOrDefault(value => value.Position == position);
                OfflineEquipmentInstance instance = equipped == null ? null
                    : progression.Player.Equipment.Instances
                        .FirstOrDefault(value => value.InstanceId == equipped.InstanceId);
                if (instance != null)
                {
                    GameObject icon = BuildItemIcon(instance, mount);
                    if (icon != null) icon.transform.SetParent(mount, false);
                }
                GameObject locked = FindDeep(slot, "Lock")?.gameObject;
                if (locked != null) locked.SetActive(!IsUnlocked(position));
                Button button = slot.GetComponentInChildren<Button>(true);
                if (button != null)
                {
                    button.onClick.RemoveAllListeners();
                    int captured = position;
                    button.onClick.AddListener(() => OpenList(characterRoot, captured));
                }
            }
        }

        private bool IsUnlocked(int position)
        {
            if (position != 14) return true;
            int profession = progression.Player.Wardrobe.Profession;
            List<Dictionary<string, object>> rows = progression.Catalog.Rows("profession_base")
                .Where(row => Integer(row, "Profession") == profession).ToList();
            int rank = rows.Count == 0 ? 1 : rows.Max(row => Integer(row, "Rank"));
            return rank > 1;
        }

        private GameObject BuildItemIcon(OfflineEquipmentInstance instance, Transform parent)
        {
            OfficialCharacterPanelNode node = CharacterPanelNodeFactory.ReadRoot(itemTemplate);
            var nodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            var buttons = new Dictionary<string, Button>(StringComparer.Ordinal);
            RectTransform root = factory.Build(node, parent, nodes, buttons);
            Dictionary<string, object> item = progression.Catalog.FindItem(instance.ItemId);
            int quality = item == null ? 0 : Integer(item, "Quality");
            foreach (RectTransform rect in nodes.Values)
            {
                if (rect.name == "QualityImg")
                {
                    Image image = rect.GetComponent<Image>();
                    if (image != null) image.sprite = QualityBackground(quality);
                }
                if (rect.name == "LevelLimit")
                {
                    Text text = rect.GetComponentInChildren<Text>(true);
                    if (text != null) text.text = instance.ItemLevel.ToString(CultureInfo.InvariantCulture);
                }
            }
            SetIcon(nodes, instance.ItemId, item);
            DisableInteraction(root.gameObject);
            return root.gameObject;
        }

        private void SetIcon(Dictionary<string, RectTransform> nodes, int itemId, Dictionary<string, object> item)
        {
            RectTransform iconRect = nodes.Values.FirstOrDefault(value => value.name == "icon");
            string uri = progression.Catalog.ItemIconUri(itemId);
            Sprite sprite = string.IsNullOrEmpty(uri) ? null : assets.FindSprite(uri);
            if (iconRect != null)
            {
                Image image = iconRect.GetComponent<Image>();
                if (image != null)
                {
                    image.sprite = sprite;
                    image.enabled = sprite != null;
                }
            }
            RectTransform fake = nodes.Values.FirstOrDefault(value => value.name == "FakeIcon");
            if (fake != null)
            {
                Text text = fake.GetComponent<Text>();
                if (text != null)
                {
                    text.gameObject.SetActive(sprite == null);
                    text.text = sprite == null && item != null
                        ? MissingIconText(Convert.ToString(item["Name"], CultureInfo.InvariantCulture)) : string.Empty;
                }
            }
        }

        private static string MissingIconText(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            return name.Substring(Math.Max(0, name.Length - 4));
        }

        private Sprite QualityBackground(int quality)
        {
            string[] files =
            {
                "ImgQuality_White-031a03cab0.png", "ImgQuality_White-031a03cab0.png",
                "ImgQuality_Blue-42b8cb9630.png", "ImgQuality_Purple-0b40a53021.png",
                "ImgQuality_Orange-78887320b0.png", "ImgQuality_Gold-5f018c42e0.png",
                "ImgQuality_Red-caa09e6943.png", "ImgQuality_Multicolour-93374ebc65.png"
            };
            return assets.FindSprite("res://assets/official/sprites/" +
                files[Mathf.Clamp(quality, 0, files.Length - 1)]);
        }

        private static void DisableInteraction(GameObject root)
        {
            foreach (Button button in root.GetComponentsInChildren<Button>(true)) button.enabled = false;
            foreach (Graphic graphic in root.GetComponentsInChildren<Graphic>(true)) graphic.raycastTarget = false;
        }

        private void OpenList(Transform characterRoot, int position)
        {
            if (equipment == null || propSource == null)
            {
                LastMessage = "Official equipment catalog is unavailable.";
                return;
            }
            if (listDialog != null) CloseList();
            selectedPosition = position;
            rows.Clear();
            listNodes.Clear();
            listButtons.Clear();
            listDialog = new GameObject("CharacterPanelEquipListPanel", typeof(RectTransform));
            listDialog.transform.SetParent(characterRoot, false);
            RectTransform rect = (RectTransform)listDialog.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var blocker = new GameObject("DialogBlocker", typeof(RectTransform), typeof(Image), typeof(Button));
            blocker.transform.SetParent(rect, false);
            RectTransform blockerRect = (RectTransform)blocker.transform;
            blockerRect.anchorMin = Vector2.zero;
            blockerRect.anchorMax = Vector2.one;
            blockerRect.offsetMin = Vector2.zero;
            blockerRect.offsetMax = Vector2.zero;
            blocker.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
            blocker.GetComponent<Button>().onClick.AddListener(CloseList);
            var panel = new GameObject(CharacterPanelNodeFactory.ReadRoot(listTemplate).name, typeof(RectTransform));
            panel.transform.SetParent(rect, false);
            RectTransform panelRect = (RectTransform)panel.transform;
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            factory.Build(CharacterPanelNodeFactory.ReadRoot(listTemplate), panelRect, listNodes, listButtons);
            listContent = listNodes.Values.FirstOrDefault(value => value.name == "Content"
                && value.GetComponent<GridLayoutGroup>() != null);
            emptyTip = FindDeep(panelRect, "EmptyTip")?.gameObject;
            if (listContent == null) throw new FormatException("The original equipment list has no grid content.");
            RefreshList();
        }

        private void RefreshList()
        {
            foreach (GameObject row in rows) if (row != null) Destroy(row);
            rows.Clear();
            OfflinePlayerProgress player = progression.Player;
            var available = new List<(OfflineEquipmentInstance Instance, long Score)>();
            foreach (OfflineEquipmentInstance instance in player.Equipment.Instances)
            {
                if (OfflineEquipmentOperations.PositionForItem(equipment, instance.ItemId) != selectedPosition) continue;
                if (!OfflineEquipmentOperations.CanWear(equipment, instance.ItemId, player.Wardrobe.Profession)) continue;
                available.Add((instance, ItemScore(player, instance)));
            }
            available.Sort((left, right) =>
            {
                int score = right.Score.CompareTo(left.Score);
                if (score != 0) return score;
                int quality = ItemQuality(right.Instance.ItemId).CompareTo(ItemQuality(left.Instance.ItemId));
                return quality != 0 ? quality : right.Instance.ItemId.CompareTo(left.Instance.ItemId);
            });
            foreach ((OfflineEquipmentInstance instance, long score) in available)
            {
                if (listContent == null) break;
                OfficialCharacterPanelNode rowNode = CharacterPanelNodeFactory.ReadRoot(rowTemplate);
                var rowNodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
                var rowButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
                RectTransform row = factory.Build(rowNode, listContent, rowNodes, rowButtons);
                RectTransform mount = rowNodes.Values.FirstOrDefault(value => value.name == "Slot");
                if (mount != null)
                {
                    GameObject icon = BuildItemIcon(instance, mount);
                    if (icon != null) icon.transform.SetParent(mount, false);
                }
                Button button = row.GetComponent<Button>();
                if (button == null)
                {
                    Image graphic = row.GetComponent<Image>() ?? row.gameObject.AddComponent<Image>();
                    graphic.color = new Color(1f, 1f, 1f, 0f);
                    button = row.gameObject.AddComponent<Button>();
                    button.targetGraphic = graphic;
                }
                long instanceId = instance.InstanceId;
                button.onClick.AddListener(() => SelectCandidate(instanceId));
                rows.Add(row.gameObject);
            }
            if (emptyTip != null) emptyTip.SetActive(rows.Count == 0);
        }

        private long ItemScore(OfflinePlayerProgress player, OfflineEquipmentInstance instance)
        {
            CharacterPropBasicRef reference = CharacterPropBasicRef.Build(player.Level, player.SubRank,
                progression.Catalog.Props, progression.Catalog.GetLevelProps);
            return CharacterPropFormula.CombatRatingCommon(player.Level, player.SubRank,
                propSource.ItemProps(player, instance), progression.Catalog.Props, reference);
        }

        private int ItemQuality(int itemId)
        {
            Dictionary<string, object> item = progression.Catalog.FindItem(itemId);
            return item == null ? 0 : Integer(item, "Quality");
        }

        private void Wear(long instanceId)
        {
            ProgressionResult result = progression.WearEquipment(equipment, instanceId,
                "character-equip-wear-" + instanceId.ToString(CultureInfo.InvariantCulture) + "-"
                + Guid.NewGuid().ToString("N"));
            LastMessage = result.Message ?? result.Status.ToString();
            if (!result.IsApplied) return;
            CloseList();
            Changed?.Invoke();
        }

        public void CloseList()
        {
            if (compareDialog != null) Destroy(compareDialog);
            compareDialog = null;
            if (listDialog != null) Destroy(listDialog);
            listDialog = null;
            rows.Clear();
            listNodes.Clear();
            listButtons.Clear();
            listContent = null;
            emptyTip = null;
        }

        public bool HandleEscape()
        {
            if (compareDialog != null)
            {
                Destroy(compareDialog);
                compareDialog = null;
                return true;
            }
            if (listDialog == null) return false;
            CloseList();
            return true;
        }

        private void SelectCandidate(long instanceId)
        {
            OfflineEquippedSlot equipped = progression.Player.Equipment.Equipped
                .FirstOrDefault(value => value.Position == selectedPosition);
            if (equipped != null && equipped.InstanceId != instanceId)
                ShowCompare(equipped.InstanceId, instanceId);
            else
                Wear(instanceId);
        }

        private void ShowCompare(long currentId, long candidateId)
        {
            if (compareDialog != null) Destroy(compareDialog);
            OfflinePlayerProgress player = progression.Player;
            OfflineEquipmentInstance current = player.Equipment.Instances
                .FirstOrDefault(value => value.InstanceId == currentId);
            OfflineEquipmentInstance candidate = player.Equipment.Instances
                .FirstOrDefault(value => value.InstanceId == candidateId);
            if (current == null || candidate == null) return;
            compareInstanceId = candidateId;
            compareDialog = new GameObject("CharacterPanelEquipComparePanel", typeof(RectTransform));
            compareDialog.transform.SetParent(transform, false);
            RectTransform root = (RectTransform)compareDialog.transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            var nodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            var buttons = new Dictionary<string, Button>(StringComparer.Ordinal);
            factory.Build(CharacterPanelNodeFactory.ReadRoot(compareTemplate), root, nodes, buttons);
            RectTransform compareSlot = nodes.Values.FirstOrDefault(value => value.name == "CompareSlot");
            RectTransform content = nodes.Values.FirstOrDefault(value => value.name == "Content");
            var panels = compareSlot == null ? new List<RectTransform>()
                : compareSlot.Cast<Transform>().Select(value => (RectTransform)value).ToList();
            string[] labels = { "当前", "更换" };
            for (int i = 0; i < panels.Count && i < 2; i++)
            {
                OfflineEquipmentInstance instance = i == 0 ? current : candidate;
                FillComparePanel(panels[i], instance, labels[i]);
            }
            if (content != null)
            {
                Button confirm = CreateCompareButton(content, "确定", new Vector2(-150f, 60f),
                    new Color(0.86f, 0.55f, 0.3f, 0.95f));
                confirm.onClick.AddListener(() => Wear(compareInstanceId));
                Button cancel = CreateCompareButton(content, "取消", new Vector2(150f, 60f),
                    new Color(0.45f, 0.45f, 0.45f, 0.95f));
                cancel.onClick.AddListener(() =>
                {
                    if (compareDialog != null) Destroy(compareDialog);
                    compareDialog = null;
                });
            }
        }

        private void FillComparePanel(RectTransform panel, OfflineEquipmentInstance instance, string label)
        {
            for (int i = panel.childCount - 1; i >= 0; i--)
                if (panel.GetChild(i).name.StartsWith("Compare", StringComparison.Ordinal))
                    Destroy(panel.GetChild(i).gameObject);
            GameObject icon = BuildItemIcon(instance, panel);
            if (icon != null)
            {
                icon.name = "CompareIcon";
                RectTransform iconRect = (RectTransform)icon.transform;
                iconRect.anchorMin = new Vector2(0.5f, 0.62f);
                iconRect.anchorMax = new Vector2(0.5f, 0.62f);
                iconRect.sizeDelta = new Vector2(180f, 180f);
                iconRect.anchoredPosition = Vector2.zero;
            }
            var labelObject = new GameObject("CompareName", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(panel, false);
            RectTransform labelRect = (RectTransform)labelObject.transform;
            labelRect.anchorMin = new Vector2(0f, 0.5f);
            labelRect.anchorMax = new Vector2(1f, 0.5f);
            labelRect.sizeDelta = new Vector2(0f, 40f);
            Text text = labelObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 24;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label + "：" + ItemName(instance.ItemId);
            var scoreObject = new GameObject("CompareScore", typeof(RectTransform), typeof(Text));
            scoreObject.transform.SetParent(panel, false);
            RectTransform scoreRect = (RectTransform)scoreObject.transform;
            scoreRect.anchorMin = new Vector2(0f, 0.44f);
            scoreRect.anchorMax = new Vector2(1f, 0.44f);
            scoreRect.sizeDelta = new Vector2(0f, 40f);
            Text score = scoreObject.GetComponent<Text>();
            score.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            score.fontSize = 22;
            score.alignment = TextAnchor.MiddleCenter;
            score.color = new Color(1f, 0.85f, 0.5f);
            score.text = "评分：" + ItemScore(progression.Player, instance)
                .ToString(CultureInfo.InvariantCulture);
        }

        private Button CreateCompareButton(RectTransform parent, string label, Vector2 position, Color color)
        {
            var buttonObject = new GameObject("Compare" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObject.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)buttonObject.transform;
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = new Vector2(240f, 76f);
            rect.anchoredPosition = position;
            buttonObject.GetComponent<Image>().color = color;
            var textObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObject.transform.SetParent(buttonObject.transform, false);
            RectTransform textRect = (RectTransform)textObject.transform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            Text text = textObject.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 26;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label;
            return buttonObject.GetComponent<Button>();
        }

        private string ItemName(int itemId)
        {
            Dictionary<string, object> item = progression.Catalog.FindItem(itemId);
            return item == null ? itemId.ToString(CultureInfo.InvariantCulture)
                : Convert.ToString(item["Name"], CultureInfo.InvariantCulture);
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                Transform found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static int Integer(Dictionary<string, object> row, string key)
        {
            return row.TryGetValue(key, out object value)
                ? Convert.ToInt32(value, CultureInfo.InvariantCulture) : 0;
        }
    }
}
