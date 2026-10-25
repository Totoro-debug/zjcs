using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Presentation.Skills;

namespace Zjcs.Presentation.Character
{
    /// <summary>
    /// Original <c>CharacterPanelSkillContent</c>: three skill plans, four battle-skill and four
    /// secret-skill slots, the skill list tabs and installing/replacing/removing a skill.
    /// </summary>
    public sealed class CharacterSkillPanel : MonoBehaviour
    {
        private sealed class SkillAnimationTrack
        {
            public Transform Target;
            public string Property;
            public AnimationCurve Curve;
        }

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
        private Dictionary<string, object> skillUiTemplates;
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
        private OfficialSkillQualityGradientData qualityGradient;
        private GameObject detailDialog;
        private GameObject pieceDialog;
        private GameObject mergeDialog;
        private GameObject resultDialog;
        private GameObject confirmationDialog;
        private Coroutine batchReveal;
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
            CharacterPanelNodeFactory.SetUiFont(assets.UiFont);
            factory = new CharacterPanelNodeFactory(assets);
            Dictionary<string, object> panels = CharacterModelPackage.AsObject(
                OfficialEquipmentJson.Parse(assets.CharacterPanels.text), "panels");
            Dictionary<string, object> screens = CharacterModelPackage.AsObject(panels["screens"], "screens");
            screen = CharacterModelPackage.AsObject(screens["skills"], "skills");
            listItemTemplate = CharacterModelPackage.AsObject(screens["skill_list_item"], "skill_list_item");
            itemTemplate = CharacterModelPackage.AsObject(screens["item"], "item");
            promoteTemplate = CharacterModelPackage.AsObject(screens["skill_promote"], "skill_promote");
            infoTemplate = CharacterModelPackage.AsObject(screens["character_props"], "character_props");
            skillUiTemplates = CharacterModelPackage.AsObject(
                OfficialEquipmentJson.Parse(assets.SkillUiLayouts.text), "skill UI layouts");
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
            Dictionary<string, object> skillPanelLayout = CharacterModelPackage.AsObject(
                skillUiTemplates["character_skills"], "character_skills");
            PlayOfficialLayoutAnimations("character_skills", skillPanelLayout, nodes,
                "CharacterPanelSkillPanel_Show");
            plan = progression.Player.Skills.SelectedPlan;
            Bind();
            Refresh();
        }

        public void Close()
        {
            CloseUpgradeConfirmation();
            if (recommendDialog != null) Destroy(recommendDialog);
            recommendDialog = null;
            if (mergeDialog != null) Destroy(mergeDialog);
            mergeDialog = null;
            if (resultDialog != null) Destroy(resultDialog);
            resultDialog = null;
            if (pieceDialog != null) Destroy(pieceDialog);
            pieceDialog = null;
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
            Changed?.Invoke();
        }

        public bool HandleEscape()
        {
            if (installMask != null)
            {
                ClearPendingInstall();
                return true;
            }
            if (recommendDialog != null)
            {
                CloseRecommendations();
                return true;
            }
            if (mergeDialog != null)
            {
                CloseMergeConfirm();
                return true;
            }
            if (resultDialog != null)
            {
                CloseResultDialog();
                return true;
            }
            if (pieceDialog != null)
            {
                ClosePieceDetail();
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
                if (planName != null)
                {
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
                SkillSlotDrag drag = slots[index].gameObject.AddComponent<SkillSlotDrag>();
                drag.ItemType = itemType;
                drag.Slot = slot;
                drag.Dropped = SwapSkillSlots;
                drag.DragLayer = panel.GetComponent<RectTransform>();
            }
        }

        private void SwapSkillSlots(int itemType, int fromSlot, int toSlot)
        {
            ProgressionResult result = progression.SwapSkillSlots(itemType, fromSlot, toSlot);
            SetStatus(result.Message ?? result.Status.ToString());
            if (!result.IsApplied) return;
            Refresh();
            Changed?.Invoke();
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
            RefreshFastAction();
        }

        private void RefreshFastAction()
        {
            int promotable = 0;
            OfflineSkillProfile profile = progression.Player.Skills;
            foreach (OfflineOwnedSkill skill in profile.Skills)
            {
                if (skill == null) continue;
                OfflineSkillDefinition definition = progression.SkillDefinition(skill.ClassId);
                if (definition == null || skill.Rank >= definition.MaximumRank) continue;
                OfflineSkillRankCost cost = definition.RankCosts?.FirstOrDefault(value => value.CurrentRank == skill.Rank);
                if (cost == null || cost.Items == null) continue;
                bool affordable = cost.Items.All(item => item != null
                    && progression.Player.Inventory.Where(value => value != null && value.ItemId == item.ItemId)
                        .Sum(value => value.Count) >= item.Count);
                if (affordable) promotable++;
            }

            bool fastPromotionUnlocked = progression.Player.Level >= 12;
            if (nodes.TryGetValue("/SkillContentPanel/FastPromotionBtn", out RectTransform promotion))
                promotion.gameObject.SetActive(fastPromotionUnlocked && promotable >= 5);
            if (nodes.TryGetValue("/SkillContentPanel/FastLevelUpBtn", out RectTransform levelUp))
                levelUp.gameObject.SetActive(!fastPromotionUnlocked || promotable < 5);
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
            var mergeable = new List<int>();
            var collected = new List<int>();
            var uncollected = new List<int>();
            foreach (OfflineSkillDefinition definition in progression.SkillRules?.Skills ?? Array.Empty<OfflineSkillDefinition>())
            {
                if (definition.ItemType != listType) continue;
                bool has = owned.ContainsKey(definition.ClassId);
                bool craftable = !has && HasPiece(definition.PieceItemId);
                bool available = OfflineSkillProgression.IsAvailableFor(definition, progression.SkillContext());
                if (!has && !available && !(showAll != null && showAll.isOn)) continue;
                if (has && Array.IndexOf(equipped, definition.ClassId) >= 0) continue;
                if (craftable) mergeable.Add(definition.ClassId);
                else if (OfflineSkillProgression.IsFavorite(profile, definition.ClassId)) collected.Add(definition.ClassId);
                else uncollected.Add(definition.ClassId);
            }
            OfflineSkillPlayerContext context = progression.SkillContext();
            Comparison<int> bySkillPanel = (left, right) =>
            {
                int canPromote = CanPromoteSkill(right).CompareTo(CanPromoteSkill(left));
                if (canPromote != 0) return canPromote;
                int available = IsAvailableForCurrentProfession(right, context)
                    .CompareTo(IsAvailableForCurrentProfession(left, context));
                if (available != 0) return available;
                int rank = OfflineSkillProgression.OwnedRank(profile, right)
                    .CompareTo(OfflineSkillProgression.OwnedRank(profile, left));
                if (rank != 0) return rank;
                int leftProfession = progression.SkillDefinition(left)?.AvailableProfessions?.DefaultIfEmpty(0).Min() ?? 0;
                int rightProfession = progression.SkillDefinition(right)?.AvailableProfessions?.DefaultIfEmpty(0).Min() ?? 0;
                int profession = rightProfession.CompareTo(leftProfession);
                return profession != 0 ? profession : left.CompareTo(right);
            };
            mergeable.Sort(bySkillPanel);
            collected.Sort(bySkillPanel);
            uncollected.Sort(bySkillPanel);
            foreach (int skillId in mergeable.Concat(collected).Concat(uncollected))
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
            OfflineSkillPlayerContext context = progression.SkillContext();
            var pieces = progression.Player.Inventory
                .Where(value => value != null && value.Count > 0
                    && progression.Catalog.ItemType(value.ItemId) == PieceItemType
                    && ((showAll != null && showAll.isOn) || IsPieceAvailable(value.ItemId, context)))
                .OrderByDescending(value => Quality(value.ItemId)).ThenByDescending(value => value.Count)
                .ThenBy(value => value.ItemId).ToList();
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
                OfflineSkillDefinition pieceDefinition = progression.SkillRules?.Skills
                    .FirstOrDefault(value => value.PieceItemId == stack.ItemId);
                if (pieceDefinition != null)
                {
                    Button rowButton = row.gameObject.GetComponent<Button>();
                    if (rowButton == null) rowButton = row.gameObject.AddComponent<Button>();
                    Image target = row.GetComponent<Image>() ?? row.GetComponentInChildren<Image>();
                    rowButton.targetGraphic = target;
                    if (target != null) target.raycastTarget = true;
                    int skillId = pieceDefinition.ClassId;
                    rowButton.onClick.AddListener(() => OpenPieceDetail(skillId));
                }
                LayoutElement element = row.GetComponent<LayoutElement>() ?? row.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = 150f;
                element.minHeight = 150f;
                listRows.Add(row.gameObject);
            }
            if (emptyTip != null) emptyTip.SetActive(listRows.Count == 0);
        }

        private bool IsPieceAvailable(int itemId, OfflineSkillPlayerContext context)
        {
            OfflineSkillDefinition definition = progression.SkillRules?.Skills
                .FirstOrDefault(value => value.PieceItemId == itemId);
            return definition != null && OfflineSkillProgression.IsAvailableFor(definition, context);
        }

        private bool CanPromoteSkill(int skillId)
        {
            OfflineSkillDefinition definition = progression.SkillDefinition(skillId);
            int rank = OfflineSkillProgression.OwnedRank(progression.Player.Skills, skillId);
            OfflineSkillRankCost cost = definition?.RankCosts?.FirstOrDefault(value => value.CurrentRank == rank);
            return rank > 0 && cost != null && cost.Items.All(item => item != null
                && progression.Player.Inventory.Where(value => value != null && value.ItemId == item.ItemId)
                    .Sum(value => value.Count) >= item.Count);
        }

        private bool IsAvailableForCurrentProfession(int skillId, OfflineSkillPlayerContext context)
        {
            return OfflineSkillProgression.IsAvailableFor(progression.SkillDefinition(skillId), context);
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
            OfflineSkillProfile profile = progression.Player.Skills;
            OfflineSkillPlayerContext context = progression.SkillContext();
            int maximumBaseLevel = progression.SkillRules?.BaseLevelLimits?
                .Where(value => value != null && value.SubRank == context.SubRank)
                .Select(value => value.SkillLevel).DefaultIfEmpty(0).Max() ?? 0;
            bool blessingOnly = maximumBaseLevel > 0
                && profile.ActiveSlotLevels.All(value => value >= maximumBaseLevel)
                && profile.PassiveSlotLevels.All(value => value >= maximumBaseLevel);
            List<OfflineSkillSlotUpgradeChange> preview = progression.PreviewUpgradeAllSkillSlotChanges().ToList();
            if (blessingOnly && preview.Count >= 40)
            {
                OpenUpgradeConfirmation(() => ApplyUpgradeAllSlots(preview));
                return;
            }
            ApplyUpgradeAllSlots(preview);
        }

        private void ApplyUpgradeAllSlots(IReadOnlyList<OfflineSkillSlotUpgradeChange> preview)
        {
            ProgressionResult result = progression.UpgradeAllSkillSlots();
            SetStatus(result.Message ?? result.Status.ToString());
            if (!result.IsApplied) return;
            Refresh();
            if (assets.SkillSlotLevelUpAnimation != null)
                StartCoroutine(PlaySlotUpgradeEffects(preview));
            Changed?.Invoke();
        }

        private IEnumerator PlaySlotUpgradeEffects(IReadOnlyList<OfflineSkillSlotUpgradeChange> changes)
        {
            AnimationClip clip = assets.SkillSlotLevelUpAnimation;
            foreach (OfflineSkillSlotUpgradeChange change in changes)
            {
                RectTransform group = change.ItemType == ActiveSkillItemType ? activeGroup : passiveGroup;
                if (group != null)
                {
                    RectTransform row = group.Cast<Transform>().OfType<RectTransform>()
                        .Where(value => value.name.StartsWith("SkillContentPanelSkillGroupItem",
                            StringComparison.Ordinal)).ElementAtOrDefault(change.Slot);
                    if (row != null)
                    {
                        Animation animation = row.GetComponent<Animation>();
                        if (animation == null) animation = row.gameObject.AddComponent<Animation>();
                        if (animation.GetClip(clip.name) == null) animation.AddClip(clip, clip.name);
                        animation.Play(clip.name);
                        if (assets.SkillSlotLevelUpSound != null)
                        {
                            AudioSource source = row.GetComponent<AudioSource>();
                            if (source == null) source = row.gameObject.AddComponent<AudioSource>();
                            source.playOnAwake = false;
                            source.spatialBlend = 0f;
                            source.PlayOneShot(assets.SkillSlotLevelUpSound);
                        }
                    }
                }
                yield return new WaitForSecondsRealtime(0.06f);
            }
            if (changes.Count > 0 && clip != null)
                yield return new WaitForSecondsRealtime(clip.length);
        }

        private void OpenUpgradeConfirmation(Action confirmed)
        {
            if (confirmationDialog != null) Destroy(confirmationDialog);
            if (assets.ConfirmDialogPrefab == null)
            {
                SetStatus("缺少官方确认弹窗资源，未执行一键升级。");
                return;
            }
            confirmationDialog = Instantiate(assets.ConfirmDialogPrefab, transform, false);
            confirmationDialog.name = "SkillFastLevelUpConfirmation";
            confirmationDialog.transform.SetAsLastSibling();
            SetTextValue(FindText(confirmationDialog.transform, "TitleText"), "确认");
            SetTextValue(FindText(confirmationDialog.transform, "ContentTxt"), "一键升级将消耗大量材料，是否继续？");
            Transform confirmNode = FindDeep(confirmationDialog.transform, "ConfirmBtn");
            Transform cancelNode = FindDeep(confirmationDialog.transform, "CancelBtn");
            BindConfirmationButton(confirmNode, () =>
            {
                CloseUpgradeConfirmation();
                confirmed?.Invoke();
            });
            BindConfirmationButton(cancelNode, CloseUpgradeConfirmation);
        }

        private static Text FindText(Transform root, string nodeName)
        {
            Transform node = FindDeep(root, nodeName);
            return node == null ? null : node.GetComponentInChildren<Text>(true);
        }

        private static void BindConfirmationButton(Transform node, Action action)
        {
            if (node == null) return;
            Button button = node.GetComponent<Button>();
            if (button == null) button = node.gameObject.AddComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(() => action?.Invoke());
        }

        private void CloseUpgradeConfirmation()
        {
            if (confirmationDialog != null) Destroy(confirmationDialog);
            confirmationDialog = null;
        }

        private void PromoteAllSkills()
        {
            var before = progression.Player.Skills.Skills.Where(value => value != null)
                .ToDictionary(value => value.ClassId, value => value.Rank);
            ProgressionResult result = progression.PromoteAllSkills();
            SetStatus(result.Message ?? result.Status.ToString());
            if (!result.IsApplied) return;
            var promoted = progression.Player.Skills.Skills.Where(value => value != null
                    && before.TryGetValue(value.ClassId, out int oldRank) && value.Rank > oldRank)
                .Select(value => new SkillRankChange(value.ClassId, before[value.ClassId], value.Rank))
                .OrderByDescending(value => EquippedSkillUseCount(value.SkillId))
                .ThenByDescending(value => value.ToRank).ThenBy(value => value.SkillId).ToList();
            Refresh();
            Changed?.Invoke();
            if (promoted.Count > 0) OpenBatchPromotionResult(promoted);
        }

        private int EquippedSkillUseCount(int skillId)
        {
            int count = 0;
            foreach (OfflineSkillPlan skillPlan in progression.Player.Skills.Plans ?? Array.Empty<OfflineSkillPlan>())
            {
                if (skillPlan == null) continue;
                count += (skillPlan.ActiveSkills ?? Array.Empty<int>()).Count(value => value == skillId);
                count += (skillPlan.PassiveSkills ?? Array.Empty<int>()).Count(value => value == skillId);
            }
            return count;
        }

        private sealed class SkillRankChange
        {
            public readonly int SkillId;
            public readonly int FromRank;
            public readonly int ToRank;

            public SkillRankChange(int skillId, int fromRank, int toRank)
            {
                SkillId = skillId;
                FromRank = fromRank;
                ToRank = toRank;
            }
        }

        private void OpenBatchPromotionResult(List<SkillRankChange> promoted)
        {
            if (batchReveal != null) StopCoroutine(batchReveal);
            batchReveal = null;
            CloseResultDialog();
            resultDialog = BuildOfficialDialog("skill_batch_notify", out var resultNodes, out var resultButtons);
            RectTransform content = DialogNodeEnding(resultNodes,
                "/ContentGroup/ItemGrid/ViewPort/Content");
            var resultRows = new List<CanvasGroup>();
            if (content != null)
            {
                foreach (SkillRankChange change in promoted)
                {
                    Dictionary<string, object> template = CharacterModelPackage.AsObject(
                        skillUiTemplates["skill_batch_item"], "skill_batch_item");
                    var itemNodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
                    var itemButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
                    RectTransform row = factory.Build(CharacterPanelNodeFactory.ReadRoot(template), content,
                        itemNodes, itemButtons);
                    Transform icon = DialogNode(itemNodes, "ItemIconSkill") ?? row;
                    BuildSkillIcon(change.SkillId, icon, "PromotedSkillIcon");
                    SetTextValue(DialogNode(itemNodes, "BottomText")?.GetComponent<Text>(), SkillName(change.SkillId));
                    SetTextValue(DialogNode(itemNodes, "CountText")?.GetComponent<Text>(), change.ToRank.ToString(CultureInfo.InvariantCulture));
                    SetTextValue(DialogNode(itemNodes, "DebugRankText")?.GetComponent<Text>(), change.ToRank.ToString(CultureInfo.InvariantCulture));
                    SetTextValue(DialogNode(itemNodes, "Count")?.GetComponent<Text>(), change.ToRank.ToString(CultureInfo.InvariantCulture));
                    CanvasGroup rowAlpha = row.GetComponent<CanvasGroup>();
                    if (rowAlpha == null) rowAlpha = row.gameObject.AddComponent<CanvasGroup>();
                    rowAlpha.alpha = 0f;
                    resultRows.Add(rowAlpha);
                }
            }
            Button close = DialogButton(resultButtons, "CloseObj");
            if (close != null)
            {
                close.gameObject.SetActive(false);
                close.onClick.AddListener(CloseResultDialog);
            }
            Button skip = DialogButton(resultButtons, "SkipBtn");
            if (skip != null) skip.gameObject.SetActive(resultRows.Count > 0);
            batchReveal = StartCoroutine(RevealBatchPromotionRows(resultRows, resultNodes, close, skip));
            if (skip != null) skip.onClick.AddListener(() =>
            {
                if (batchReveal != null) StopCoroutine(batchReveal);
                batchReveal = null;
                foreach (CanvasGroup row in resultRows) if (row != null) row.alpha = 1f;
                ScrollSkillResultsToEnd(resultNodes);
                skip.gameObject.SetActive(false);
                if (close != null) close.gameObject.SetActive(true);
            });
        }

        private IEnumerator RevealBatchPromotionRows(List<CanvasGroup> rows,
            Dictionary<string, RectTransform> resultNodes,
            Button closeButton, Button skipButton)
        {
            foreach (CanvasGroup row in rows)
            {
                yield return new WaitForSecondsRealtime(0.06f);
                if (row != null) row.alpha = 1f;
            }
            ScrollSkillResultsToEnd(resultNodes);
            batchReveal = null;
            if (skipButton != null) skipButton.gameObject.SetActive(false);
            if (closeButton != null) closeButton.gameObject.SetActive(true);
        }

        private static void ScrollSkillResultsToEnd(Dictionary<string, RectTransform> nodes)
        {
            RectTransform grid = DialogNode(nodes, "ItemGrid");
            ScrollRect scroll = grid == null ? null : grid.GetComponent<ScrollRect>();
            if (scroll != null) scroll.verticalNormalizedPosition = 0f;
        }

        private void OpenRankPromotionResult(int skillId, int fromRank, int toRank)
        {
            CloseResultDialog();
            resultDialog = BuildOfficialDialog("skill_rank_notify", out var resultNodes, out var resultButtons);
            SetTextValue(DialogNode(resultNodes, "SkillNameText")?.GetComponent<Text>(), SkillName(skillId));
            SetTextValue(DialogNode(resultNodes, "FromRankText")?.GetComponent<Text>(), fromRank.ToString(CultureInfo.InvariantCulture));
            SetTextValue(DialogNode(resultNodes, "ToRankText")?.GetComponent<Text>(), toRank.ToString(CultureInfo.InvariantCulture));
            int skillQuality = Quality(skillId);
            ApplyQualityGradient(DialogNode(resultNodes, "FromRankText")?.GetComponent<Text>(), skillQuality, 0f);
            ApplyQualityGradient(DialogNode(resultNodes, "ToRankText")?.GetComponent<Text>(), skillQuality, 0f);
            Transform icon = DialogNode(resultNodes, "ItemIconSkill");
            if (icon != null) BuildSkillIcon(skillId, icon, "PromotedSkillIcon");
            DialogButton(resultButtons, "CloseObj")?.onClick.AddListener(CloseResultDialog);
        }

        private void CloseResultDialog()
        {
            if (batchReveal != null) StopCoroutine(batchReveal);
            batchReveal = null;
            if (resultDialog != null) Destroy(resultDialog);
            resultDialog = null;
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

        private GameObject BuildOfficialDialog(string layoutKey,
            out Dictionary<string, RectTransform> dialogNodes,
            out Dictionary<string, Button> dialogButtons)
        {
            if (!skillUiTemplates.TryGetValue(layoutKey, out object raw))
                throw new InvalidOperationException("Official skill UI layout is missing: " + layoutKey);
            Dictionary<string, object> template = CharacterModelPackage.AsObject(raw, layoutKey);
            GameObject dialog = new GameObject(layoutKey, typeof(RectTransform));
            dialog.transform.SetParent(transform, false);
            RectTransform root = (RectTransform)dialog.transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            dialogNodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            dialogButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
            factory.Build(CharacterPanelNodeFactory.ReadRoot(template), root, dialogNodes, dialogButtons);
            PlayOfficialLayoutAnimations(layoutKey, template, dialogNodes);
            return dialog;
        }

        private void PlayOfficialLayoutAnimations(string layoutKey, Dictionary<string, object> template,
            Dictionary<string, RectTransform> layoutNodes, string animationName = null)
        {
            if (!template.TryGetValue("root", out object rootValue)
                || !(rootValue is Dictionary<string, object> root)
                || !root.TryGetValue("animations", out object animationValue)
                || !(animationValue is List<object> animations)) return;
            List<Dictionary<string, object>> clips = animations
                .Where(value => value is Dictionary<string, object>)
                .Select(value => (Dictionary<string, object>)value).ToList();
            Dictionary<string, object> clip = animationName == null
                ? clips.FirstOrDefault(value => !CharacterPanelNodeFactory.Boolean(value, "loop", false))
                : clips.FirstOrDefault(value => CharacterPanelNodeFactory.String(value, "name") == animationName);
            if (clip == null || !TryBuildAnimationTracks(clip, layoutNodes, out List<SkillAnimationTrack> tracks,
                out float duration)) return;
            bool loop = CharacterPanelNodeFactory.Boolean(clip, "loop", false);
            if (loop) StartCoroutine(PlaySkillAnimation(tracks, duration, true));
            else
            {
                Dictionary<string, object> loopClip = clips.FirstOrDefault(value =>
                    CharacterPanelNodeFactory.Boolean(value, "loop", false));
                if (loopClip != null && TryBuildAnimationTracks(loopClip, layoutNodes,
                    out List<SkillAnimationTrack> loopTracks, out float loopDuration))
                    StartCoroutine(PlaySkillAnimationAfter(tracks, duration, loopTracks, loopDuration));
            }
        }

        private static bool TryBuildAnimationTracks(Dictionary<string, object> clip,
            Dictionary<string, RectTransform> layoutNodes, out List<SkillAnimationTrack> tracks, out float duration)
        {
            tracks = new List<SkillAnimationTrack>();
            duration = CharacterPanelNodeFactory.Number(clip, "duration", 0f);
            if (!clip.TryGetValue("tracks", out object rawTracks) || !(rawTracks is List<object> sourceTracks))
                return false;
            foreach (object rawTrack in sourceTracks)
            {
                if (!(rawTrack is Dictionary<string, object> source)
                    || !source.TryGetValue("keys", out object rawKeys) || !(rawKeys is List<object> keys)
                    || keys.Count == 0) continue;
                string path = CharacterPanelNodeFactory.String(source, "path");
                string property = CharacterPanelNodeFactory.String(source, "property");
                if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(property)) continue;
                RectTransform target = layoutNodes.TryGetValue(path, out RectTransform exact) ? exact : null;
                const string skillPanelContentPath = "/CharacterPanelSkillPanel/Content";
                if (target == null && path.StartsWith(skillPanelContentPath, StringComparison.Ordinal))
                    layoutNodes.TryGetValue(path.Substring(skillPanelContentPath.Length), out target);
                if (target == null)
                    target = layoutNodes.FirstOrDefault(value => path.EndsWith("/" + value.Value.name,
                        StringComparison.Ordinal)).Value;
                if (target == null) continue;
                var keyframes = new List<Keyframe>(keys.Count);
                foreach (object rawKey in keys)
                {
                    if (!(rawKey is List<object> values) || values.Count < 2) continue;
                    float time = Convert.ToSingle(values[0], CultureInfo.InvariantCulture);
                    float value = Convert.ToSingle(values[1], CultureInfo.InvariantCulture);
                    float inSlope = values.Count > 2 ? Convert.ToSingle(values[2], CultureInfo.InvariantCulture) : 0f;
                    float outSlope = values.Count > 3 ? Convert.ToSingle(values[3], CultureInfo.InvariantCulture) : 0f;
                    keyframes.Add(new Keyframe(time, value, inSlope, outSlope));
                    duration = Mathf.Max(duration, time);
                }
                if (keyframes.Count == 0) continue;
                tracks.Add(new SkillAnimationTrack
                {
                    Target = target,
                    Property = property,
                    Curve = new AnimationCurve(keyframes.ToArray())
                });
            }
            return tracks.Count > 0 && duration > 0f;
        }

        private IEnumerator PlaySkillAnimationAfter(List<SkillAnimationTrack> intro, float introDuration,
            List<SkillAnimationTrack> loopTracks, float loopDuration)
        {
            yield return PlaySkillAnimation(intro, introDuration, false);
            yield return PlaySkillAnimation(loopTracks, loopDuration, true);
        }

        private IEnumerator PlaySkillAnimation(List<SkillAnimationTrack> tracks, float duration, bool loop)
        {
            float elapsed = 0f;
            do
            {
                float time = loop ? elapsed % duration : Mathf.Min(elapsed, duration);
                foreach (SkillAnimationTrack track in tracks)
                    ApplySkillAnimationValue(track.Target, track.Property, track.Curve.Evaluate(time));
                if (!loop && elapsed >= duration) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            while (true);
        }

        private static void ApplySkillAnimationValue(Transform target, string property, float value)
        {
            if (target == null) return;
            RectTransform rect = target as RectTransform;
            if (property == "m_AnchoredPosition.x" || property == "m_AnchoredPosition.y")
            {
                if (rect == null) return;
                Vector2 position = rect.anchoredPosition;
                if (property.EndsWith(".x", StringComparison.Ordinal)) position.x = value;
                else position.y = value;
                rect.anchoredPosition = position;
            }
            else if (property == "scale.x" || property == "scale.y" || property == "scale.z")
            {
                Vector3 scale = target.localScale;
                if (property.EndsWith(".x", StringComparison.Ordinal)) scale.x = value;
                else if (property.EndsWith(".y", StringComparison.Ordinal)) scale.y = value;
                else scale.z = value;
                target.localScale = scale;
            }
            else if (property.StartsWith("rotation.", StringComparison.Ordinal))
            {
                Vector3 euler = target.localEulerAngles;
                if (property.EndsWith(".x", StringComparison.Ordinal)) euler.x = value;
                else if (property.EndsWith(".y", StringComparison.Ordinal)) euler.y = value;
                else if (property.EndsWith(".z", StringComparison.Ordinal)) euler.z = value;
                target.localEulerAngles = euler;
            }
            else if (property.StartsWith("quaternion.", StringComparison.Ordinal))
            {
                Quaternion rotation = target.localRotation;
                if (property.EndsWith(".x", StringComparison.Ordinal)) rotation.x = value;
                else if (property.EndsWith(".y", StringComparison.Ordinal)) rotation.y = value;
                else if (property.EndsWith(".z", StringComparison.Ordinal)) rotation.z = value;
                else if (property.EndsWith(".w", StringComparison.Ordinal)) rotation.w = value;
                target.localRotation = rotation;
            }
            else if (property == "m_Alpha")
            {
                CanvasGroup group = target.GetComponent<CanvasGroup>();
                if (group == null) group = target.gameObject.AddComponent<CanvasGroup>();
                group.alpha = value;
            }
            else if (property == "m_IsActive") target.gameObject.SetActive(value >= 0.5f);
            else if (property.StartsWith("m_Color.", StringComparison.Ordinal))
            {
                Graphic graphic = target.GetComponent<Graphic>();
                if (graphic == null) return;
                Color color = graphic.color;
                if (property.EndsWith(".r", StringComparison.Ordinal)) color.r = value;
                else if (property.EndsWith(".g", StringComparison.Ordinal)) color.g = value;
                else if (property.EndsWith(".b", StringComparison.Ordinal)) color.b = value;
                else if (property.EndsWith(".a", StringComparison.Ordinal)) color.a = value;
                graphic.color = color;
            }
        }

        private static Button DialogButton(Dictionary<string, Button> dialogButtons, string nodeName)
        {
            return dialogButtons.FirstOrDefault(pair => pair.Key.EndsWith("/" + nodeName,
                StringComparison.Ordinal)).Value;
        }

        private static RectTransform DialogNode(Dictionary<string, RectTransform> dialogNodes,
            string nodeName)
        {
            return dialogNodes.Values.FirstOrDefault(value => value.name == nodeName);
        }

        private static RectTransform DialogNodeEnding(Dictionary<string, RectTransform> dialogNodes,
            string pathSuffix)
        {
            return dialogNodes.FirstOrDefault(pair => pair.Key.EndsWith(pathSuffix,
                StringComparison.Ordinal)).Value;
        }

        private void OpenSkillDetail(int skillId)
        {
            OfflineSkillDefinition definition = progression.SkillDefinition(skillId);
            if (definition == null) return;
            CloseSkillDetail();
            detailDialog = BuildOfficialDialog("skill_panel_detail", out var dialogNodes, out var dialogButtons);
            Text name = DialogNode(dialogNodes, "NameText")?.GetComponent<Text>();
            Text level = DialogNode(dialogNodes, "LevelText")?.GetComponent<Text>();
            Text rank = DialogNode(dialogNodes, "Count")?.GetComponent<Text>();
            Text detail = DialogNode(dialogNodes, "DetailText")?.GetComponent<Text>();
            Text quality = DialogNode(dialogNodes, "QualityText")?.GetComponent<Text>();
            int qualityLevel = Quality(skillId);
            ApplyQualityGradient(quality, qualityLevel, 45f);
            int ownedRank = OfflineSkillProgression.OwnedRank(progression.Player.Skills, skillId);
            SetTextValue(name, SkillName(skillId));
            SetTextValue(rank, ownedRank.ToString(CultureInfo.InvariantCulture));
            OfflineSkillRankCost nextCost = definition.RankCosts?.FirstOrDefault(value => value.CurrentRank == ownedRank);
            if (detail != null)
            {
                long pieces = progression.Player.Inventory.Where(value => value != null
                    && value.ItemId == definition.PieceItemId).Sum(value => value.Count);
                detail.text = (definition.ItemType == ActiveSkillItemType ? "战技" : "秘法")
                    + "\n等阶 " + ownedRank.ToString(CultureInfo.InvariantCulture) + "/"
                    + definition.MaximumRank.ToString(CultureInfo.InvariantCulture)
                    + "\n碎片 " + pieces.ToString("N0", CultureInfo.InvariantCulture)
                    + (nextCost == null ? "\n已达最高等阶。" : "\n下一阶消耗：" + string.Join("、",
                        nextCost.Items.Select(item => ItemName(item.ItemId) + " ×"
                            + item.Count.ToString("N0", CultureInfo.InvariantCulture))));
            }
            int[] equippedSlots = definition.ItemType == ActiveSkillItemType
                ? progression.Player.Skills.Plans[progression.Player.Skills.SelectedPlan].ActiveSkills
                : progression.Player.Skills.Plans[progression.Player.Skills.SelectedPlan].PassiveSkills;
            int equippedSlot = Array.IndexOf(equippedSlots, skillId);
            int currentLevel = equippedSlot < 0 ? 0 : progression.SkillProgression.SlotLevel(
                progression.Player.Skills, definition.ItemType, equippedSlot,
                progression.SkillContext().CurrentSeasonId);
            SetTextValue(level, currentLevel <= 0 ? string.Empty
                : "Lv" + currentLevel.ToString(CultureInfo.InvariantCulture));
            Transform iconSlot = DialogNode(dialogNodes, "ItemIconSkill") ?? DialogNode(dialogNodes, "IconSlot");
            if (iconSlot != null) BuildSkillIcon(skillId, iconSlot, "OfficialSkillIcon");

            bool owned = ownedRank > 0;
            bool equipped = progression.Player.Skills.Plans[progression.Player.Skills.SelectedPlan].ActiveSkills
                .Concat(progression.Player.Skills.Plans[progression.Player.Skills.SelectedPlan].PassiveSkills)
                .Contains(skillId);
            SetDialogNodeActive(dialogNodes, "UpgradeBtn", owned);
            SetDialogNodeActive(dialogNodes, "EquipBtn", owned && !equipped);
            SetDialogNodeActive(dialogNodes, "ReplaceBtn", owned && !equipped && selectedSlot >= 0);
            SetDialogNodeActive(dialogNodes, "TakeOffBtn", equipped);
            SetDialogNodeActive(dialogNodes, "DecomposeBtn", false);
            SetDialogNodeActive(dialogNodes, "CommonBtn_11", !owned);

            DialogButton(dialogButtons, "CollectionBtn")?.onClick.AddListener(() =>
            {
                ToggleFavorite(skillId);
                CloseSkillDetail();
            });
            DialogButton(dialogButtons, "UpgradeBtn")?.onClick.AddListener(() =>
            {
                CloseSkillDetail();
                OpenPromote(skillId);
            });
            DialogButton(dialogButtons, "EquipBtn")?.onClick.AddListener(() =>
            {
                CloseSkillDetail();
                Install(skillId);
            });
            DialogButton(dialogButtons, "ReplaceBtn")?.onClick.AddListener(() =>
            {
                CloseSkillDetail();
                Install(skillId);
            });
            DialogButton(dialogButtons, "TakeOffBtn")?.onClick.AddListener(() =>
            {
                int[] slots = definition.ItemType == ActiveSkillItemType
                    ? progression.Player.Skills.Plans[plan].ActiveSkills
                    : progression.Player.Skills.Plans[plan].PassiveSkills;
                int slot = Array.IndexOf(slots, skillId);
                if (slot >= 0) Unequip(definition.ItemType, slot);
                CloseSkillDetail();
            });
            DialogButton(dialogButtons, "CommonBtn_11")?.onClick.AddListener(() =>
            {
                CloseSkillDetail();
                OpenMergeConfirm(skillId);
            });
            return;
        }

        private void ApplyQualityGradient(Text target, int quality, float angle)
        {
            if (target == null) return;
            OfficialSkillQualityTextGradient effect = target.GetComponent<OfficialSkillQualityTextGradient>();
            if (quality != 7)
            {
                if (effect != null) Destroy(effect);
                return;
            }
            if (qualityGradient == null)
                qualityGradient = OfficialSkillAssetCatalog.LoadDefault().LoadQualityGradient();
            if (effect == null) effect = target.gameObject.AddComponent<OfficialSkillQualityTextGradient>();
            effect.Configure(target, qualityGradient, angle);
        }

        private static Text FindTextNodeEnding(Dictionary<string, RectTransform> dialogNodes, string suffix)
        {
            RectTransform node = dialogNodes.FirstOrDefault(pair =>
                pair.Key.EndsWith(suffix, StringComparison.Ordinal)).Value;
            return node == null ? null : node.GetComponent<Text>();
        }

        private static void SetTextValue(Text text, string value)
        {
            if (text != null) text.text = value ?? string.Empty;
        }

        private bool HasAffordableSkillCost(OfflineSkillItemCount[] items)
        {
            return items != null && items.Length > 0 && items.All(item => item != null && item.Count > 0
                && progression.Player.Inventory.Where(value => value != null && value.ItemId == item.ItemId)
                    .Sum(value => value.Count) >= item.Count);
        }

        private static void SetDialogNodeActive(Dictionary<string, RectTransform> dialogNodes,
            string nodeName, bool active)
        {
            RectTransform node = DialogNode(dialogNodes, nodeName);
            if (node != null) node.gameObject.SetActive(active);
        }

        private void OpenPieceDetail(int skillId)
        {
            OfflineSkillDefinition definition = progression.SkillDefinition(skillId);
            if (definition == null) return;
            ClosePieceDetail();
            pieceDialog = BuildOfficialDialog("skill_piece_detail", out var dialogNodes, out var dialogButtons);
            int rank = OfflineSkillProgression.OwnedRank(progression.Player.Skills, skillId);
            long count = progression.Player.Inventory.Where(value => value != null
                && value.ItemId == definition.PieceItemId).Sum(value => value.Count);
            SetTextValue(DialogNode(dialogNodes, "NameText")?.GetComponent<Text>(), ItemName(definition.PieceItemId));
            SetTextValue(DialogNode(dialogNodes, "CountText")?.GetComponent<Text>(),
                count.ToString("N0", CultureInfo.InvariantCulture));
            SetTextValue(DialogNode(dialogNodes, "TitleText")?.GetComponent<Text>(), ItemName(definition.PieceItemId));
            Transform icon = DialogNode(dialogNodes, "Piece") ?? DialogNode(dialogNodes, "PieceIcon");
            Image iconImage = icon == null ? null : icon.GetComponent<Image>();
            string iconUri = progression.Catalog.ItemIconUri(definition.PieceItemId);
            if (iconImage != null) iconImage.sprite = assets.FindSprite(iconUri);

            bool owned = rank > 0;
            OfflineSkillRankCost nextCost = definition.RankCosts?.FirstOrDefault(value => value.CurrentRank == rank);
            bool canRank = owned && nextCost != null && nextCost.Items.All(item => item != null
                && progression.Player.Inventory.Where(value => value != null && value.ItemId == item.ItemId)
                    .Sum(value => value.Count) >= item.Count);
            int mergeCount = progression.SkillMergePiece(skillId);
            bool canMerge = !owned && mergeCount > 0;
            SetDialogNodeActive(dialogNodes, "CommonBtn_12_RankUp", canRank);
            SetDialogNodeActive(dialogNodes, "CommonBtn_12_Merge", canMerge);
            SetDialogNodeActive(dialogNodes, "CommonBtn_13_Max", owned && nextCost == null);
            SetDialogNodeActive(dialogNodes, "DecomposeBtn", false);
            SetDialogNodeActive(dialogNodes, "CommonBtn_11", false);
            SetDialogNodeActive(dialogNodes, "RankUplDialogGridLayout", owned);
            SetDialogNodeActive(dialogNodes, "MergeDetailDialogGridLayout", !owned);
            Slider rankSlider = DialogNode(dialogNodes, "RankUplDialogGridLayout")?.GetComponentInChildren<Slider>(true);
            Slider mergeSlider = DialogNode(dialogNodes, "MergeDetailDialogGridLayout")?.GetComponentInChildren<Slider>(true);
            ConfigureProgressSlider(rankSlider, count, nextCost?.Items?.FirstOrDefault(value => value.ItemId == definition.PieceItemId)?.Count ?? 0);
            ConfigureProgressSlider(mergeSlider, count, mergeCount);
            DialogButton(dialogButtons, "CommonBtn_12_RankUp")?.onClick.AddListener(() => OpenPromote(skillId));
            DialogButton(dialogButtons, "CommonBtn_12_Merge")?.onClick.AddListener(() => OpenMergeConfirm(skillId));
        }

        private static void ConfigureProgressSlider(Slider slider, long current, long required)
        {
            if (slider == null || required <= 0) return;
            slider.minValue = 0;
            slider.maxValue = required;
            slider.SetValueWithoutNotify(Math.Min(current, required));
        }

        private void OpenMergeConfirm(int skillId)
        {
            OfflineSkillDefinition definition = progression.SkillDefinition(skillId);
            if (definition == null || progression.SkillMergePiece(skillId) <= 0) return;
            if (mergeDialog != null) Destroy(mergeDialog);
            mergeDialog = BuildOfficialDialog("skill_merge_confirm", out var dialogNodes, out var dialogButtons);
            SetTextValue(DialogNode(dialogNodes, "TitleText")?.GetComponent<Text>(), "合成技能");
            Transform pieceIcon = DialogNode(dialogNodes, "ItemIconSkillPiece");
            Transform skillIcon = DialogNode(dialogNodes, "ItemIconSkill");
            if (pieceIcon != null) BuildSkillIcon(definition.PieceItemId, pieceIcon, "PieceIcon");
            if (skillIcon != null) BuildSkillIcon(skillId, skillIcon, "SkillIcon");
            SetTextValue(DialogNode(dialogNodes, "CountText")?.GetComponent<Text>(),
                progression.SkillMergePiece(skillId).ToString(CultureInfo.InvariantCulture));
            DialogButton(dialogButtons, "CancelBtn")?.onClick.AddListener(CloseMergeConfirm);
            Button mergeButton = DialogButton(dialogButtons, "MergeBtn");
            long ownedPieces = progression.Player.Inventory.Where(value => value != null
                && value.ItemId == definition.PieceItemId).Sum(value => value.Count);
            if (mergeButton != null) mergeButton.interactable = ownedPieces >= progression.SkillMergePiece(skillId);
            bool submitting = false;
            mergeButton?.onClick.AddListener(() =>
            {
                if (submitting) return;
                submitting = true;
                mergeButton.interactable = false;
                ProgressionResult result = progression.MergeSkill(skillId);
                SetStatus(result.Message ?? result.Status.ToString());
                if (!result.IsApplied)
                {
                    submitting = false;
                    mergeButton.interactable = ownedPieces >= progression.SkillMergePiece(skillId);
                    return;
                }
                CloseMergeConfirm();
                if (pieceDialog != null) ClosePieceDetail();
                Refresh();
                Changed?.Invoke();
            });
        }

        private void CloseMergeConfirm()
        {
            if (mergeDialog != null) Destroy(mergeDialog);
            mergeDialog = null;
        }

        private void ClosePieceDetail()
        {
            if (pieceDialog != null) Destroy(pieceDialog);
            pieceDialog = null;
        }

        private void OpenSkillDetailFallback(int skillId)
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
            bool owned = progression.Player.Skills.Skills.Any(value =>
                value != null && value.ClassId == skillId);
            int mergePieces = progression.SkillMergePiece(skillId);
            bool mergeable = !owned && mergePieces > 0;
            Button install = CreatePanelButton(content, owned ? "\u5b89\u88c5" : mergeable ? "\u5408\u6210"
                : "\u672a\u62e5\u6709", new Vector2(-160f, 40f),
                new Color(0.86f, 0.55f, 0.3f, 0.95f));
            bool confirmingMerge = false;
            install.onClick.AddListener(() =>
            {
                if (mergeable)
                {
                    long ownedPieces = progression.Player.Inventory
                        .Where(value => value.ItemId == definition.PieceItemId).Sum(value => value.Count);
                    if (ownedPieces < mergePieces)
                    {
                        SetStatus("\u788e\u7247\u4e0d\u8db3\uff1a" + ownedPieces + "/" + mergePieces);
                        return;
                    }
                    if (!confirmingMerge)
                    {
                        confirmingMerge = true;
                        Text label = install.GetComponentInChildren<Text>();
                        if (label != null) label.text = "\u786e\u8ba4\u5408\u6210";
                        SetStatus("\u518d\u6b21\u70b9\u51fb\u5408\u6210\u4ee5\u786e\u8ba4\uff08"
                            + mergePieces + " \u7247\uff09");
                        return;
                    }
                    ProgressionResult merged = progression.MergeSkill(skillId);
                    SetStatus(merged.Message ?? merged.Status.ToString());
                    if (!merged.IsApplied) return;
                    CloseSkillDetail();
                    Refresh();
                    Changed?.Invoke();
                    return;
                }
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
            bool favorite = OfflineSkillProgression.IsFavorite(progression.Player.Skills, skillId);
            Button star = CreatePanelButton(content, favorite ? "\u53d6\u6d88\u6536\u85cf" : "\u6536\u85cf",
                new Vector2(320f, 40f), new Color(1f, 0.8f, 0.3f, 0.95f));
            star.onClick.AddListener(() =>
            {
                ToggleFavorite(skillId);
                CloseSkillDetail();
            });
            Button close = CreatePanelButton(content, "\u5173\u95ed", new Vector2(480f, 40f),
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
            int rank = OfflineSkillProgression.OwnedRank(progression.Player.Skills, skillId);
            if (definition == null || rank <= 0) return;
            ClosePromote();
            promoteSkillId = skillId;
            promoteDialog = BuildOfficialDialog("skill_promote", out var dialogNodes, out var dialogButtons);
            int itemType = definition.ItemType;
            RectTransform rankContent = DialogNode(dialogNodes, "RankUpContent");
            RectTransform levelContent = DialogNode(dialogNodes, "LevelUpContent");
            if (rankContent != null) rankContent.gameObject.SetActive(true);
            if (levelContent != null) levelContent.gameObject.SetActive(false);
            Button close = DialogButton(dialogButtons, "CloseBtnDark");
            if (close != null) close.onClick.AddListener(ClosePromote);
            Button rankTab = DialogButton(dialogButtons, "SkillPromotePanelRankUpTabGroupItem_11");
            if (rankTab != null) rankTab.onClick.AddListener(() =>
            {
                if (rankContent != null) rankContent.gameObject.SetActive(true);
                if (levelContent != null) levelContent.gameObject.SetActive(false);
                PlayOfficialLayoutAnimations("skill_promote", skillUiTemplates["skill_promote"]
                    as Dictionary<string, object>, dialogNodes, "SkillPromotePanel_Switch");
            });
            Button levelTab = DialogButton(dialogButtons, "SkillPromotePanelLevelUpTabGroupItem_11");
            if (levelTab != null) levelTab.onClick.AddListener(() =>
            {
                if (rankContent != null) rankContent.gameObject.SetActive(false);
                if (levelContent != null) levelContent.gameObject.SetActive(true);
                PlayOfficialLayoutAnimations("skill_promote", skillUiTemplates["skill_promote"]
                    as Dictionary<string, object>, dialogNodes, "SkillPromotePanel_Switch");
            });

            OfflineSkillPlan planState = progression.Player.Skills.Plans[progression.Player.Skills.SelectedPlan];
            int[] skillSlots = itemType == ActiveSkillItemType ? planState.ActiveSkills : planState.PassiveSkills;
            int equippedSlot = Array.IndexOf(skillSlots, skillId);
            int currentLevel = equippedSlot < 0 ? 0 : progression.SkillProgression.SlotLevel(
                progression.Player.Skills, itemType, equippedSlot, progression.SkillContext().CurrentSeasonId);
            SetTextValue(DialogNode(dialogNodes, "NameText")?.GetComponent<Text>(), SkillName(skillId));
            SetTextValue(DialogNode(dialogNodes, "CurrentLevelText")?.GetComponent<Text>(), currentLevel.ToString(CultureInfo.InvariantCulture));
            SetTextValue(DialogNode(dialogNodes, "NextLevelText")?.GetComponent<Text>(), (currentLevel + 1).ToString(CultureInfo.InvariantCulture));
            SetTextValue(DialogNode(dialogNodes, "Count")?.GetComponent<Text>(), rank.ToString(CultureInfo.InvariantCulture));
            int skillQuality = Quality(skillId);
            ApplyQualityGradient(FindTextNodeEnding(dialogNodes, "/From/upgradeText"), skillQuality, 0f);
            ApplyQualityGradient(FindTextNodeEnding(dialogNodes, "/RankUpDetailContent/UpgradeRank/upgradeText"),
                skillQuality, 45f);
            Transform skillIcon = DialogNode(dialogNodes, "ItemIconSkill");
            if (skillIcon != null) BuildSkillIcon(skillId, skillIcon, "OfficialSkillIcon");

            OfflineSkillRankCost rankCost = definition.RankCosts?.FirstOrDefault(value => value.CurrentRank == rank);
            RectTransform rankButtonNode = dialogNodes.Values.FirstOrDefault(value =>
                value.name == "UseItemBtn" && value.parent != null
                && value.parent.parent != null && value.parent.parent.name == "RankUpContent");
            Button rankUpgrade = rankButtonNode == null ? null : rankButtonNode.GetComponent<Button>();
            SetTextValue(rankButtonNode == null ? null : FindDeep(rankButtonNode, "BtnTipTxt")?.GetComponent<Text>(),
                rankCost == null ? "已达最高等阶" : string.Join("、", rankCost.Items.Select(item =>
                    ItemName(item.ItemId) + " ×" + item.Count.ToString("N0", CultureInfo.InvariantCulture))));
            if (rankUpgrade != null)
            {
                rankUpgrade.interactable = rankCost != null && HasAffordableSkillCost(rankCost.Items);
                rankUpgrade.onClick.AddListener(DoPromote);
            }

            foreach (KeyValuePair<string, Button> pair in dialogButtons.Where(value =>
                value.Key.EndsWith("/Slot", StringComparison.Ordinal)
                && value.Key.Contains("/SkillContentPanelSkillGroupItem", StringComparison.Ordinal)))
            {
                string rowName = pair.Key.Substring(pair.Key.LastIndexOf("SkillContentPanelSkillGroupItem",
                    StringComparison.Ordinal));
                int slot = rowName.Contains("(3)", StringComparison.Ordinal) ? 3
                    : rowName.Contains("(2)", StringComparison.Ordinal) ? 2
                    : rowName.Contains("(1)", StringComparison.Ordinal) ? 1 : 0;
                int level = progression.SkillProgression.SlotLevel(progression.Player.Skills,
                    itemType, slot, progression.SkillContext().CurrentSeasonId);
                RectTransform row = pair.Value.transform.parent as RectTransform;
                SetTextValue(row == null ? null : FindDeep(row, "LevelText")?.GetComponent<Text>(),
                    "Lv" + level.ToString(CultureInfo.InvariantCulture));
                int capturedSlot = slot;
                pair.Value.onClick.AddListener(() => UpgradePromoteSlot(itemType, capturedSlot));
            }

            RectTransform levelButtonNode = dialogNodes.Values.FirstOrDefault(value =>
                value.name == "UseItemBtn" && value.parent != null
                && value.parent.parent != null && value.parent.parent.name == "LevelUpContent");
            Button levelUpgrade = levelButtonNode == null ? null : levelButtonNode.GetComponent<Button>();
            if (levelUpgrade != null)
            {
                levelUpgrade.interactable = equippedSlot >= 0;
                levelUpgrade.onClick.AddListener(() =>
                {
                    if (equippedSlot >= 0) UpgradePromoteSlot(itemType, equippedSlot);
                });
            }
        }

        private void UpgradePromoteSlot(int itemType, int slot)
        {
            ProgressionResult result = progression.UpgradeSkillSlot(itemType, slot);
            SetStatus(result.Message ?? result.Status.ToString());
            if (!result.IsApplied) return;
            ClosePromote();
            Refresh();
            Changed?.Invoke();
            OpenPromote(promoteSkillId);
        }

        private void OpenPromoteFallback(int skillId)
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
            int skillId = promoteSkillId;
            int oldRank = OfflineSkillProgression.OwnedRank(progression.Player.Skills, skillId);
            ProgressionResult result = progression.PromoteSkill(promoteSkillId);
            SetStatus(result.Message ?? result.Status.ToString());
            if (!result.IsApplied) return;
            ClosePromote();
            Refresh();
            Changed?.Invoke();
            OpenRankPromotionResult(skillId, oldRank,
                OfflineSkillProgression.OwnedRank(progression.Player.Skills, skillId));
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
