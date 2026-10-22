using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;

namespace Zjcs.Presentation.Character
{
    /// <summary>
    /// Original <c>CharacterPanelSkillContent</c>: three skill plans, four battle-skill and four
    /// secret-skill slots, the skill list tabs and installing/replacing/removing a skill.
    /// </summary>
    public sealed class CharacterSkillPanel : MonoBehaviour
    {
        private const int ActiveSkillItemType = 100;
        private const int PassiveSkillItemType = 101;
        private const int PieceItemType = 111;

        private OfficialCharacterAssetCatalog assets;
        private OfflineCharacterProgressionService progression;
        private CharacterPanelNodeFactory factory;
        private Dictionary<string, object> screen;
        private Dictionary<string, object> listItemTemplate;
        private Dictionary<string, object> itemTemplate;
        private Dictionary<string, object> promoteTemplate;
        private Dictionary<string, object> infoTemplate;
        private GameObject panel;
        private readonly Dictionary<string, RectTransform> nodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
        private readonly Dictionary<string, Button> buttons = new Dictionary<string, Button>(StringComparer.Ordinal);
        private Text planName;
        private InputField planInput;
        private RectTransform activeGroup;
        private RectTransform passiveGroup;
        private RectTransform skillGrid;
        private GameObject emptyTip;
        private Toggle showAll;
        private Text statusText;
        private GameObject promoteDialog;
        private int promoteSkillId;
        private GameObject detailDialog;
        private GameObject recommendDialog;
        private readonly List<GameObject> listRows = new List<GameObject>();
        private int listType = ActiveSkillItemType;
        private int selectedType;
        private int selectedSlot = -1;
        private int plan;
        private int pendingSkillId;
        private int pendingType;
        private GameObject installMask;

        public event Action Changed;
        public string LastMessage { get; private set; } = string.Empty;

        public bool IsOpen => panel != null;

        public void Initialize(OfficialCharacterAssetCatalog assetCatalog,
            OfflineCharacterProgressionService characterProgression)
        {
            assets = assetCatalog ?? throw new ArgumentNullException(nameof(assetCatalog));
            progression = characterProgression ?? throw new ArgumentNullException(nameof(characterProgression));
            factory = new CharacterPanelNodeFactory(assets);
            Dictionary<string, object> panels = CharacterModelPackage.AsObject(
                OfficialEquipmentJson.Parse(assets.CharacterPanels.text), "panels");
            Dictionary<string, object> screens = CharacterModelPackage.AsObject(panels["screens"], "screens");
            screen = CharacterModelPackage.AsObject(screens["skills"], "skills");
            listItemTemplate = CharacterModelPackage.AsObject(screens["skill_list_item"], "skill_list_item");
            itemTemplate = CharacterModelPackage.AsObject(screens["item"], "item");
            promoteTemplate = CharacterModelPackage.AsObject(screens["skill_promote"], "skill_promote");
            infoTemplate = CharacterModelPackage.AsObject(screens["character_props"], "character_props");
        }

        public void Open(Transform parent)
        {
            if (panel != null) return;
            nodes.Clear();
            buttons.Clear();
            panel = new GameObject("SkillContentPanel", typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)panel.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            factory.Build(CharacterPanelNodeFactory.ReadRoot(screen), rect, nodes, buttons);
            plan = progression.Player.Skills.SelectedPlan;
            Bind();
            Refresh();
        }

        public void Close()
        {
            if (recommendDialog != null) Destroy(recommendDialog);
            recommendDialog = null;
            if (detailDialog != null) Destroy(detailDialog);
            detailDialog = null;
            if (promoteDialog != null) Destroy(promoteDialog);
            promoteDialog = null;
            if (installMask != null) Destroy(installMask);
            installMask = null;
            pendingSkillId = 0;
            pendingType = 0;
            if (panel != null) Destroy(panel);
            panel = null;
            listRows.Clear();
            nodes.Clear();
            buttons.Clear();
            skillGrid = null;
            activeGroup = null;
            passiveGroup = null;
            planName = null;
            emptyTip = null;
            showAll = null;
        }

        public bool HandleEscape()
        {
            if (recommendDialog != null)
            {
                CloseRecommendations();
                return true;
            }
            if (detailDialog != null)
            {
                CloseSkillDetail();
                return true;
            }
            if (promoteDialog != null)
            {
                ClosePromote();
                return true;
            }
            if (panel == null) return false;
            Close();
            return true;
        }

        private void Bind()
        {
            planName = FindTextDeep("SkillPlanName");
            RectTransform nameRect = FindDeep("SkillPlanName");
            if (nameRect != null)
            {
                Button nameButton = nameRect.GetComponent<Button>() ?? nameRect.gameObject.AddComponent<Button>();
                Graphic graphic = nameRect.GetComponent<Graphic>();
                if (graphic == null)
                {
                    Image image = nameRect.gameObject.AddComponent<Image>();
                    image.color = new Color(1f, 1f, 1f, 0f);
                    graphic = image;
                }
                nameButton.targetGraphic = graphic;
                nameButton.onClick.AddListener(BeginRename);
                planInput = nameRect.GetComponent<InputField>();
                if (planInput == null)
                {
                    planInput = nameRect.gameObject.AddComponent<InputField>();
                    planInput.textComponent = planName;
                    planInput.targetGraphic = graphic;
                    planInput.lineType = InputField.LineType.SingleLine;
                    planInput.characterLimit = 16;
                    planInput.onEndEdit.AddListener(CommitRename);
                }
                planInput.interactable = false;
            }
            BindButtonName("PrevPlanBtn", () => SwitchPlan(-1));
            BindButtonName("FastLevelUpBtn", UpgradeAllSlots);
            BindButtonName("FastPromotionBtn", PromoteAllSkills);
            BindButtonName("RecommendBtn", OpenRecommendations);
            var statusObject = new GameObject("SkillPanelStatus", typeof(RectTransform), typeof(Text));
            statusObject.transform.SetParent(panel.transform, false);
            RectTransform statusRect = (RectTransform)statusObject.transform;
            statusRect.anchorMin = new Vector2(0.5f, 0f);
            statusRect.anchorMax = new Vector2(0.5f, 0f);
            statusRect.pivot = new Vector2(0.5f, 0f);
            statusRect.sizeDelta = new Vector2(900f, 40f);
            statusRect.anchoredPosition = new Vector2(0f, 8f);
            statusText = statusObject.GetComponent<Text>();
            statusText.font = CharacterPanelNodeFactory.UiFont;
            statusText.fontSize = 20;
            statusText.alignment = TextAnchor.MiddleCenter;
            statusText.color = new Color(1f, 0.85f, 0.5f);
            statusText.raycastTarget = false;
            BindButtonName("NextPlanBtn", () => SwitchPlan(1));
            activeGroup = FindDeep("ActiveSkill");
            passiveGroup = FindDeep("PassiveSkill");
            skillGrid = FindDeep("skillGrid");
            emptyTip = FindDeep("EmptyTip")?.gameObject;
            RectTransform toggle = FindDeep("ShowAll");
            if (toggle != null)
            {
                showAll = toggle.GetComponent<Toggle>() ?? toggle.gameObject.AddComponent<Toggle>();
                showAll.isOn = false;
                showAll.onValueChanged.AddListener(_ => RefreshList());
            }
            BindTab("ActiveSkillTab", ActiveSkillItemType);
            BindTab("PassiveSkillTab", PassiveSkillItemType);
            BindTab("SkillPieceTab", PieceItemType);
            if (panel == null) return;
            Button close = buttons.Values.FirstOrDefault(value => value.name.IndexOf("Close", StringComparison.OrdinalIgnoreCase) >= 0);
            if (close != null) close.onClick.AddListener(Close);
            BuildSlotHandlers(activeGroup, ActiveSkillItemType);
            BuildSlotHandlers(passiveGroup, PassiveSkillItemType);
        }

        private void BindButtonName(string name, Action action)
        {
            Button button = buttons.Values.FirstOrDefault(value => value.name == name);
            if (button != null) button.onClick.AddListener(() => action());
        }

        private void BindTab(string namePart, int itemType)
        {
            Button button = buttons.FirstOrDefault(pair => pair.Key.IndexOf(namePart, StringComparison.Ordinal) >= 0).Value;
            if (button != null) button.onClick.AddListener(() => SelectTab(itemType));
        }

        private void BuildSlotHandlers(RectTransform group, int itemType)
        {
            if (group == null) return;
            var slots = new List<RectTransform>();
            foreach (Transform child in group)
            {
                if (child.name.StartsWith("SkillContentPanelSkillGroupItem", StringComparison.Ordinal)
                    || child.GetComponent<Button>() != null) slots.Add((RectTransform)child);
            }
            for (int index = 0; index < slots.Count && index < 4; index++)
            {
                int slot = index;
                Button button = slots[index].GetComponent<Button>();
                if (button == null)
                {
                    Image image = slots[index].gameObject.AddComponent<Image>();
                    image.color = new Color(1f, 1f, 1f, 0f);
                    button = slots[index].gameObject.AddComponent<Button>();
                    button.targetGraphic = image;
                }
                button.onClick.AddListener(() => SelectSlot(itemType, slot));
                var upgradeObject = new GameObject("SlotUpgrade", typeof(RectTransform), typeof(Image),
                    typeof(Button));
                upgradeObject.transform.SetParent(slots[index], false);
                RectTransform upgradeRect = (RectTransform)upgradeObject.transform;
                upgradeRect.anchorMin = new Vector2(1f, 0f);
                upgradeRect.anchorMax = new Vector2(1f, 0f);
                upgradeRect.pivot = new Vector2(1f, 0f);
                upgradeRect.sizeDelta = new Vector2(64f, 32f);
                upgradeRect.anchoredPosition = new Vector2(-4f, 4f);
                Image upgradeImage = upgradeObject.GetComponent<Image>();
                upgradeImage.color = new Color(0.4f, 0.55f, 0.75f, 0.9f);
                var labelObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
                labelObject.transform.SetParent(upgradeObject.transform, false);
                RectTransform labelRect = (RectTransform)labelObject.transform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
                Text labelText = labelObject.GetComponent<Text>();
                labelText.font = CharacterPanelNodeFactory.UiFont;
                labelText.fontSize = 16;
                labelText.alignment = TextAnchor.MiddleCenter;
                labelText.color = Color.white;
                labelText.raycastTarget = false;
                labelText.text = "\u5347\u7ea7";
                Button upgradeButton = upgradeObject.GetComponent<Button>();
                upgradeButton.targetGraphic = upgradeImage;
                int capturedType = itemType;
                upgradeButton.onClick.AddListener(() => UpgradeSlot(capturedType, slot));
            }
        }

        private void UpgradeSlot(int itemType, int slot)
        {
            ProgressionResult result = progression.UpgradeSkillSlot(itemType, slot);
            SetStatus(result.Message ?? result.Status.ToString());
            if (!result.IsApplied) return;
            Refresh();
            Changed?.Invoke();
        }

        private void Refresh()
        {
            if (panel == null) return;
            OfflineSkillProfile profile = progression.Player.Skills;
            plan = profile.SelectedPlan;
            if (planName != null) planName.text = profile.Plans[plan].Name;
            if (planInput != null) planInput.SetTextWithoutNotify(profile.Plans[plan].Name);
            RefreshSlots(activeGroup, ActiveSkillItemType);
            RefreshSlots(passiveGroup, PassiveSkillItemType);
            RefreshList();
        }

        private void RefreshSlots(RectTransform group, int itemType)
        {
            if (group == null) return;
            int[] equipped = itemType == ActiveSkillItemType
                ? progression.Player.Skills.Plans[plan].ActiveSkills
                : progression.Player.Skills.Plans[plan].PassiveSkills;
            int index = 0;
            foreach (Transform child in group)
            {
                if (!(child.name.StartsWith("SkillContentPanelSkillGroupItem", StringComparison.Ordinal)
                    || child.GetComponent<Button>() != null)) continue;
                if (index >= 4) break;
                int skillId = equipped[index];
                Transform mount = FindDeep(child, "Slot") ?? child;
                for (int i = mount.childCount - 1; i >= 0; i--)
                    if (mount.GetChild(i).name == "SkillIcon") Destroy(mount.GetChild(i).gameObject);
                if (skillId != 0)
                {
                    BuildSkillIcon(skillId, mount, "SkillIcon");
                }
                Text level = FindDeep(child, "LevelText")?.GetComponent<Text>();
                if (level != null)
                {
                    int slotLevel = progression.SkillProgression == null ? 0
                        : progression.SkillProgression.SlotLevel(progression.Player.Skills, itemType, index);
                    level.text = skillId == 0 ? string.Empty : "Lv" + slotLevel.ToString(CultureInfo.InvariantCulture);
                }
                Transform selected = FindDeep(child, "SelAnimGo");
                bool highlighted = selectedType == itemType
                    && (selectedSlot == index || (pendingSkillId != 0 && pendingType == itemType));
                if (selected != null) selected.gameObject.SetActive(highlighted);
                index++;
            }
        }

        private void RefreshList()
        {
            foreach (GameObject row in listRows) if (row != null) Destroy(row);
            listRows.Clear();
            if (skillGrid == null) return;
            OfflineSkillProfile profile = progression.Player.Skills;
            int[] equipped = listType == ActiveSkillItemType
                ? profile.Plans[plan].ActiveSkills : listType == PassiveSkillItemType
                    ? profile.Plans[plan].PassiveSkills : Array.Empty<int>();
            if (listType == PieceItemType)
            {
                RefreshPieceList();
                return;
            }
            var owned = profile.Skills.Where(skill => skill != null && skill.ClassId > 0)
                .ToDictionary(skill => skill.ClassId, skill => skill.Rank);
            var entries = new List<int>();
            foreach (OfflineSkillDefinition definition in progression.SkillRules?.Skills ?? Array.Empty<OfflineSkillDefinition>())
            {
                if (definition.ItemType != listType) continue;
                bool has = owned.ContainsKey(definition.ClassId);
                bool craftable = !has && HasPiece(definition.PieceItemId);
                if (!has && !craftable && !(showAll != null && showAll.isOn)) continue;
                if (has && Array.IndexOf(equipped, definition.ClassId) >= 0) continue;
                entries.Add(definition.ClassId);
            }
            entries.Sort((left, right) =>
            {
                bool leftFavorite = OfflineSkillProgression.IsFavorite(profile, left);
                bool rightFavorite = OfflineSkillProgression.IsFavorite(profile, right);
                if (leftFavorite != rightFavorite) return rightFavorite ? 1 : -1;
                int quality = Quality(right).CompareTo(Quality(left));
                return quality != 0 ? quality : right.CompareTo(left);
            });
            OfflineSkillPlayerContext context = progression.SkillContext();
            foreach (int skillId in entries)
            {
                OfflineSkillDefinition definition = progression.SkillRules.Skills
                    .FirstOrDefault(value => value.ClassId == skillId);
                bool available = OfflineSkillProgression.IsAvailableFor(definition, context);
                GameObject row = BuildSkillRow(skillId, owned.ContainsKey(skillId), available);
                if (row != null) listRows.Add(row);
            }
            if (emptyTip != null) emptyTip.SetActive(listRows.Count == 0);
        }

        private bool HasPiece(int pieceItemId)
        {
            if (pieceItemId <= 0) return false;
            return progression.Player.Inventory.Any(value => value != null
                && value.ItemId == pieceItemId && value.Count > 0);
        }

        private int SkillPieceId(int skillId)
        {
            OfflineSkillDefinition definition = progression.SkillRules?.Skills
                .FirstOrDefault(value => value.ClassId == skillId);
            return definition == null ? 0 : definition.PieceItemId;
        }

        private void RefreshPieceList()
        {
            var pieces = progression.Player.Inventory
                .Where(value => value != null && value.Count > 0 && progression.Catalog.ItemType(value.ItemId) == PieceItemType)
                .OrderByDescending(value => Quality(value.ItemId)).ThenByDescending(value => value.ItemId).ToList();
            foreach (OfflineItemStack stack in pieces)
            {
                var rowNodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
                var rowButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
                RectTransform row = factory.Build(CharacterPanelNodeFactory.ReadRoot(listItemTemplate), skillGrid,
                    rowNodes, rowButtons);
                Transform mount = FindDeep(row, "Anchor") ?? row;
                BuildSkillIcon(stack.ItemId, mount, "SkillIcon");
                var labelObject = new GameObject("SkillPieceLabel", typeof(RectTransform), typeof(Text));
                labelObject.transform.SetParent(row, false);
                RectTransform labelRect = (RectTransform)labelObject.transform;
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(1f, 0f);
                labelRect.pivot = new Vector2(0.5f, 0f);
                labelRect.sizeDelta = new Vector2(0f, 34f);
                labelRect.anchoredPosition = new Vector2(0f, 4f);
                Text text = labelObject.GetComponent<Text>();
                text.font = CharacterPanelNodeFactory.UiFont;
                text.fontSize = 18;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.white;
                text.text = ItemName(stack.ItemId) + " \u00d7" + stack.Count.ToString("N0", CultureInfo.InvariantCulture);
                text.raycastTarget = false;
                LayoutElement element = row.GetComponent<LayoutElement>() ?? row.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = 150f;
                element.minHeight = 150f;
                listRows.Add(row.gameObject);
            }
            if (emptyTip != null) emptyTip.SetActive(listRows.Count == 0);
        }

        private string ItemName(int itemId)
        {
            Dictionary<string, object> item = progression.Catalog.FindItem(itemId);
            return item == null ? itemId.ToString(CultureInfo.InvariantCulture)
                : Convert.ToString(item["Name"], CultureInfo.InvariantCulture);
        }

        private GameObject BuildSkillRow(int skillId, bool has, bool available)
        {
            var rowNodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            var rowButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
            RectTransform row = factory.Build(CharacterPanelNodeFactory.ReadRoot(listItemTemplate), skillGrid,
                rowNodes, rowButtons);
            Transform mount = FindDeep(row, "Anchor") ?? row;
            BuildSkillIcon(skillId, mount, "SkillIcon");
            var label = new GameObject("SkillLabel", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(row, false);
            RectTransform labelRect = (RectTransform)label.transform;
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(1f, 0f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.sizeDelta = new Vector2(0f, 34f);
            labelRect.anchoredPosition = new Vector2(0f, 4f);
            var starObject = new GameObject("SkillFavorite", typeof(RectTransform), typeof(Image), typeof(Button));
            starObject.transform.SetParent(row, false);
            RectTransform starRect = (RectTransform)starObject.transform;
            starRect.anchorMin = new Vector2(1f, 1f);
            starRect.anchorMax = new Vector2(1f, 1f);
            starRect.pivot = new Vector2(1f, 1f);
            starRect.sizeDelta = new Vector2(44f, 44f);
            starRect.anchoredPosition = new Vector2(-6f, -6f);
            Image starImage = starObject.GetComponent<Image>();
            starImage.color = new Color(1f, 1f, 1f, 0f);
            starImage.raycastTarget = true;
            var starTextObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            starTextObject.transform.SetParent(starObject.transform, false);
            RectTransform starTextRect = (RectTransform)starTextObject.transform;
            starTextRect.anchorMin = Vector2.zero;
            starTextRect.anchorMax = Vector2.one;
            starTextRect.offsetMin = Vector2.zero;
            starTextRect.offsetMax = Vector2.zero;
            Text starText = starTextObject.GetComponent<Text>();
            starText.font = CharacterPanelNodeFactory.UiFont;
            starText.fontSize = 30;
            starText.alignment = TextAnchor.MiddleCenter;
            starText.color = new Color(1f, 0.85f, 0.3f);
            starText.raycastTarget = false;
            starText.text = OfflineSkillProgression.IsFavorite(progression.Player.Skills, skillId)
                ? "\u2605" : "\u2606";
            Button starButton = starObject.GetComponent<Button>();
            starButton.targetGraphic = starImage;
            starButton.onClick.AddListener(() => ToggleFavorite(skillId));
            var promoteObject = new GameObject("SkillPromote", typeof(RectTransform), typeof(Image), typeof(Button));
            promoteObject.transform.SetParent(row, false);
            RectTransform promoteRect = (RectTransform)promoteObject.transform;
            promoteRect.anchorMin = new Vector2(1f, 0f);
            promoteRect.anchorMax = new Vector2(1f, 0f);
            promoteRect.pivot = new Vector2(1f, 0f);
            promoteRect.sizeDelta = new Vector2(88f, 36f);
            promoteRect.anchoredPosition = new Vector2(-6f, 6f);
            Image promoteImage = promoteObject.GetComponent<Image>();
            promoteImage.color = new Color(0.86f, 0.55f, 0.3f, 0.9f);
            var promoteTextObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            promoteTextObject.transform.SetParent(promoteObject.transform, false);
            RectTransform promoteTextRect = (RectTransform)promoteTextObject.transform;
            promoteTextRect.anchorMin = Vector2.zero;
            promoteTextRect.anchorMax = Vector2.one;
            promoteTextRect.offsetMin = Vector2.zero;
            promoteTextRect.offsetMax = Vector2.zero;
            Text promoteText = promoteTextObject.GetComponent<Text>();
            promoteText.font = CharacterPanelNodeFactory.UiFont;
            promoteText.fontSize = 18;
            promoteText.alignment = TextAnchor.MiddleCenter;
            promoteText.color = Color.white;
            promoteText.raycastTarget = false;
            promoteText.text = "\u57f9\u517b";
            Button promoteButton = promoteObject.GetComponent<Button>();
            promoteButton.targetGraphic = promoteImage;
            promoteButton.onClick.AddListener(() => OpenPromote(skillId));
            var detailObject = new GameObject("SkillDetail", typeof(RectTransform), typeof(Image), typeof(Button));
            detailObject.transform.SetParent(row, false);
            RectTransform detailRect = (RectTransform)detailObject.transform;
            detailRect.anchorMin = new Vector2(1f, 0f);
            detailRect.anchorMax = new Vector2(1f, 0f);
            detailRect.pivot = new Vector2(1f, 0f);
            detailRect.sizeDelta = new Vector2(88f, 36f);
            detailRect.anchoredPosition = new Vector2(-100f, 6f);
            Image detailImage = detailObject.GetComponent<Image>();
            detailImage.color = new Color(0.4f, 0.55f, 0.75f, 0.9f);
            var detailTextObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
            detailTextObject.transform.SetParent(detailObject.transform, false);
            RectTransform detailTextRect = (RectTransform)detailTextObject.transform;
            detailTextRect.anchorMin = Vector2.zero;
            detailTextRect.anchorMax = Vector2.one;
            detailTextRect.offsetMin = Vector2.zero;
            detailTextRect.offsetMax = Vector2.zero;
            Text detailText = detailTextObject.GetComponent<Text>();
            detailText.font = CharacterPanelNodeFactory.UiFont;
            detailText.fontSize = 18;
            detailText.alignment = TextAnchor.MiddleCenter;
            detailText.color = Color.white;
            detailText.raycastTarget = false;
            detailText.text = "\u8be6\u60c5";
            Button detailButton = detailObject.GetComponent<Button>();
            detailButton.targetGraphic = detailImage;
            detailButton.onClick.AddListener(() => OpenSkillDetail(skillId));
            Text text = label.GetComponent<Text>();
            text.font = CharacterPanelNodeFactory.UiFont;
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = !available ? new Color(0.6f, 0.6f, 0.6f)
                : has ? Color.white : new Color(1f, 0.55f, 0.45f);
            text.text = SkillName(skillId) + (available ? string.Empty : "\uff08\u804c\u4e1a\u4e0d\u7b26\uff09")
                + (has ? string.Empty : HasPiece(SkillPieceId(skillId)) ? "\uff08\u53ef\u5408\u6210\uff09" : string.Empty);
            text.raycastTarget = false;
            LayoutElement element = row.GetComponent<LayoutElement>() ?? row.gameObject.AddComponent<LayoutElement>();
            element.preferredHeight = 150f;
            element.minHeight = 150f;
            Image graphic = row.gameObject.GetComponent<Image>();
            if (graphic == null)
            {
                graphic = row.gameObject.AddComponent<Image>();
                graphic.color = new Color(1f, 1f, 1f, 0f);
            }
            Button button = row.GetComponent<Button>() ?? row.gameObject.AddComponent<Button>();
            button.targetGraphic = graphic;
            button.onClick.AddListener(() => Install(skillId));
            return row.gameObject;
        }

        private GameObject BuildSkillIcon(int itemId, Transform parent, string name)
        {
            var iconNodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            var iconButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
            RectTransform root = factory.Build(CharacterPanelNodeFactory.ReadRoot(itemTemplate), parent,
                iconNodes, iconButtons);
            root.gameObject.name = name;
            Dictionary<string, object> item = progression.Catalog.FindItem(itemId);
            int quality = item == null ? 0 : Convert.ToInt32(item["Quality"], CultureInfo.InvariantCulture);
            foreach (RectTransform rect in iconNodes.Values)
            {
                if (rect.name == "QualityImg")
                {
                    Image image = rect.GetComponent<Image>();
                    string[] files = { "ImgQuality_White-031a03cab0.png", "ImgQuality_White-031a03cab0.png",
                        "ImgQuality_Blue-42b8cb9630.png", "ImgQuality_Purple-0b40a53021.png",
                        "ImgQuality_Orange-78887320b0.png", "ImgQuality_Gold-5f018c42e0.png",
                        "ImgQuality_Red-caa09e6943.png", "ImgQuality_Multicolour-93374ebc65.png" };
                    if (image != null) image.sprite = assets.FindSprite("res://assets/official/sprites/"
                        + files[Mathf.Clamp(quality, 0, files.Length - 1)]);
                }
            }
            RectTransform icon = iconNodes.Values.FirstOrDefault(value => value.name == "icon");
            string uri = progression.Catalog.ItemIconUri(itemId);
            Sprite sprite = string.IsNullOrEmpty(uri) ? null : assets.FindSprite(uri);
            if (icon != null)
            {
                Image image = icon.GetComponent<Image>();
                if (image != null)
                {
                    image.sprite = sprite;
                    image.enabled = sprite != null;
                }
            }
            foreach (Button value in root.GetComponentsInChildren<Button>(true)) value.enabled = false;
            foreach (Graphic value in root.GetComponentsInChildren<Graphic>(true)) value.raycastTarget = false;
            return root.gameObject;
        }

        private void UpgradeAllSlots()
        {
            ProgressionResult result = progression.UpgradeAllSkillSlots();
            SetStatus(result.Message ?? result.Status.ToString());
            if (!result.IsApplied) return;
            Refresh();
            Changed?.Invoke();
        }

        private void PromoteAllSkills()
        {
            ProgressionResult result = progression.PromoteAllSkills();
            SetStatus(result.Message ?? result.Status.ToString());
            if (!result.IsApplied) return;
            Refresh();
            Changed?.Invoke();
        }

        private void SetStatus(string message)
        {
            LastMessage = message ?? string.Empty;
            if (statusText != null) statusText.text = LastMessage;
        }

        private void OpenRecommendations()
        {
            IReadOnlyList<OfficialSkillRecommendation> recommendations =
                progression.RecommendationsForProfession(progression.Player.Wardrobe.Profession);
            if (recommendations.Count == 0)
            {
                SetStatus("\u5f53\u524d\u804c\u4e1a\u6682\u65e0\u63a8\u8350\u65b9\u6848\u3002");
                return;
            }
            if (recommendDialog != null) Destroy(recommendDialog);
            recommendDialog = new GameObject("SkillRecommendDialog", typeof(RectTransform));
            recommendDialog.transform.SetParent(transform, false);
            RectTransform root = (RectTransform)recommendDialog.transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            var blocker = new GameObject("RecommendBlocker", typeof(RectTransform), typeof(Image), typeof(Button));
            blocker.transform.SetParent(root, false);
            RectTransform blockerRect = (RectTransform)blocker.transform;
            blockerRect.anchorMin = Vector2.zero;
            blockerRect.anchorMax = Vector2.one;
            blockerRect.offsetMin = Vector2.zero;
            blockerRect.offsetMax = Vector2.zero;
            blocker.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
            blocker.GetComponent<Button>().onClick.AddListener(CloseRecommendations);
            var nodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            var buttons = new Dictionary<string, Button>(StringComparer.Ordinal);
            factory.Build(CharacterPanelNodeFactory.ReadRoot(infoTemplate), root, nodes, buttons);
            RectTransform content = nodes.Values.FirstOrDefault(value => value.name == "OverallContent");
            Text title = nodes.Values.Select(value => value.GetComponent<Text>())
                .FirstOrDefault(text => text != null && text.name == "TitleText");
            if (title != null) title.text = "\u6280\u80fd\u63a8\u8350";
            if (content == null) return;
            foreach (RectTransform child in content) child.gameObject.SetActive(false);
            float top = 0.9f;
            foreach (OfficialSkillRecommendation recommendation in recommendations)
            {
                if (top < 0.14f) break;
                var rowObject = new GameObject("RecommendRow", typeof(RectTransform));
                rowObject.transform.SetParent(content, false);
                RectTransform row = (RectTransform)rowObject.transform;
                row.anchorMin = new Vector2(0.06f, top);
                row.anchorMax = new Vector2(0.94f, top);
                row.sizeDelta = new Vector2(0f, 110f);
                var labelObject = new GameObject("Text", typeof(RectTransform), typeof(Text));
                labelObject.transform.SetParent(row, false);
                RectTransform labelRect = (RectTransform)labelObject.transform;
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(0.72f, 1f);
                labelRect.offsetMin = Vector2.zero;
                labelRect.offsetMax = Vector2.zero;
                Text label = labelObject.GetComponent<Text>();
                label.font = CharacterPanelNodeFactory.UiFont;
                label.fontSize = 20;
                label.alignment = TextAnchor.MiddleLeft;
                label.color = Color.white;
                label.horizontalOverflow = HorizontalWrapMode.Wrap;
                label.text = recommendation.name + "\n" + string.Join("\u3001",
                    recommendation.CoreSkillList.Select(ItemName).Where(value => value.Length > 0));
                Button apply = CreatePanelButton(row, "\u5e94\u7528", new Vector2(0f, 0f),
                    new Color(0.86f, 0.55f, 0.3f, 0.95f));
                RectTransform applyRect = (RectTransform)apply.transform;
                applyRect.anchorMin = new Vector2(1f, 0.5f);
                applyRect.anchorMax = new Vector2(1f, 0.5f);
                applyRect.pivot = new Vector2(1f, 0.5f);
                applyRect.sizeDelta = new Vector2(120f, 48f);
                applyRect.anchoredPosition = Vector2.zero;
                OfficialSkillRecommendation captured = recommendation;
                apply.onClick.AddListener(() => ApplyRecommendation(captured));
                top -= 0.13f;
            }
            Button close = CreatePanelButton(content, "\u5173\u95ed", new Vector2(0f, 20f),
                new Color(0.45f, 0.45f, 0.45f, 0.95f));
            close.onClick.AddListener(CloseRecommendations);
        }

        private void ApplyRecommendation(OfficialSkillRecommendation recommendation)
        {
            ProgressionResult result = progression.ApplySkillRecommendation(recommendation);
            SetStatus(result.Message ?? result.Status.ToString());
            if (!result.IsApplied) return;
            CloseRecommendations();
            Refresh();
            Changed?.Invoke();
        }

        private void CloseRecommendations()
        {
            if (recommendDialog != null) Destroy(recommendDialog);
            recommendDialog = null;
        }

        private void OpenSkillDetail(int skillId)
        {
            OfflineSkillDefinition definition = progression.SkillDefinition(skillId);
            if (definition == null) return;
            CloseSkillDetail();
            detailDialog = new GameObject("SkillDetailDialog", typeof(RectTransform));
            detailDialog.transform.SetParent(transform, false);
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
            blocker.GetComponent<Button>().onClick.AddListener(CloseSkillDetail);
            var nodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            var buttons = new Dictionary<string, Button>(StringComparer.Ordinal);
            factory.Build(CharacterPanelNodeFactory.ReadRoot(infoTemplate), root, nodes, buttons);
            RectTransform content = nodes.Values.FirstOrDefault(value => value.name == "OverallContent");
            Text title = nodes.Values.Select(value => value.GetComponent<Text>())
                .FirstOrDefault(text => text != null && text.name == "TitleText");
            int rank = OfflineSkillProgression.OwnedRank(progression.Player.Skills, skillId);
            if (title != null) title.text = SkillName(skillId);
            if (content == null) return;
            foreach (RectTransform child in content) child.gameObject.SetActive(false);
            var body = new GameObject("SkillDetailText", typeof(RectTransform), typeof(Text));
            body.transform.SetParent(content, false);
            RectTransform bodyRect = (RectTransform)body.transform;
            bodyRect.anchorMin = new Vector2(0.08f, 0.32f);
            bodyRect.anchorMax = new Vector2(0.92f, 0.92f);
            bodyRect.offsetMin = Vector2.zero;
            bodyRect.offsetMax = Vector2.zero;
            Text text = body.GetComponent<Text>();
            text.font = CharacterPanelNodeFactory.UiFont;
            text.fontSize = 24;
            text.color = Color.white;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            var lines = new List<string>
            {
                "\u7c7b\u578b\uff1a" + (definition.ItemType == ActiveSkillItemType ? "\u6218\u6280" : "\u79d8\u6cd5"),
                "\u7b49\u9636\uff1a" + rank + " / " + definition.MaximumRank,
                "\u788e\u7247\uff1a" + ItemName(definition.PieceItemId) + " \u00d7"
                    + progression.Player.Inventory.Where(value => value.ItemId == definition.PieceItemId)
                        .Sum(value => value.Count)
            };
            OfflineSkillRankCost cost = definition.RankCosts?.FirstOrDefault(value => value.CurrentRank == rank);
            if (cost != null)
                lines.Add("\u4e0b\u4e00\u7b49\u9636\uff1a" + string.Join("\u3001", cost.Items.Select(item =>
                    ItemName(item.ItemId) + " \u00d7" + item.Count)));
            text.text = string.Join("\n", lines);
            Button install = CreatePanelButton(content, "\u5b89\u88c5", new Vector2(-160f, 40f),
                new Color(0.86f, 0.55f, 0.3f, 0.95f));
            install.onClick.AddListener(() =>
            {
                CloseSkillDetail();
                Install(skillId);
            });
            Button promote = CreatePanelButton(content, "\u57f9\u517b", new Vector2(0f, 40f),
                new Color(0.55f, 0.7f, 0.9f, 0.95f));
            promote.onClick.AddListener(() =>
            {
                CloseSkillDetail();
                OpenPromote(skillId);
            });
            Button close = CreatePanelButton(content, "\u5173\u95ed", new Vector2(160f, 40f),
                new Color(0.45f, 0.45f, 0.45f, 0.95f));
            close.onClick.AddListener(CloseSkillDetail);
        }

        private void CloseSkillDetail()
        {
            if (detailDialog != null) Destroy(detailDialog);
            detailDialog = null;
        }

        private void OpenPromote(int skillId)
        {
            OfflineSkillDefinition definition = progression.SkillDefinition(skillId);
            if (definition == null) return;
            ClosePromote();
            promoteSkillId = skillId;
            int rank = OfflineSkillProgression.OwnedRank(progression.Player.Skills, skillId);
            promoteDialog = new GameObject("SkillPromotePanel", typeof(RectTransform));
            promoteDialog.transform.SetParent(transform, false);
            RectTransform root = (RectTransform)promoteDialog.transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            var blocker = new GameObject("PromoteBlocker", typeof(RectTransform), typeof(Image), typeof(Button));
            blocker.transform.SetParent(root, false);
            RectTransform blockerRect = (RectTransform)blocker.transform;
            blockerRect.anchorMin = Vector2.zero;
            blockerRect.anchorMax = Vector2.one;
            blockerRect.offsetMin = Vector2.zero;
            blockerRect.offsetMax = Vector2.zero;
            blocker.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.5f);
            blocker.GetComponent<Button>().onClick.AddListener(ClosePromote);
            var nodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            var buttons = new Dictionary<string, Button>(StringComparer.Ordinal);
            factory.Build(CharacterPanelNodeFactory.ReadRoot(promoteTemplate), root, nodes, buttons);
            RectTransform middle = nodes.Values.FirstOrDefault(value => value.name == "MiddleGo");
            if (middle == null) return;
            var titleObject = new GameObject("PromoteTitle", typeof(RectTransform), typeof(Text));
            titleObject.transform.SetParent(middle, false);
            RectTransform titleRect = (RectTransform)titleObject.transform;
            titleRect.anchorMin = new Vector2(0f, 0.86f);
            titleRect.anchorMax = new Vector2(1f, 0.86f);
            titleRect.sizeDelta = new Vector2(0f, 54f);
            Text title = titleObject.GetComponent<Text>();
            title.font = CharacterPanelNodeFactory.UiFont;
            title.fontSize = 28;
            title.alignment = TextAnchor.MiddleCenter;
            title.color = Color.white;
            title.text = SkillName(skillId) + "  \u7b49\u9636 " + rank.ToString(CultureInfo.InvariantCulture)
                + " \u2192 " + Math.Min(definition.MaximumRank, rank + 1).ToString(CultureInfo.InvariantCulture);
            float top = 0.76f;
            OfflineSkillRankCost cost = definition.RankCosts?.FirstOrDefault(value => value.CurrentRank == rank);
            if (cost == null)
            {
                AddPromoteLine(middle, "\u5df2\u8fbe\u6700\u5927\u7b49\u9636\u3002", ref top,
                    new Color(1f, 0.6f, 0.55f));
            }
            else
            {
                foreach (OfflineSkillItemCount item in cost.Items)
                {
                    long owned = progression.Player.Inventory.Where(value => value.ItemId == item.ItemId)
                        .Sum(value => value.Count);
                    AddPromoteLine(middle, ItemName(item.ItemId) + "\uff1a" + owned.ToString("N0",
                        CultureInfo.InvariantCulture) + "/" + item.Count.ToString("N0", CultureInfo.InvariantCulture),
                        ref top, owned >= item.Count ? Color.white : new Color(1f, 0.6f, 0.55f));
                }
            }
            Button promote = CreatePanelButton(middle, "\u5347\u9636", new Vector2(-140f, 30f),
                new Color(0.86f, 0.55f, 0.3f, 0.95f));
            promote.onClick.AddListener(DoPromote);
            Button close = CreatePanelButton(middle, "\u5173\u95ed", new Vector2(140f, 30f),
                new Color(0.45f, 0.45f, 0.45f, 0.95f));
            close.onClick.AddListener(ClosePromote);
        }

        private void DoPromote()
        {
            ProgressionResult result = progression.PromoteSkill(promoteSkillId);
            SetStatus(result.Message ?? result.Status.ToString());
            if (!result.IsApplied) return;
            ClosePromote();
            Refresh();
            Changed?.Invoke();
        }

        private void ClosePromote()
        {
            if (promoteDialog != null) Destroy(promoteDialog);
            promoteDialog = null;
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
            text.font = CharacterPanelNodeFactory.UiFont;
            text.fontSize = 24;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.text = value;
            top -= 0.09f;
        }

        private Button CreatePanelButton(RectTransform parent, string label, Vector2 position, Color color)
        {
            var buttonObject = new GameObject("PromoteButton" + label, typeof(RectTransform), typeof(Image),
                typeof(Button));
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
            text.font = CharacterPanelNodeFactory.UiFont;
            text.fontSize = 26;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = label;
            return buttonObject.GetComponent<Button>();
        }

        private void ToggleFavorite(int skillId)
        {
            ProgressionResult result = progression.ToggleSkillFavorite(skillId);
            LastMessage = result.Message ?? result.Status.ToString();
            if (!result.IsApplied) return;
            Refresh();
            Changed?.Invoke();
        }

        private void SelectTab(int itemType)
        {
            ClearPendingInstall();
            listType = itemType;
            selectedType = 0;
            selectedSlot = -1;
            Refresh();
        }

        private void SelectSlot(int itemType, int slot)
        {
            if (pendingSkillId != 0 && pendingType == itemType)
            {
                int skillId = pendingSkillId;
                ClearPendingInstall();
                EquipToSlot(skillId, slot);
                return;
            }
            if (selectedType == itemType && selectedSlot == slot)
            {
                Unequip(itemType, slot);
                return;
            }
            selectedType = itemType;
            selectedSlot = slot;
            listType = itemType;
            Refresh();
        }

        private void Install(int skillId)
        {
            if (listType == PieceItemType) return;
            if (selectedType != listType || selectedSlot < 0)
            {
                BeginInstall(skillId);
                return;
            }
            EquipToSlot(skillId, selectedSlot);
        }

        private void BeginInstall(int skillId)
        {
            pendingSkillId = skillId;
            pendingType = listType;
            selectedType = pendingType;
            selectedSlot = -1;
            if (installMask == null && panel != null)
            {
                installMask = new GameObject("SkillInstallMask", typeof(RectTransform), typeof(Image));
                installMask.transform.SetParent(panel.transform, false);
                RectTransform maskRect = (RectTransform)installMask.transform;
                maskRect.anchorMin = Vector2.zero;
                maskRect.anchorMax = Vector2.one;
                maskRect.offsetMin = Vector2.zero;
                maskRect.offsetMax = Vector2.zero;
                installMask.transform.SetSiblingIndex(0);
                Image image = installMask.GetComponent<Image>();
                image.color = new Color(0f, 0f, 0f, 0.42f);
                image.raycastTarget = false;
                var hint = new GameObject("SkillInstallHint", typeof(RectTransform), typeof(Text));
                hint.transform.SetParent(installMask.transform, false);
                RectTransform hintRect = (RectTransform)hint.transform;
                hintRect.anchorMin = new Vector2(0.5f, 0.92f);
                hintRect.anchorMax = new Vector2(0.5f, 0.92f);
                hintRect.sizeDelta = new Vector2(420f, 50f);
                Text text = hint.GetComponent<Text>();
                text.font = CharacterPanelNodeFactory.UiFont;
                text.fontSize = 26;
                text.alignment = TextAnchor.MiddleCenter;
                text.color = Color.white;
                text.text = "选择技能槽安装";
            }
            Refresh();
        }

        private void ClearPendingInstall()
        {
            pendingSkillId = 0;
            pendingType = 0;
            if (recommendDialog != null) Destroy(recommendDialog);
            recommendDialog = null;
            if (detailDialog != null) Destroy(detailDialog);
            detailDialog = null;
            if (promoteDialog != null) Destroy(promoteDialog);
            promoteDialog = null;
            if (installMask != null) Destroy(installMask);
            installMask = null;
        }

        private void EquipToSlot(int skillId, int slot)
        {
            ProgressionResult result = progression.EquipSkill(skillId, slot);
            LastMessage = result.Message ?? result.Status.ToString();
            if (!result.IsApplied) return;
            selectedSlot = slot;
            selectedType = listType;
            Refresh();
            Changed?.Invoke();
        }

        private void Unequip(int itemType, int slot)
        {
            ProgressionResult result = progression.UnequipSkill(itemType, slot);
            LastMessage = result.Message ?? result.Status.ToString();
            if (!result.IsApplied) return;
            selectedSlot = slot;
            selectedType = itemType;
            Refresh();
            Changed?.Invoke();
        }

        private int FirstEmptySlot(int itemType)
        {
            int[] slots = itemType == ActiveSkillItemType
                ? progression.Player.Skills.Plans[plan].ActiveSkills
                : progression.Player.Skills.Plans[plan].PassiveSkills;
            for (int i = 0; i < slots.Length; i++) if (slots[i] == 0) return i;
            return -1;
        }

        private void SwitchPlan(int delta)
        {
            int target = (plan + delta + 3) % 3;
            ProgressionResult result = progression.SelectSkillPlan(target);
            LastMessage = result.Message ?? result.Status.ToString();
            if (!result.IsApplied) return;
            plan = target;
            selectedType = 0;
            selectedSlot = -1;
            Refresh();
            Changed?.Invoke();
        }

        private void BeginRename()
        {
            if (planInput == null) return;
            planInput.interactable = true;
            planInput.ActivateInputField();
        }

        private void CommitRename(string value)
        {
            if (planInput == null) return;
            planInput.interactable = false;
            if (string.IsNullOrWhiteSpace(value) || value.Trim() == progression.Player.Skills.Plans[plan].Name)
            {
                if (planName != null) planName.text = progression.Player.Skills.Plans[plan].Name;
                return;
            }
            ProgressionResult result = progression.RenameSkillPlan(plan, value);
            LastMessage = result.Message ?? result.Status.ToString();
            if (result.IsApplied) Changed?.Invoke();
            if (planName != null) planName.text = progression.Player.Skills.Plans[plan].Name;
        }

        private string SkillName(int itemId)
        {
            Dictionary<string, object> item = progression.Catalog.FindItem(itemId);
            string name = item == null ? itemId.ToString(CultureInfo.InvariantCulture)
                : Convert.ToString(item["Name"], CultureInfo.InvariantCulture);
            int rank = OfflineSkillProgression.OwnedRank(progression.Player.Skills, itemId);
            return rank > 1 ? name + " +" + (rank - 1).ToString(CultureInfo.InvariantCulture) : name;
        }

        private int Quality(int itemId)
        {
            Dictionary<string, object> item = progression.Catalog.FindItem(itemId);
            return item == null ? 0 : Convert.ToInt32(item["Quality"], CultureInfo.InvariantCulture);
        }

        private Text FindTextDeep(string name)
        {
            return FindDeep(name)?.GetComponent<Text>();
        }

        private RectTransform FindDeep(string name)
        {
            if (nodes.TryGetValue("/SkillContentPanel/" + name, out RectTransform exact)) return exact;
            return nodes.Where(pair => pair.Value.name == name).Select(pair => pair.Value).FirstOrDefault();
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
    }
}
