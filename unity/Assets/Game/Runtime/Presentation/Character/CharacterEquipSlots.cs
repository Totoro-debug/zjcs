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
        private Dictionary<string, object> detailTemplate;
        private Dictionary<string, object> propRowTemplate;
        private Dictionary<string, object> promoteTemplate;
        private GameObject listDialog;
        private GameObject compareDialog;
        private GameObject detailDialog;
        private GameObject promoteDialog;
        private int promotePosition;
        private Transform slotRoot;
        private Text statusText;
        private long pendingDecomposeId;
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
            detailTemplate = CharacterModelPackage.AsObject(screens["equip_detail"], "equip_detail");
            propRowTemplate = CharacterModelPackage.AsObject(screens["character_prop_row"], "character_prop_row");
            promoteTemplate = CharacterModelPackage.AsObject(screens["equip_promote"], "equip_promote");
            var statusObject = new GameObject("EquipSlotsStatus", typeof(RectTransform), typeof(Text));
            statusObject.transform.SetParent(transform, false);
            RectTransform statusRect = (RectTransform)statusObject.transform;
            statusRect.anchorMin = new Vector2(0.5f, 0f);
            statusRect.anchorMax = new Vector2(0.5f, 0f);
            statusRect.pivot = new Vector2(0.5f, 0f);
            statusRect.sizeDelta = new Vector2(900f, 40f);
            statusRect.anchoredPosition = new Vector2(0f, 8f);
            statusText = statusObject.GetComponent<Text>();
            statusText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            statusText.fontSize = 20;
            statusText.alignment = TextAnchor.MiddleCenter;
            statusText.color = new Color(1f, 0.85f, 0.5f);
            statusText.raycastTarget = false;
        }

        public void Refresh(Transform characterRoot)
        {
            if (characterRoot == null) return;
            slotRoot = characterRoot;
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
                    long capturedInstance = instance == null ? 0 : instance.InstanceId;
                    button.onClick.AddListener(() =>
                    {
                        if (capturedInstance != 0) OpenDetail(characterRoot, capturedInstance);
                        else OpenList(characterRoot, captured);
                    });
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
            RectTransform bottom = nodes.Values.FirstOrDefault(value => value.name == "BottomText");
            if (bottom != null)
            {
                Text text = bottom.GetComponent<Text>();
                if (text != null)
                {
                    text.gameObject.SetActive(instance.BlessLevel > 0);
                    text.text = instance.BlessLevel > 0
                        ? "祝福+" + instance.BlessLevel.ToString(CultureInfo.InvariantCulture)
                        : string.Empty;
                }
            }
            string marker = SlotMarker(instance);
            if (!string.IsNullOrEmpty(marker))
            {
                var badge = new GameObject("SlotMarker", typeof(RectTransform), typeof(Text));
                badge.transform.SetParent(root, false);
                RectTransform badgeRect = (RectTransform)badge.transform;
                badgeRect.anchorMin = new Vector2(0f, 1f);
                badgeRect.anchorMax = new Vector2(0f, 1f);
                badgeRect.pivot = new Vector2(0f, 1f);
                badgeRect.sizeDelta = new Vector2(120f, 24f);
                badgeRect.anchoredPosition = new Vector2(4f, -2f);
                Text badgeText = badge.GetComponent<Text>();
                badgeText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                badgeText.fontSize = 14;
                badgeText.color = new Color(1f, 0.85f, 0.4f);
                badgeText.text = marker;
                badgeText.raycastTarget = false;
            }
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

        private string SlotMarker(OfflineEquipmentInstance instance)
        {
            if (equipment == null) return string.Empty;
            int position = OfflineEquipmentOperations.PositionForItem(equipment, instance.ItemId);
            var markers = new List<string>();
            int skinType = position == 10 ? 7 : position == 14 ? 8 : position == 12 ? 5 : 0;
            if (skinType != 0 && progression.Player.Wardrobe.WornItems.Any(value => value.Type == skinType))
                markers.Add("幻装");
            if (instance.Gems != null && instance.Gems.Count > 0)
                markers.Add("宝石" + instance.Gems.Count.ToString(CultureInfo.InvariantCulture));
            EquipmentUpgradeQuote quote = OfflineEquipmentOperations.QuoteUpgrade(progression.Player, equipment,
                position, progression.Player.SeasonRank, BlessLevel(), progression.Player.Level,
                progression.Player.MaxProfessionRank);
            if (quote.CanUpgrade) markers.Add("可强化");
            return string.Join("·", markers);
        }

        private int BlessLevel()
        {
            CharacterExperienceSnapshot experience = progression.Experience();
            return experience.BlessLevel;
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
                OfflineEquippedSlot worn = player.Equipment.Equipped
                    .FirstOrDefault(value => value.Position == selectedPosition);
                OfflineEquipmentInstance current = worn == null ? null
                    : player.Equipment.Instances.FirstOrDefault(value => value.InstanceId == worn.InstanceId);
                if (current != null && score > ItemScore(player, current))
                {
                    var better = new GameObject("BetterMark", typeof(RectTransform), typeof(Text));
                    better.transform.SetParent(row, false);
                    RectTransform betterRect = (RectTransform)better.transform;
                    betterRect.anchorMin = new Vector2(0f, 1f);
                    betterRect.anchorMax = new Vector2(0f, 1f);
                    betterRect.pivot = new Vector2(0f, 1f);
                    betterRect.sizeDelta = new Vector2(100f, 24f);
                    betterRect.anchoredPosition = new Vector2(6f, -4f);
                    Text betterText = better.GetComponent<Text>();
                    betterText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                    betterText.fontSize = 16;
                    betterText.color = new Color(0.55f, 1f, 0.55f);
                    betterText.text = "\u66f4\u4f18";
                    betterText.raycastTarget = false;
                }
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
            if (promoteDialog != null) Destroy(promoteDialog);
            promoteDialog = null;
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
            if (promoteDialog != null)
            {
                ClosePromote();
                return true;
            }
            if (detailDialog != null)
            {
                CloseDetail();
                return true;
            }
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

        private void OpenDetail(Transform characterRoot, long instanceId)
        {
            OfflineEquipmentInstance instance = progression.Player.Equipment.Instances
                .FirstOrDefault(value => value.InstanceId == instanceId);
            if (instance == null) return;
            CloseDetail();
            detailDialog = new GameObject("CharacterPanelEquipDetailPanel", typeof(RectTransform));
            detailDialog.transform.SetParent(characterRoot, false);
            RectTransform root = (RectTransform)detailDialog.transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            var blocker = new GameObject("DetailBlocker", typeof(RectTransform), typeof(Image), typeof(Button));
            blocker.transform.SetParent(root, false);
            RectTransform blockerRect = (RectTransform)blocker.transform;
            blockerRect.anchorMin = Vector2.zero;
            blockerRect.anchorMax = Vector2.one;
            blockerRect.offsetMin = Vector2.zero;
            blockerRect.offsetMax = Vector2.zero;
            blocker.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
            blocker.GetComponent<Button>().onClick.AddListener(CloseDetail);
            var nodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            var buttons = new Dictionary<string, Button>(StringComparer.Ordinal);
            factory.Build(CharacterPanelNodeFactory.ReadRoot(detailTemplate), root, nodes, buttons);
            RectTransform content = nodes.Values.FirstOrDefault(value => value.name == "Content");
            Text title = nodes.Values.Select(value => value.GetComponent<Text>())
                .FirstOrDefault(text => text != null && text.name == "TitleText");
            string name = ItemName(instance.ItemId);
            if (title != null) title.text = name;
            if (content == null) return;
            var nameObject = new GameObject("EquipDetailName", typeof(RectTransform), typeof(Text));
            nameObject.transform.SetParent(content, false);
            RectTransform nameRect = (RectTransform)nameObject.transform;
            nameRect.anchorMin = new Vector2(0f, 0.9f);
            nameRect.anchorMax = new Vector2(1f, 0.9f);
            nameRect.sizeDelta = new Vector2(0f, 44f);
            Text nameText = nameObject.GetComponent<Text>();
            nameText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            nameText.fontSize = 28;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.color = Color.white;
            nameText.text = name + "  Lv" + instance.ItemLevel.ToString(CultureInfo.InvariantCulture);
            RectTransform iconParent = content;
            GameObject icon = BuildItemIcon(instance, iconParent);
            if (icon != null)
            {
                icon.name = "EquipDetailIcon";
                RectTransform iconRect = (RectTransform)icon.transform;
                iconRect.anchorMin = new Vector2(0.5f, 0.78f);
                iconRect.anchorMax = new Vector2(0.5f, 0.78f);
                iconRect.sizeDelta = new Vector2(140f, 140f);
                iconRect.anchoredPosition = Vector2.zero;
            }
            Dictionary<int, long> props = propSource.ItemProps(progression.Player, instance);
            var lines = new List<int>(props.Keys);
            lines.Sort();
            float top = 0.62f;
            int shown = 0;
            foreach (int propType in lines)
            {
                if (shown >= 6) break;
                OfficialPropDefinition definition = progression.Catalog.Props.Get(propType);
                if (definition == null || props[propType] == 0) continue;
                RectTransform row = factory.Build(CharacterPanelNodeFactory.ReadRoot(propRowTemplate), content,
                    new Dictionary<string, RectTransform>(StringComparer.Ordinal),
                    new Dictionary<string, Button>(StringComparer.Ordinal));
                row.name = "EquipDetailProp";
                row.anchorMin = new Vector2(0.08f, top);
                row.anchorMax = new Vector2(0.92f, top);
                row.sizeDelta = new Vector2(0f, 44f);
                Text label = row.GetComponentsInChildren<Text>(true)
                    .FirstOrDefault(value => value.name == "Key");
                Text valueText = row.GetComponentsInChildren<Text>(true)
                    .FirstOrDefault(value => value.name == "Text");
                if (label != null) label.text = definition.Name;
                if (valueText != null) valueText.text = OfficialPropFormat.Value(definition, props[propType]);
                top -= 0.07f;
                if (top < 0.14f) break;
                shown++;
            }
            foreach (OfflineEquipmentTerm term in instance.Terms ?? new List<OfflineEquipmentTerm>())
            {
                if (term == null) continue;
                string termName = term.Type == 1
                    ? (progression.Catalog.Props.Get(term.PropertyType)?.Name ?? ("\u5c5e\u6027 " + term.PropertyType))
                    : "\u7279\u6b8a\u8bcd\u6761";
                string factor = term.Type == 1 ? " \u00d7" + term.Factor.ToString("0.##",
                    CultureInfo.InvariantCulture) : string.Empty;
                AddTermLine(content, termName + factor, ref top);
            }
            Button replace = CreateCompareButton(content, "更换", new Vector2(-260f, 40f),
                new Color(0.86f, 0.55f, 0.3f, 0.95f));
            replace.onClick.AddListener(() =>
            {
                CloseDetail();
                OpenList(characterRoot, OfflineEquipmentOperations.PositionForItem(equipment, instance.ItemId));
            });
            Button upgrade = CreateCompareButton(content, "强化", new Vector2(0f, 40f),
                new Color(0.4f, 0.55f, 0.75f, 0.95f));
            upgrade.onClick.AddListener(() => OpenPromote(OfflineEquipmentOperations.PositionForItem(equipment,
                instance.ItemId)));
            Button close = CreateCompareButton(content, "关闭", new Vector2(260f, 40f),
                new Color(0.45f, 0.45f, 0.45f, 0.95f));
            close.onClick.AddListener(CloseDetail);
            Button locked = CreateCompareButton(content, instance.Locked ? "解锁" : "锁定",
                new Vector2(-260f, 124f), new Color(0.5f, 0.65f, 0.85f, 0.95f));
            locked.onClick.AddListener(() => ToggleLock(instance));
            Button decompose = CreateCompareButton(content, "分解", new Vector2(0f, 124f),
                new Color(0.85f, 0.4f, 0.4f, 0.95f));
            decompose.onClick.AddListener(() => Decompose(instance));
            Button takeOff = CreateCompareButton(content, "卸下", new Vector2(260f, 124f),
                new Color(0.6f, 0.6f, 0.6f, 0.95f));
            takeOff.onClick.AddListener(() => TakeOff(instance));
        }

        private void ToggleLock(OfflineEquipmentInstance instance)
        {
            ProgressionResult result = progression.SetEquipmentLocked(instance.InstanceId, !instance.Locked,
                "character-equip-lock-" + Guid.NewGuid().ToString("N"));
            SetStatus(result.Message ?? result.Status.ToString());
            if (!result.IsApplied) return;
            CloseDetail();
            Changed?.Invoke();
            if (slotRoot != null) Refresh(slotRoot);
        }

        private void Decompose(OfflineEquipmentInstance instance)
        {
            if (pendingDecomposeId != instance.InstanceId)
            {
                pendingDecomposeId = instance.InstanceId;
                SetStatus("再次点击分解以确认。");
                return;
            }
            ProgressionResult result = progression.DecomposeEquipment(instance.InstanceId,
                "character-equip-decompose-" + Guid.NewGuid().ToString("N"), out IReadOnlyDictionary<int, long> rewards);
            pendingDecomposeId = 0;
            SetStatus(result.IsApplied
                ? "分解完成：" + string.Join("、", rewards.Select(pair =>
                    ItemName(pair.Key) + " ×" + pair.Value.ToString("N0", CultureInfo.InvariantCulture)))
                : result.Message ?? result.Status.ToString());
            if (!result.IsApplied) return;
            CloseDetail();
            Changed?.Invoke();
            if (slotRoot != null) Refresh(slotRoot);
        }

        private void TakeOff(OfflineEquipmentInstance instance)
        {
            int position = OfflineEquipmentOperations.PositionForItem(equipment, instance.ItemId);
            ProgressionResult result = progression.TakeOffEquipped(position,
                "character-equip-takeoff-" + Guid.NewGuid().ToString("N"));
            SetStatus(result.Message ?? result.Status.ToString());
            if (!result.IsApplied) return;
            CloseDetail();
            Changed?.Invoke();
            if (slotRoot != null) Refresh(slotRoot);
        }

        private void SetStatus(string message)
        {
            LastMessage = message ?? string.Empty;
            if (statusText != null) statusText.text = LastMessage;
        }

        private void OpenPromote(int position)
        {
            if (equipment == null || position <= 0) return;
            ClosePromote();
            promotePosition = position;
            EquipmentUpgradeQuote quote = progression.QuoteEquipmentUpgrade(position);
            promoteDialog = new GameObject("EquipPromotePanel", typeof(RectTransform));
            promoteDialog.transform.SetParent(transform, false);
            RectTransform root = (RectTransform)promoteDialog.transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            var nodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            var buttons = new Dictionary<string, Button>(StringComparer.Ordinal);
            factory.Build(CharacterPanelNodeFactory.ReadRoot(promoteTemplate), root, nodes, buttons);
            RectTransform middle = nodes.Values.FirstOrDefault(value => value.name == "MiddleGo");
            if (middle == null) return;
            var titleObject = new GameObject("PromoteTitle", typeof(RectTransform), typeof(Text));
            titleObject.transform.SetParent(middle, false);
            RectTransform titleRect = (RectTransform)titleObject.transform;
            titleRect.anchorMin = new Vector2(0f, 0.88f);
            titleRect.anchorMax = new Vector2(1f, 0.88f);
            titleRect.sizeDelta = new Vector2(0f, 50f);
            Text title = titleObject.GetComponent<Text>();
            title.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            title.fontSize = 28;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = Color.white;
            title.text = quote == null ? "装备强化"
                : "装备强化  Lv" + quote.CurrentLevel.ToString(CultureInfo.InvariantCulture)
                  + " → Lv" + quote.NextLevel.ToString(CultureInfo.InvariantCulture);
            float top = 0.78f;
            if (quote != null && !string.IsNullOrEmpty(quote.LimitKey))
            {
                AddPromoteLine(middle, LimitText(quote), ref top, new Color(1f, 0.6f, 0.55f));
            }
            if (quote?.Cost != null)
            {
                foreach (KeyValuePair<int, long> cost in quote.Cost)
                {
                    long owned = progression.Player.Inventory.Where(value => value.ItemId == cost.Key)
                        .Sum(value => value.Count);
                    bool enough = owned >= cost.Value;
                    AddPromoteLine(middle, ItemName(cost.Key) + "：" + owned.ToString("N0",
                        CultureInfo.InvariantCulture) + "/" + cost.Value.ToString("N0", CultureInfo.InvariantCulture),
                        ref top, enough ? Color.white : new Color(1f, 0.6f, 0.55f));
                }
            }
            Button upgrade = CreateCompareButton(middle, "强化", new Vector2(-140f, 30f),
                new Color(0.86f, 0.55f, 0.3f, 0.95f));
            upgrade.onClick.AddListener(DoUpgrade);
            Button close = CreateCompareButton(middle, "关闭", new Vector2(140f, 30f),
                new Color(0.45f, 0.45f, 0.45f, 0.95f));
            close.onClick.AddListener(ClosePromote);
        }

        private void AddTermLine(RectTransform parent, string value, ref float top)
        {
            if (top < 0.08f) return;
            RectTransform row = factory.Build(CharacterPanelNodeFactory.ReadRoot(propRowTemplate), parent,
                new Dictionary<string, RectTransform>(StringComparer.Ordinal),
                new Dictionary<string, Button>(StringComparer.Ordinal));
            row.name = "EquipDetailTerm";
            row.anchorMin = new Vector2(0.08f, top);
            row.anchorMax = new Vector2(0.92f, top);
            row.sizeDelta = new Vector2(0f, 40f);
            Text label = row.GetComponentsInChildren<Text>(true).FirstOrDefault(text => text.name == "Key");
            Text valueText = row.GetComponentsInChildren<Text>(true).FirstOrDefault(text => text.name == "Text");
            if (label != null) label.text = "\u8bcd\u6761";
            if (valueText != null) valueText.text = value;
            top -= 0.05f;
        }

        private void AddPromoteLine(RectTransform parent, string value, ref float top, Color color)
        {
            var line = new GameObject("PromoteLine", typeof(RectTransform), typeof(Text));
            line.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)line.transform;
            rect.anchorMin = new Vector2(0.08f, top);
            rect.anchorMax = new Vector2(0.92f, top);
            rect.sizeDelta = new Vector2(0f, 40f);
            Text text = line.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 24;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.text = value;
            top -= 0.09f;
        }

        private string LimitText(EquipmentUpgradeQuote quote)
        {
            switch (quote.LimitKey)
            {
                case "ui_equip_not_equipped": return "该部位尚未穿戴装备。";
                case "function_limit_subrank": return "需要位阶 " + quote.LimitValue;
                case "function_limit_level": return "需要等级 " + quote.LimitValue;
                case "function_limit_bless_level": return "需要赐福等级 " + quote.LimitValue;
                case "EquipPromotePanel.MaxSubRankLevel": return "其他部位等级不足。";
                case "EquipPromotePanel.LimitGapLevel": return "与最低部位等级差超过限制。";
                case "PlayerUseItemContent.Max": return "已达到上限。";
                default: return quote.LimitKey;
            }
        }

        private void DoUpgrade()
        {
            ProgressionResult result = progression.UpgradeEquipment(promotePosition,
                "character-equip-upgrade-" + promotePosition.ToString(CultureInfo.InvariantCulture) + "-"
                + Guid.NewGuid().ToString("N"));
            LastMessage = result.Message ?? result.Status.ToString();
            if (!result.IsApplied) return;
            ClosePromote();
            CloseDetail();
            Changed?.Invoke();
            if (slotRoot != null) Refresh(slotRoot);
        }

        private void ClosePromote()
        {
            if (promoteDialog != null) Destroy(promoteDialog);
            promoteDialog = null;
        }

        private void CloseDetail()
        {
            pendingDecomposeId = 0;
            if (detailDialog != null) Destroy(detailDialog);
            detailDialog = null;
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
