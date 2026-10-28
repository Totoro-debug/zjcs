using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TMPro;
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
    public sealed partial class CharacterSkillPanel : MonoBehaviour
    {
        private sealed class SkillAnimationTrack
        {
            public Transform Target;
            public string Property;
            public AnimationCurve Curve;
            public float InitialValue;
        }

        private const int ActiveSkillItemType = 100;
        private const int PassiveSkillItemType = 101;
        private const int PieceItemType = 111;

        private OfficialCharacterAssetCatalog assets;
        private OfflineCharacterProgressionService progression;
        private CharacterPanelNodeFactory factory;
        private OfficialSkillAssetCatalog skillAssets;
        private readonly Dictionary<Button, Coroutine> tabAnimations = new Dictionary<Button, Coroutine>();
        private Dictionary<string, object> screen;
        private Dictionary<string, object> listItemTemplate;
        private Dictionary<string, object> itemTemplate;
        private Dictionary<string, object> emptyItemTemplate;
        private Dictionary<string, object> promoteTemplate;
        private Dictionary<string, object> infoTemplate;
        private Dictionary<string, object> skillUiTemplates;
        private Dictionary<string, object> skillListRowTemplate;
        private Dictionary<string, object> skillListSeparatorTemplate;
        private GameObject panel;
        private RectTransform skillContentRoot;
        private RectTransform achievementContentRoot;
        private Button skillPageTab;
        private Button achievementPageTab;
        private OfficialSkillGiftPanel giftPanel;
        private readonly Dictionary<string, RectTransform> nodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
        private readonly Dictionary<string, Button> buttons = new Dictionary<string, Button>(StringComparer.Ordinal);
        private Text planName;
        private InputField planInput;
        private Scrollbar planSelectedBar;
        private RectTransform activeGroup;
        private RectTransform passiveGroup;
        private RectTransform skillGrid;
        private RectTransform skillPieceGrid;
        private RectTransform skillScrollList;
        private RectTransform skillPieceScrollList;
        private GameObject emptyTip;
        private Toggle showAll;
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
        private readonly List<GameObject> recommendationItems = new List<GameObject>();
        private readonly Dictionary<int, GameObject> recommendationRankTabs = new Dictionary<int, GameObject>();
        private int recommendationRank;
        private readonly List<GameObject> listRows = new List<GameObject>();
        private int listType = ActiveSkillItemType;
        private int selectedType;
        private int selectedSlot = -1;
        private int plan;
        private int pendingSkillId;
        private int pendingType;
        private GameObject installMask;

        public event Action Changed;
        public event Action<bool> PlayerHeaderVisibilityChanged;
        public string LastMessage { get; private set; } = string.Empty;

        public bool IsOpen => panel != null;
        private string profileSlotId, profileSavesRoot;

        public void Initialize(OfficialCharacterAssetCatalog assetCatalog,
            OfflineCharacterProgressionService characterProgression, string selectedSlotId = null, string savesRoot = null)
        {
            profileSlotId = selectedSlotId;
            profileSavesRoot = savesRoot;
            assets = assetCatalog ?? throw new ArgumentNullException(nameof(assetCatalog));
            progression = characterProgression ?? throw new ArgumentNullException(nameof(characterProgression));
            CharacterPanelNodeFactory.SetUiFont(assets.UiFont);
            factory = new CharacterPanelNodeFactory(assets);
            skillAssets = OfficialSkillAssetCatalog.LoadDefault();
            Dictionary<string, object> panels = CharacterModelPackage.AsObject(
                OfficialEquipmentJson.Parse(assets.CharacterPanels.text), "panels");
            Dictionary<string, object> screens = CharacterModelPackage.AsObject(panels["screens"], "screens");
            listItemTemplate = CharacterModelPackage.AsObject(screens["skill_list_item"], "skill_list_item");
            itemTemplate = CharacterModelPackage.AsObject(screens["item"], "item");
            promoteTemplate = CharacterModelPackage.AsObject(screens["skill_promote"], "skill_promote");
            infoTemplate = CharacterModelPackage.AsObject(screens["character_props"], "character_props");
            skillUiTemplates = CharacterModelPackage.AsObject(
                OfficialEquipmentJson.Parse(assets.SkillUiLayouts.text), "skill UI layouts");
            itemTemplate = CharacterModelPackage.AsObject(skillUiTemplates["item_skill"], "official skill icon");
            emptyItemTemplate = CharacterModelPackage.AsObject(skillUiTemplates["item_empty"], "official empty skill icon");
            screen = CharacterModelPackage.AsObject(
                skillUiTemplates["character_skills"], "official character skill panel");
            skillListRowTemplate = CharacterModelPackage.AsObject(
                skillUiTemplates["skill_list_row"], "official skill list row");
            skillListSeparatorTemplate = CharacterModelPackage.AsObject(
                skillUiTemplates["skill_list_separator"], "official skill list separator");
        }

        public void Open(Transform parent)
        {
            if (panel != null) return;
            nodes.Clear();
            buttons.Clear();
            panel = new GameObject("CharacterSkillsLayer", typeof(RectTransform));
            panel.transform.SetParent(parent, false);
            RectTransform rect = (RectTransform)panel.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            factory.Build(CharacterPanelNodeFactory.ReadRoot(screen), rect, nodes, buttons);
            CharacterSafeArea.Restore(nodes["/CharacterPanelSkillPanel"], "/CharacterPanelSkillPanel", skillUiTemplates);
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
            CloseAuxiliary();
            if (messageRoutine != null) StopCoroutine(messageRoutine);
            messageRoutine = null;
            if (systemMessage != null) Destroy(systemMessage);
            systemMessage = null;
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
            skillContentRoot = null;
            achievementContentRoot = null;
            skillPageTab = null;
            achievementPageTab = null;
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
            if (skillVideoDialog != null) { CloseSkillVideo(); return true; }
            if (detailDialog != null && detailDialog.name == "skill_plain_detail") { CloseSkillDetail(); return true; }
            if (auxiliaryDialog != null) { var back = auxiliaryReturn; CloseAuxiliary(); back?.Invoke(); return true; }
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
            foreach (Text text in panel.GetComponentsInChildren<Text>(true)) text.raycastTarget = false;
            foreach (TMP_Text text in panel.GetComponentsInChildren<TMP_Text>(true)) text.raycastTarget = false;
            RectTransform nameRect = FindDeep("SkillPlanName");
            planName = nameRect == null ? null : nameRect.GetComponentInChildren<Text>(true);
            if (nameRect != null)
            {
                Button nameButton = FindDeep(nameRect, "CustomMask")?.GetComponent<Button>();
                Graphic graphic = nameRect.GetComponent<Graphic>();
                if (graphic == null)
                {
                    Image image = nameRect.gameObject.AddComponent<Image>();
                    image.color = new Color(1f, 1f, 1f, 0f);
                    graphic = image;
                }
                graphic.canvasRenderer.cullTransparentMesh = false;
                if (nameButton != null) nameButton.onClick.AddListener(BeginRename);
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
            BindPlanSelectedBar();
            BindButtonName("FastLevelUpBtn", UpgradeAllSlots);
            BindButtonName("FastPromotionBtn", PromoteAllSkills);
            BindButtonName("RecommendBtn", OpenRecommendations);
            BindButtonName("StageDamageTestBtn", OpenOriginalDamageTest);
            BindButtonName("NextPlanBtn", () => SwitchPlan(1));
            activeGroup = FindDeep("ActiveSkill");
            passiveGroup = FindDeep("PassiveSkill");
            skillScrollList = FindDeep(panel.transform, "SkillScrollList") as RectTransform;
            skillPieceScrollList = FindDeep(panel.transform, "SkillPieceScrollList") as RectTransform;
            skillGrid = FindDeep(skillScrollList, "skillGrid") as RectTransform;
            skillPieceGrid = FindDeep(skillPieceScrollList, "skillGrid") as RectTransform;
            BindSkillListScroll();
            ConfigureSkillPieceGrid();
            emptyTip = FindDeep("EmptyTip")?.gameObject;
            RectTransform toggle = FindDeep("ShowAll");
            if (toggle != null)
            {
                showAll = toggle.GetComponent<Toggle>() ?? toggle.gameObject.AddComponent<Toggle>();
                showAll.isOn = false;
                Transform on = toggle.Find("Toggle/On");
                Transform off = toggle.Find("Toggle/Off");
                if (on != null) on.gameObject.SetActive(false);
                if (off != null) off.gameObject.SetActive(true);
                showAll.onValueChanged.AddListener(selected =>
                {
                    if (on != null) on.gameObject.SetActive(selected);
                    if (off != null) off.gameObject.SetActive(!selected);
                    RefreshList();
                });
            }
            BindTab("ActiveSkillTab", ActiveSkillItemType);
            BindTab("PassiveSkillTab", PassiveSkillItemType);
            BindTab("SkillPieceTab", PieceItemType);
            skillContentRoot = FindDeep(panel.transform, "SkillContentPanel") as RectTransform;
            achievementContentRoot = FindDeep(panel.transform, "AchievementPanelWithTypes") as RectTransform;
            skillPageTab = buttons.Values.FirstOrDefault(value =>
                value.name == "CharacterPanelSkillDetailPanelSkillTabGroupItem_11");
            achievementPageTab = buttons.Values.FirstOrDefault(value =>
                value.name == "AchievementTabGroupItem_11");
            if (skillPageTab != null) skillPageTab.onClick.AddListener(ShowSkillContent);
            if (achievementPageTab != null) achievementPageTab.onClick.AddListener(ShowAchievementContent);
            foreach (Button button in buttons.Values)
                if (button.targetGraphic != null)
                    button.targetGraphic.canvasRenderer.cullTransparentMesh = false;
            giftPanel = new OfficialSkillGiftPanel(nodes, skillUiTemplates, progression, assets, skillAssets,
                PlayTabTransition, () => { Changed?.Invoke(); }, OpenOriginalGiftItemDetail);
            SetBottomPageState(showSkills: true);
            RefreshCategoryTabs();
            if (panel == null) return;
            BindButtonName("CloseBtn_dark", Close);
            BuildSlotHandlers(activeGroup, ActiveSkillItemType);
            BuildSlotHandlers(passiveGroup, PassiveSkillItemType);
        }

        private void ShowSkillContent()
        {
            SetBottomPageState(showSkills: true);
            Refresh();
        }

        private void ShowAchievementContent()
        {
            SetBottomPageState(showSkills: false);
        }

        private void SetBottomPageState(bool showSkills)
        {
            PlayerHeaderVisibilityChanged?.Invoke(showSkills);
            if (skillContentRoot != null) skillContentRoot.gameObject.SetActive(showSkills);
            if (achievementContentRoot != null) achievementContentRoot.gameObject.SetActive(!showSkills);
            if (!showSkills) giftPanel?.Show();
            SetBottomTabSelected(skillPageTab, showSkills);
            SetBottomTabSelected(achievementPageTab, !showSkills);
        }

        private void SetBottomTabSelected(Button tab, bool selected)
        {
            PlayTabTransition(tab, selected ? "TabBottomBarCell_On" : "TabBottomBarCell_Off");
        }

        private void PlayTabTransition(Button tab, string animationName)
        {
            if (tab == null) return;
            string path = nodes.FirstOrDefault(pair => pair.Value == tab.transform).Key;
            if (string.IsNullOrEmpty(path)) return;
            Dictionary<string, object> root = CharacterModelPackage.AsObject(screen["root"], "skill page root");
            Dictionary<string, object> source = FindLayoutNode(root, path);
            if (source == null || !source.TryGetValue("animations", out object raw)
                || !(raw is List<object> animations)) return;
            Dictionary<string, object> clip = animations.OfType<Dictionary<string, object>>()
                .FirstOrDefault(value => CharacterPanelNodeFactory.String(value, "name") == animationName);
            if (clip == null || !TryBuildAnimationTracks(clip, nodes, out var tracks, out float duration)) return;
            if (tabAnimations.TryGetValue(tab, out Coroutine previous) && previous != null) StopCoroutine(previous);
            tabAnimations[tab] = StartCoroutine(PlaySkillAnimation(tracks, duration, false));
        }

        private static Dictionary<string, object> FindLayoutNode(Dictionary<string, object> node, string path)
        {
            if (CharacterPanelNodeFactory.String(node, "path") == path) return node;
            if (node.TryGetValue("children", out object raw) && raw is List<object> children)
                foreach (Dictionary<string, object> child in children.OfType<Dictionary<string, object>>())
                {
                    Dictionary<string, object> found = FindLayoutNode(child, path);
                    if (found != null) return found;
                }
            return null;
        }

        private void RefreshCategoryTabs()
        {
            foreach (var pair in new[] { ("ActiveSkillTab", ActiveSkillItemType),
                         ("PassiveSkillTab", PassiveSkillItemType), ("SkillPieceTab", PieceItemType) })
            {
                Button tab = buttons.Values.FirstOrDefault(value => value.name.Contains(pair.Item1));
                PlayTabTransition(tab, listType == pair.Item2 ? "TabGroupItem_On_21" : "TabGroupItem_Off_21");
            }
        }

        private void BindSkillListScroll()
        {
            if (skillGrid == null) return;
            BindSkillScroll(skillScrollList, skillGrid);
            BindSkillScroll(skillPieceScrollList, skillPieceGrid);
        }

        private void ConfigureSkillPieceGrid()
        {
            if (skillPieceGrid == null || skillPieceScrollList == null) return;
            GridLayoutGroup grid = skillPieceGrid.GetComponent<GridLayoutGroup>();
            if (grid == null) grid = skillPieceGrid.gameObject.AddComponent<GridLayoutGroup>();
            Dictionary<string, object> itemRoot = CharacterModelPackage.AsObject(
                listItemTemplate["root"], "official skill piece list item root");
            List<object> itemSize = CharacterModelPackage.AsArray(
                itemRoot["size_delta"], "official skill piece list item size");
            grid.cellSize = new Vector2(
                Convert.ToSingle(itemSize[0], CultureInfo.InvariantCulture),
                Convert.ToSingle(itemSize[1], CultureInfo.InvariantCulture));
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.spacing = Vector2.zero;
            RectTransform viewport = FindDeep(skillPieceScrollList, "Viewport") as RectTransform;
            float availableWidth = viewport == null ? skillPieceGrid.rect.width : viewport.rect.width;
            grid.constraintCount = Mathf.Max(1, Mathf.FloorToInt(
                (availableWidth + grid.spacing.x) / (grid.cellSize.x + grid.spacing.x)));
            skillPieceGrid.anchorMin = new Vector2(0f, 1f);
            skillPieceGrid.anchorMax = new Vector2(1f, 1f);
            skillPieceGrid.pivot = new Vector2(0.5f, 1f);
            skillPieceGrid.anchoredPosition = Vector2.zero;
            ContentSizeFitter fitter = skillPieceGrid.GetComponent<ContentSizeFitter>();
            if (fitter == null) fitter = skillPieceGrid.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private static void BindSkillScroll(RectTransform list, RectTransform content)
        {
            if (list == null || content == null) return;
            RectTransform viewport = FindDeep(list, "Viewport") as RectTransform;
            if (viewport == null) return;
            foreach (Graphic graphic in viewport.GetComponents<Graphic>()) graphic.raycastTarget = false;
            ScrollRect scroll = list.GetComponent<ScrollRect>() ?? list.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.inertia = true;
            scroll.decelerationRate = 0.135f;
            scroll.scrollSensitivity = 32f;
        }

        private void BindButtonName(string name, Action action)
        {
            Button button = buttons.Values.FirstOrDefault(value => value.name == name);
            if (button != null) button.onClick.AddListener(() => action());
        }

        private void BindTab(string namePart, int itemType)
        {
            Button button = buttons.Values.FirstOrDefault(value =>
                value != null && value.name.IndexOf(namePart, StringComparison.Ordinal) >= 0);
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
                if (button.targetGraphic != null) button.targetGraphic.canvasRenderer.cullTransparentMesh = false;
                Button iconButton = FindDeep(slots[index], "Slot")?.GetComponent<Button>();
                if (iconButton != null) iconButton.onClick.AddListener(() => SelectSlot(itemType, slot));
                Transform levelEffect = FindDeep(slots[index], "AniLevelUp");
                if (levelEffect != null) levelEffect.gameObject.SetActive(false);
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
            LastMessage = string.IsNullOrEmpty(result.Message) ? result.Status.ToString() : result.Message;
            SetStatus(result.Message ?? result.Status.ToString());
            if (!result.IsApplied) return;
            Refresh();
            Changed?.Invoke();
        }

        public void ReloadProfile()
        {
            if (!IsOpen) return;
            plan = progression.Player.Skills.SelectedPlan;
            Refresh();
        }

        private void Refresh()
        {
            if (panel == null) return;
            OfflineSkillProfile profile = progression.Player.Skills;
            plan = profile.SelectedPlan;
            if (planName != null) planName.text = profile.Plans[plan].Name;
            if (planInput != null) planInput.SetTextWithoutNotify(profile.Plans[plan].Name);
            if (planSelectedBar != null) planSelectedBar.SetValueWithoutNotify((float)plan / (profile.Plans.Length - 1));
            RefreshSlots(activeGroup, ActiveSkillItemType);
            RefreshSlots(passiveGroup, PassiveSkillItemType);
            RefreshList();
            RefreshFastAction();
            var damageTest = FindDeep("StageDamageTestBtn");
            var condition = CharacterModelPackage.AsObject(progression.Catalog.Rows("function_limit")
                .First(value => Convert.ToInt32(value["Type"], CultureInfo.InvariantCulture) == 148)["Condition"], "damage test limit");
            if (damageTest != null) damageTest.gameObject.SetActive(progression.Player.Level >=
                Convert.ToInt32(condition["Level"], CultureInfo.InvariantCulture));
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
            RectTransform promotion = FindDeep("FastPromotionBtn");
            if (promotion != null) promotion.gameObject.SetActive(fastPromotionUnlocked && promotable >= 5);
            RectTransform levelUp = FindDeep("FastLevelUpBtn");
            if (levelUp != null) levelUp.gameObject.SetActive(fastPromotionUnlocked && promotable < 5
                && progression.PreviewUpgradeAllSkillSlotChanges().Any());
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
                BuildSkillIcon(skillId, mount, "SkillIcon");
                Text level = FindDeep(child, "LevelText")?.GetComponent<Text>();
                if (level != null)
                {
                    int slotLevel = progression.SkillProgression == null ? 0
                        : progression.SkillProgression.SlotLevel(progression.Player.Skills, itemType, index);
                    level.text = slotLevel.ToString(CultureInfo.InvariantCulture) + "级";
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
            ShowSkillList(listType == PieceItemType);
            foreach (GameObject row in listRows)
                if (row != null)
                {
                    row.SetActive(false);
                    Destroy(row);
                }
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
                int mergeCount = progression.SkillMergePiece(definition.ClassId);
                long pieces = progression.Player.Inventory.Where(value => value != null && value.ItemId == definition.PieceItemId).Sum(value => value.Count);
                if (!has)
                {
                    if (mergeCount > 0 && pieces >= mergeCount) mergeable.Add(definition.ClassId);
                    continue;
                }
                bool available = IsAvailableForCurrentProfession(definition.ClassId, progression.SkillContext());
                if (!(showAll != null && showAll.isOn) && !available && !CanPromoteSkill(definition.ClassId)) continue;
                if (Array.IndexOf(equipped, definition.ClassId) >= 0) continue;
                if (OfflineSkillProgression.IsFavorite(profile, definition.ClassId)) collected.Add(definition.ClassId);
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
                int profession = OriginalSkillProfessionScore(right).CompareTo(OriginalSkillProfessionScore(left));
                return profession != 0 ? profession : left.CompareTo(right);
            };
            mergeable.Sort((left, right) => {
                int quality = Quality(right).CompareTo(Quality(left));
                if (quality != 0) return quality;
                long Count(int skill) => progression.Player.Inventory.Where(value => value != null && value.ItemId == SkillPieceId(skill)).Sum(value => value.Count);
                return Count(right).CompareTo(Count(left));
            });
            collected.Sort(bySkillPanel);
            uncollected.Sort(bySkillPanel);
            int rowCapacity = SkillListRowCapacity(out float rowSpacing);
            AppendSkillRows(mergeable, rowCapacity, owned, rowSpacing);
            AppendSkillRows(collected, rowCapacity, owned, rowSpacing);
            if (collected.Count > 0 && uncollected.Count > 0)
            {
                var separatorNodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
                var separatorButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
                RectTransform separator = factory.Build(
                    CharacterPanelNodeFactory.ReadRoot(skillListSeparatorTemplate), skillGrid,
                    separatorNodes, separatorButtons);
                listRows.Add(separator.gameObject);
            }
            AppendSkillRows(uncollected, rowCapacity, owned, rowSpacing);
            if (emptyTip != null) emptyTip.SetActive(listRows.Count == 0);
            RefreshSkillListLayout(skillScrollList, skillGrid);
        }

        private void AppendSkillRows(List<int> skillIds, int rowCapacity,
            IReadOnlyDictionary<int, int> owned, float spacing)
        {
            for (int offset = 0; offset < skillIds.Count; offset += rowCapacity)
            {
                int count = Mathf.Min(rowCapacity, skillIds.Count - offset);
                GameObject row = BuildSkillListRow(skillIds, offset, count, owned, spacing);
                if (row != null) listRows.Add(row);
            }
        }

        private int SkillListRowCapacity(out float spacing)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform viewport = FindDeep(skillScrollList, "Viewport") as RectTransform;
            float width = viewport == null ? skillGrid.rect.width : viewport.rect.width;
            const float itemSize = 155f;
            const float itemSpacing = 37.42f;
            const float minItemSpacing = 20f;
            const float paddingLeft = 50f;
            const float paddingRight = 50f;
            const int maxCountPerRow = 10;
            if (width <= 1f)
            {
                spacing = itemSpacing;
                return 1;
            }

            float availableWidth = width - paddingLeft - paddingRight;
            int count = 1;
            for (int candidate = maxCountPerRow; candidate >= 1; candidate--)
            {
                float candidateSpacing = candidate <= 1 ? 0f
                    : (availableWidth - candidate * itemSize) / (candidate - 1);
                if (candidateSpacing + 0.01f < Mathf.Min(minItemSpacing, itemSpacing)) continue;
                count = candidate;
                break;
            }
            spacing = count <= 1 ? 0f : Mathf.Max(
                (availableWidth - count * itemSize) / (count - 1),
                Mathf.Min(minItemSpacing, itemSpacing));
            return count;
        }

        private GameObject BuildSkillListRow(IReadOnlyList<int> skillIds, int start, int count,
            IReadOnlyDictionary<int, int> owned, float spacing)
        {
            var rowNodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            var rowButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
            RectTransform row = factory.Build(CharacterPanelNodeFactory.ReadRoot(skillListRowTemplate),
                skillGrid, rowNodes, rowButtons);
            HorizontalLayoutGroup rowLayout = row.GetComponent<HorizontalLayoutGroup>();
            if (rowLayout != null)
            {
                rowLayout.spacing = spacing;
                rowLayout.padding.left = 50;
                rowLayout.padding.right = 50;
            }
            for (int index = 0; index < count; index++)
            {
                int skillId = skillIds[start + index];
                var itemNodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
                var itemButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
                RectTransform item = factory.Build(CharacterPanelNodeFactory.ReadRoot(listItemTemplate),
                    row, itemNodes, itemButtons);
                item.name = "SkillListItem_" + skillId.ToString(CultureInfo.InvariantCulture);
                item.sizeDelta = new Vector2(155f, 155f);
                Transform mount = FindDeep(item, "Anchor") ?? item;
                GameObject icon = BuildSkillIcon(owned.ContainsKey(skillId) ? skillId : SkillPieceId(skillId), mount, "SkillIcon");
                if (owned.ContainsKey(skillId)) {
                    // CharacterPanelSkillListPanelItem.RefreshSkillIcon uses the
                    // minimum of all eight slot levels, independent of this row.
                    int season = progression.SkillContext().CurrentSeasonId;
                    int level = new[] { ActiveSkillItemType, PassiveSkillItemType }
                        .SelectMany(type => Enumerable.Range(0, 4).Select(slot =>
                            progression.SkillProgression.SlotLevel(progression.Player.Skills, type, slot, season))).Min();
                    var label = FindDeep(icon.transform, "BottomText");
                    label.gameObject.SetActive(level != 0);
                    SetTextValue(label.GetComponent<Text>(), string.Format(CultureInfo.InvariantCulture,
                        OriginalDetailText("ui_common_level_style_3"), level));
                }
                Graphic target = item.GetComponent<Graphic>();
                if (target == null)
                {
                    Image hitTarget = item.gameObject.AddComponent<Image>();
                    hitTarget.color = new Color(1f, 1f, 1f, 0.01f);
                    hitTarget.raycastTarget = true;
                    hitTarget.canvasRenderer.cullTransparentMesh = false;
                    target = hitTarget;
                }
                else target.raycastTarget = true;
                Button button = item.GetComponent<Button>() ?? item.gameObject.AddComponent<Button>();
                button.targetGraphic = target;
                if (owned.ContainsKey(skillId)) button.onClick.AddListener(() => OpenSkillDetail(skillId));
                else button.onClick.AddListener(() => OpenMergeConfirm(skillId));
            }
            return row.gameObject;
        }

        private static void RefreshSkillListLayout(RectTransform list, RectTransform content)
        {
            if (list == null || content == null) return;
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
            Canvas.ForceUpdateCanvases();
            ScrollRect scroll = list.GetComponent<ScrollRect>();
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;
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
            RectTransform grid = skillPieceGrid;
            if (grid == null) return;
            Canvas.ForceUpdateCanvases();
            GridLayoutGroup pieceLayout = grid.GetComponent<GridLayoutGroup>();
            RectTransform viewport = FindDeep(skillPieceScrollList, "Viewport") as RectTransform;
            if (pieceLayout != null && viewport != null && pieceLayout.cellSize.x > 0f)
                pieceLayout.constraintCount = Mathf.Max(1, Mathf.FloorToInt(
                    (viewport.rect.width + pieceLayout.spacing.x)
                    / (pieceLayout.cellSize.x + pieceLayout.spacing.x)));
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
                RectTransform row = factory.Build(CharacterPanelNodeFactory.ReadRoot(
                    CharacterModelPackage.AsObject(skillUiTemplates["skill_piece_list_item"], "original piece list item")), grid,
                    rowNodes, rowButtons);
                Transform mount = FindDeep(row, "Anchor") ?? row;
                GameObject pieceIcon = BuildSkillIcon(stack.ItemId, mount, "SkillIcon");
                ShowOriginalPieceProgress(pieceIcon.transform, stack.ItemId, stack.Count);
                OfflineSkillDefinition pieceDefinition = progression.SkillRules?.Skills
                    .FirstOrDefault(value => value.PieceItemId == stack.ItemId);
                if (pieceDefinition != null)
                {
                    Button rowButton = row.gameObject.GetComponent<Button>();
                    if (rowButton == null) rowButton = row.gameObject.AddComponent<Button>();
                    Image target = row.GetComponent<Image>();
                    if (target == null || !target.enabled)
                    {
                        target = row.gameObject.AddComponent<Image>();
                        target.color = new Color(1f, 1f, 1f, 0.01f);
                    }
                    rowButton.targetGraphic = target;
                    if (target.sprite == null) target.color = new Color(1f, 1f, 1f, 0.01f);
                    target.raycastTarget = true;
                    CanvasRenderer targetRenderer = target.canvasRenderer;
                    if (targetRenderer != null) targetRenderer.cullTransparentMesh = false;
                    int skillId = pieceDefinition.ClassId;
                    rowButton.onClick.AddListener(() => OpenPieceDetail(skillId));
                }
                LayoutElement element = row.GetComponent<LayoutElement>() ?? row.gameObject.AddComponent<LayoutElement>();
                element.preferredHeight = 150f;
                element.minHeight = 150f;
                listRows.Add(row.gameObject);
            }
            if (emptyTip != null) emptyTip.SetActive(listRows.Count == 0);
            RefreshSkillListLayout(skillPieceScrollList, skillPieceGrid);
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

        private GameObject BuildSkillIcon(int itemId, Transform parent, string name, int? snapshotRank = null, int? snapshotSkin = null)
        {
            var iconNodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            var iconButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
            RectTransform root = factory.Build(CharacterPanelNodeFactory.ReadRoot(itemId == 0 ? emptyItemTemplate
                : progression.Catalog.ItemType(itemId) == PieceItemType
                    ? CharacterModelPackage.AsObject(skillUiTemplates["item_skill_piece"], "official skill piece icon") : itemTemplate), parent,
                iconNodes, iconButtons);
            root.gameObject.name = name;
            Dictionary<string, object> item = progression.Catalog.FindItem(itemId);
            int quality = snapshotRank.HasValue && snapshotRank.Value > 0
                ? progression.SkillRules.RankDisplays.First(value => value.Rank == snapshotRank.Value).Quality : Quality(itemId);
            foreach (RectTransform rect in iconNodes.Values)
            {
                if (rect.name == "QualityImg")
                {
                    Image image = rect.GetComponent<Image>();
                    string[] files = { "ImgQuality_White-031a03cab0.png", "ImgQuality_White-031a03cab0.png",
                        "ImgQuality_Blue-42b8cb9630.png", "ImgQuality_Purple-0b40a53021.png",
                        "ImgQuality_Orange-78887320b0.png", "ImgQuality_Gold-5f018c42e0.png",
                        "ImgQuality_Red-caa09e6943.png", "ImgQuality_Multicolour-93374ebc65.png" };
                    if (image != null && itemId != 0) image.sprite = assets.FindSprite("res://assets/official/sprites/"
                        + files[Mathf.Clamp(quality, 0, files.Length - 1)]);
                }
            }
            RectTransform icon = iconNodes.Values.FirstOrDefault(value => value.name == "icon");
            string uri = progression.Catalog.ItemIconUri(itemId);
            Sprite sprite = string.IsNullOrEmpty(uri) ? null : assets.FindSprite(uri);
            int equippedSkin = snapshotSkin ?? progression.EquippedSkillSkinId(itemId);
            if (equippedSkin > 0) sprite = assets.FindSkillSkinSprite(progression.Catalog, equippedSkin)
                ?? throw new InvalidOperationException("Original equipped skill skin icon is absent: " + equippedSkin);
            if (icon != null)
            {
                Image image = icon.GetComponent<Image>();
                if (image != null)
                {
                    image.sprite = sprite;
                    image.color = Color.white;
                    image.enabled = sprite != null;
                }
            }
            ConfigureSkillStars(root, itemId, quality, snapshotRank);
            // ItemIcon.Clear hides prefab editor placeholders before Init.
            // The piece prefab's count container has a trailing space.
            foreach (RectTransform node in iconNodes.Values)
                if (node.name.TrimEnd() == "BgCountText" || node.name == "BottomText"
                    || node.name == "Progress" || node.name == "DebugProfessionRankText") node.gameObject.SetActive(false);
            foreach (Button value in root.GetComponentsInChildren<Button>(true)) value.enabled = false;
            foreach (Graphic value in root.GetComponentsInChildren<Graphic>(true)) value.raycastTarget = false;
            return root.gameObject;
        }

        private void ShowOriginalPieceProgress(Transform icon, int pieceId, long count)
        {
            // ItemIconSkillPiece.ShowProgress: the next rank's piece requirement,
            // or zero at maximum rank; use the original ItemProgress subtree.
            var definition = progression.SkillRules.Skills.First(value => value.PieceItemId == pieceId);
            int rank = OfflineSkillProgression.OwnedRank(progression.Player.Skills, definition.ClassId);
            long required = rank == 0 ? progression.SkillMergePiece(definition.ClassId)
                : rank >= definition.MaximumRank ? 0
                : definition.RankCosts.FirstOrDefault(value => value.CurrentRank == rank)?.Items
                    .Where(value => value.ItemId == pieceId).Sum(value => value.Count) ?? 0;
            Transform progress = FindDeep(icon, "Progress");
            if (progress == null) throw new InvalidOperationException("Original skill piece progress is absent.");
            progress.gameObject.SetActive(true);
            Transform countRoot = progress.Find("BgCountText");
            countRoot.gameObject.SetActive(true);
            SetTextValue(countRoot.Find("LeftTxt").GetComponent<Text>(), OfficialPropFormat.Compact(count));
            SetTextValue(countRoot.Find("RightTxt").GetComponent<Text>(), OfficialPropFormat.Compact(required));
        }

        private void ConfigureSkillStars(RectTransform icon, int skillId, int quality, int? snapshotRank = null)
        {
            Transform stars = FindDeep(icon, "SkillStarInfo");
            if (stars == null) return;
            int rank = snapshotRank ?? OfflineSkillProgression.OwnedRank(progression.Player.Skills, skillId);
            OfflineSkillRankDisplay display = progression.SkillRules.RankDisplays.FirstOrDefault(value => value.Rank == rank);
            int addition = display?.RankAddition ?? 0;
            stars.gameObject.SetActive(addition > 0);
            if (addition == 0) return;
            Dictionary<string, object> settings = CharacterModelPackage.AsObject(skillUiTemplates["skill_star_settings"], "official star settings");
            int threshold = Convert.ToInt32(settings["threshold"], CultureInfo.InvariantCulture);
            Dictionary<string, object> sprites = CharacterModelPackage.AsObject(settings["quality_sprites"], "official star sprites");
            string uri = Convert.ToString(sprites[quality.ToString(CultureInfo.InvariantCulture)], CultureInfo.InvariantCulture);
            Sprite sprite = assets.FindSprite(uri) ?? skillAssets.FindSprite(uri);
            Transform starList = FindDeep(stars, "StarList");
            Transform starNumber = FindDeep(stars, "StarNum");
            bool showNumber = addition > threshold;
            if (starNumber != null)
            {
                starNumber.gameObject.SetActive(showNumber);
                Text text = starNumber.GetComponentInChildren<Text>(true);
                if (text != null) text.text = addition.ToString(CultureInfo.InvariantCulture);
                Image image = FindDeep(starNumber, "Star")?.GetComponent<Image>();
                if (image != null) image.sprite = sprite;
            }
            if (starList == null) return;
            starList.gameObject.SetActive(!showNumber);
            int index = 0;
            foreach (Transform star in starList)
            {
                star.gameObject.SetActive(index++ < addition);
                if (star.childCount > 0)
                {
                    Image image = star.GetChild(0).GetComponent<Image>();
                    if (image != null) image.sprite = sprite;
                }
            }
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
            batchReveal = StartCoroutine(RevealBatchPromotionRows(resultDialog, resultRows, resultNodes, close, skip));
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

        private IEnumerator RevealBatchPromotionRows(GameObject owner, List<CanvasGroup> rows,
            Dictionary<string, RectTransform> resultNodes,
            Button closeButton, Button skipButton)
        {
            var mask = owner.GetComponent<OfficialSkillPopupMask>();
            while (mask != null && !mask.IsReady)
            {
                if (owner == null || resultDialog != owner) yield break;
                yield return null;
            }
            var template = CharacterModelPackage.AsObject(skillUiTemplates["skill_batch_notify"], "batch result");
            var root = CharacterModelPackage.AsObject(template["root"], "batch result root");
            var sequence = FindOriginalComponent(root, "ListItemShowSequence");
            float interval = Convert.ToSingle(sequence["mShowInterval"], CultureInfo.InvariantCulture);
            float delay = OriginalAnimationEventTime(root, "ShowItems")
                + Convert.ToSingle(sequence["mFirstShowDelay"], CultureInfo.InvariantCulture);
            float remaining = delay - (mask == null ? 0f : Time.unscaledTime - mask.ShownAtUnscaled);
            if (remaining > 0) yield return new WaitForSecondsRealtime(remaining);
            foreach (CanvasGroup row in rows)
            {
                yield return new WaitForSecondsRealtime(interval);
                if (owner == null || resultDialog != owner) yield break;
                if (row != null) row.alpha = 1f;
            }
            ScrollSkillResultsToEnd(resultNodes);
            batchReveal = null;
            if (skipButton != null) skipButton.gameObject.SetActive(false);
            if (closeButton != null) closeButton.gameObject.SetActive(true);
        }

        private static Dictionary<string, object> FindOriginalComponent(Dictionary<string, object> node, string name)
        {
            if (node.TryGetValue("original_bindings", out object raw)
                && CharacterModelPackage.AsObject(raw, "original bindings").TryGetValue(name, out object value))
                return CharacterModelPackage.AsObject(value, name);
            foreach (object child in (List<object>)node["children"])
            {
                var found = FindOriginalComponent(CharacterModelPackage.AsObject(child, "original node"), name);
                if (found != null) return found;
            }
            return null;
        }

        private static float OriginalAnimationEventTime(Dictionary<string, object> node, string method)
        {
            if (node.TryGetValue("animations", out object raw))
                foreach (object animation in (List<object>)raw)
                {
                    var clip = CharacterModelPackage.AsObject(animation, "original animation");
                    if (!clip.TryGetValue("events", out object events)) continue;
                    foreach (object entry in (List<object>)events)
                    {
                        var item = CharacterModelPackage.AsObject(entry, "animation event");
                        if (CharacterPanelNodeFactory.String(item, "method") == method)
                            return CharacterPanelNodeFactory.Number(item, "time", 0f);
                    }
                }
            throw new InvalidOperationException("Original animation event is unavailable: " + method);
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
        }

        private void OpenRecommendations()
        {
            OfflineSkillPlayerContext context = progression.SkillContext();
            Dictionary<int, int> professionRanks = progression.Catalog.Rows("profession_base")
                .Where(value => value != null)
                .GroupBy(value => Convert.ToInt32(value["Profession"], CultureInfo.InvariantCulture))
                .ToDictionary(group => group.Key, group => Convert.ToInt32(group.First()["Rank"], CultureInfo.InvariantCulture));
            IReadOnlyList<OfficialSkillRecommendation> recommendations = progression.SkillRecommendations
                .Where(value => value != null && context.CurrentProfessionChain.Contains(value.Profession))
                .OrderBy(value => professionRanks.TryGetValue(value.Profession, out int rank) ? rank : int.MaxValue)
                .ThenBy(value => value.Id).ToArray();
            if (recommendations.Count == 0)
            {
                SetStatus("\u5f53\u524d\u804c\u4e1a\u6682\u65e0\u63a8\u8350\u65b9\u6848\u3002");
                return;
            }
            CloseRecommendations();
            recommendDialog = BuildOfficialDialog("skill_recommend", out var dialogNodes, out _);
            recommendDialog.name = "SkillRecommendDialog";
            RectTransform tabParent = DialogNode(dialogNodes, "TabGroup");
            RectTransform itemParent = DialogNode(dialogNodes, "Slot");
            if (tabParent == null || itemParent == null)
                throw new InvalidOperationException("Official skill recommendation layout is incomplete.");
            recommendationRankTabs.Clear();

            var rankByProfession = recommendations
                .Select(value => professionRanks.TryGetValue(value.Profession, out int rank) ? rank : 0)
                .Where(value => value > 0).Distinct().OrderBy(value => value).ToArray();
            int currentProfession = progression.Player.Wardrobe.Profession;
            recommendationRank = professionRanks.TryGetValue(currentProfession, out int currentRank)
                && rankByProfession.Contains(currentRank) ? currentRank : rankByProfession.Last();
            foreach (int rank in rankByProfession)
            {
                var tabNodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
                var tabButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
                RectTransform tab = factory.Build(CharacterPanelNodeFactory.ReadRoot(
                    CharacterModelPackage.AsObject(skillUiTemplates["skill_tab"], "skill_tab")),
                    tabParent, tabNodes, tabButtons);
                tab.gameObject.name = "SkillRecommendRankTab_" + rank.ToString(CultureInfo.InvariantCulture);
                Button tabButton = tab.GetComponent<Button>() ?? tab.gameObject.AddComponent<Button>();
                Graphic target = tab.GetComponent<Graphic>() ?? tab.GetComponentInChildren<Graphic>(true);
                if (target == null)
                {
                    Image raycast = tab.gameObject.AddComponent<Image>();
                    raycast.color = new Color(1f, 1f, 1f, 0f);
                    target = raycast;
                }
                tabButton.targetGraphic = target;
                SetTextValue(DialogNode(tabNodes, "text")?.GetComponent<Text>(),
                    rank.ToString(CultureInfo.InvariantCulture) + "\u9636");
                recommendationRankTabs.Add(rank, tab.gameObject);
                int capturedRank = rank;
                tabButton.onClick.AddListener(() =>
                {
                    SetSelectedRecommendationRank(capturedRank);
                    ShowRecommendationsForRank(capturedRank, recommendations, professionRanks,
                        context.CurrentProfessionChain, itemParent);
                });
            }
            SetSelectedRecommendationRank(recommendationRank);
            ShowRecommendationsForRank(recommendationRank, recommendations, professionRanks,
                context.CurrentProfessionChain, itemParent);
        }

        private void SetSelectedRecommendationRank(int rank)
        {
            foreach (KeyValuePair<int, GameObject> pair in recommendationRankTabs)
            {
                if (pair.Value == null) continue;
                Transform selected = FindDeep(pair.Value.transform, "Selected");
                if (selected != null) selected.gameObject.SetActive(pair.Key == rank);
            }
        }

        private void ShowRecommendationsForRank(int rank,
            IReadOnlyList<OfficialSkillRecommendation> recommendations,
            IReadOnlyDictionary<int, int> professionRanks, ISet<int> professionChain, RectTransform parent)
        {
            recommendationRank = rank;
            foreach (GameObject item in recommendationItems) if (item != null) Destroy(item);
            recommendationItems.Clear();
            foreach (OfficialSkillRecommendation recommendation in recommendations.Where(value =>
                         professionChain.Contains(value.Profession)
                         && professionRanks.TryGetValue(value.Profession, out int valueRank) && valueRank == rank))
            {
                var itemNodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
                var itemButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
                RectTransform item = factory.Build(CharacterPanelNodeFactory.ReadRoot(
                    CharacterModelPackage.AsObject(skillUiTemplates["skill_recommend_item"],
                        "skill_recommend_item")), parent, itemNodes, itemButtons);
                item.gameObject.name = "SkillRecommendItem_" + recommendation.Id.ToString(CultureInfo.InvariantCulture);
                SetTextValue(DialogNode(itemNodes, "TitleTxt")?.GetComponent<Text>(), recommendation.name);
                SetTextValue(DialogNode(itemNodes, "DescText")?.GetComponent<Text>(), recommendation.description);
                BuildRecommendedSkillIcons(itemNodes, "ActiveSkillGo", recommendation.ActiveSkillList);
                BuildRecommendedSkillIcons(itemNodes, "PassiveSkillGo", recommendation.PassiveSkillList);
                OfficialSkillRecommendation captured = recommendation;
                Button apply = DialogButton(itemButtons, "SaveBtn");
                if (apply != null) apply.onClick.AddListener(() => ApplyRecommendation(captured));
                recommendationItems.Add(item.gameObject);
            }
        }

        private void BuildRecommendedSkillIcons(Dictionary<string, RectTransform> nodes,
            string groupName, int[] skillIds)
        {
            RectTransform group = DialogNode(nodes, groupName);
            if (group == null) return;
            foreach (int skillId in skillIds ?? Array.Empty<int>())
            {
                if (progression.SkillDefinition(skillId) == null) continue;
                BuildSkillIcon(skillId, group, "RecommendedSkill_" + skillId.ToString(CultureInfo.InvariantCulture));
            }
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
            recommendationItems.Clear();
            recommendationRankTabs.Clear();
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
            dialog.transform.SetParent(panel != null ? panel.transform.parent : transform.parent, false);
            RectTransform root = (RectTransform)dialog.transform;
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            dialogNodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            dialogButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
            factory.Build(CharacterPanelNodeFactory.ReadRoot(template), root, dialogNodes, dialogButtons);
            var sourceRoot = CharacterPanelNodeFactory.ReadRoot(template);
            CharacterSafeArea.Restore(dialogNodes[sourceRoot.path], sourceRoot.path, skillUiTemplates);
            var source = CharacterModelPackage.AsObject(template["root"], "dialog root");
            var localNodes = dialogNodes;
            if (source.TryGetValue("original_bindings", out object rawBindings)
                && CharacterModelPackage.AsObject(rawBindings, "dialog bindings").TryGetValue("PopupWindow", out object rawPopup))
            {
                var popup = CharacterModelPackage.AsObject(rawPopup, "PopupWindow");
                int maskType = Convert.ToInt32(popup["mMaskType"], CultureInfo.InvariantCulture);
                if (maskType != 0)
                {
                    var mask = dialog.AddComponent<OfficialSkillPopupMask>();
                    mask.Show(skillUiTemplates, maskType,
                        Convert.ToInt32(popup["mCloseByClickMask"], CultureInfo.InvariantCulture) != 0,
                        () => HandleEscape(), () => PlayOfficialLayoutAnimations(layoutKey, template, localNodes));
                    return dialog;
                }
            }
            PlayOfficialLayoutAnimations(layoutKey, template, localNodes);
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
                else StartCoroutine(PlaySkillAnimation(tracks, duration, false));
            }
        }

        internal void PlayCharacterPageState(Dictionary<string, object> template,
            Dictionary<string, RectTransform> layoutNodes, string animationName)
        {
            var root = CharacterModelPackage.AsObject(template["root"], "character page root");
            PlayCharacterNodeState(root, layoutNodes, animationName);
        }

        private void PlayCharacterNodeState(Dictionary<string, object> node,
            Dictionary<string, RectTransform> layoutNodes, string animationName)
        {
            if (node.TryGetValue("animations", out object rawAnimations)
                && rawAnimations is List<object> animations)
                foreach (object raw in animations)
                {
                    if (!(raw is Dictionary<string, object> clip)
                        || CharacterPanelNodeFactory.String(clip, "name") != animationName) continue;
                    if (characterStateAnimation != null) StopCoroutine(characterStateAnimation);
                    // Animator Write Defaults restores channels absent from the next official state.
                    if (characterStateTracks != null)
                        foreach (SkillAnimationTrack track in characterStateTracks)
                            ApplySkillAnimationValue(track.Target, track.Property, track.InitialValue);
                    if (TryBuildAnimationTracks(clip, layoutNodes, out var tracks, out float duration))
                    {
                        characterStateTracks = tracks;
                        foreach (SkillAnimationTrack track in tracks)
                            track.InitialValue = ReadSkillAnimationValue(track.Target, track.Property);
                        characterStateAnimation = StartCoroutine(PlaySkillAnimation(tracks, duration, false));
                    }
                    return;
                }
            if (node.TryGetValue("children", out object rawChildren) && rawChildren is List<object> children)
                foreach (object child in children)
                    PlayCharacterNodeState(CharacterModelPackage.AsObject(child, "character node"),
                        layoutNodes, animationName);
        }

        private Coroutine characterStateAnimation;
        private List<SkillAnimationTrack> characterStateTracks;

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
                    float inSlope = values.Count > 2 ? values[2] == null ? float.PositiveInfinity
                        : Convert.ToSingle(values[2], CultureInfo.InvariantCulture) : 0f;
                    float outSlope = values.Count > 3 ? values[3] == null ? float.PositiveInfinity
                        : Convert.ToSingle(values[3], CultureInfo.InvariantCulture) : 0f;
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

        private static float ReadSkillAnimationValue(Transform target, string property)
        {
            if (target == null) return 0f;
            RectTransform rect = target as RectTransform;
            bool x = property.EndsWith(".x", StringComparison.Ordinal);
            if (property.StartsWith("m_AnchoredPosition.", StringComparison.Ordinal))
                return x ? rect.anchoredPosition.x : rect.anchoredPosition.y;
            if (property.StartsWith("m_AnchorMin.", StringComparison.Ordinal)) return x ? rect.anchorMin.x : rect.anchorMin.y;
            if (property.StartsWith("m_AnchorMax.", StringComparison.Ordinal)) return x ? rect.anchorMax.x : rect.anchorMax.y;
            if (property.StartsWith("m_SizeDelta.", StringComparison.Ordinal)) return x ? rect.sizeDelta.x : rect.sizeDelta.y;
            if (property.StartsWith("scale.", StringComparison.Ordinal))
                return x ? target.localScale.x : property.EndsWith(".y", StringComparison.Ordinal) ? target.localScale.y : target.localScale.z;
            if (property == "m_IsActive") return target.gameObject.activeSelf ? 1f : 0f;
            if (property == "m_Interactable") return target.GetComponent<CanvasGroup>()?.interactable == false ? 0f : 1f;
            Graphic graphic = target.GetComponent<Graphic>();
            if (property == "m_Alpha")
            {
                CanvasGroup group = target.GetComponent<CanvasGroup>();
                return group != null ? group.alpha : graphic != null ? graphic.color.a : 1f;
            }
            Color color = graphic == null ? Color.white : graphic.color;
            return property.EndsWith(".r", StringComparison.Ordinal) ? color.r
                : property.EndsWith(".g", StringComparison.Ordinal) ? color.g
                : property.EndsWith(".b", StringComparison.Ordinal) ? color.b : color.a;
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
            else if (property.StartsWith("m_AnchorMin.", StringComparison.Ordinal)
                || property.StartsWith("m_AnchorMax.", StringComparison.Ordinal)
                || property.StartsWith("m_SizeDelta.", StringComparison.Ordinal))
            {
                if (rect == null) return;
                bool minimum = property.StartsWith("m_AnchorMin.", StringComparison.Ordinal);
                bool maximum = property.StartsWith("m_AnchorMax.", StringComparison.Ordinal);
                Vector2 vector = minimum ? rect.anchorMin : maximum ? rect.anchorMax : rect.sizeDelta;
                if (property.EndsWith(".x", StringComparison.Ordinal)) vector.x = value;
                else vector.y = value;
                if (minimum) rect.anchorMin = vector;
                else if (maximum) rect.anchorMax = vector;
                else rect.sizeDelta = vector;
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
            else if (property == "m_Interactable")
            {
                var group = target.GetComponent<CanvasGroup>() ?? target.gameObject.AddComponent<CanvasGroup>();
                group.interactable = value >= 0.5f;
            }
            else if (property.StartsWith("m_Color.", StringComparison.Ordinal)
                || property.StartsWith("m_fontColor.", StringComparison.Ordinal))
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

        private void SetOfficialState(string layoutKey, string component, string field, int state,
            Dictionary<string, RectTransform> layoutNodes)
        {
            var template = CharacterModelPackage.AsObject(skillUiTemplates[layoutKey], layoutKey);
            var root = CharacterModelPackage.AsObject(template["root"], layoutKey + " root");
            var data = FindOriginalComponent(root, component);
            if (data == null || !data.TryGetValue("references", out object rawReferences)) return;
            object rawSwitcher = data;
            foreach (string key in field.Split('.'))
            {
                var owner = CharacterModelPackage.AsObject(rawSwitcher, "official state owner");
                if (!owner.TryGetValue(key, out rawSwitcher)) return;
            }
            var switcher = CharacterModelPackage.AsObject(rawSwitcher, field);
            var references = CharacterModelPackage.AsObject(rawReferences, "managed references");
            if (!(switcher["mStateConfigs"] is List<object> configs) || !(references["RefIds"] is List<object> refs)) return;
            var ids = new HashSet<string>(configs.OfType<Dictionary<string, object>>()
                .Select(value => Convert.ToString(value["rid"], CultureInfo.InvariantCulture)));
            var states = new Dictionary<string, bool>(StringComparer.Ordinal);
            foreach (var reference in refs.OfType<Dictionary<string, object>>())
            {
                if (!ids.Contains(Convert.ToString(reference["rid"], CultureInfo.InvariantCulture))) continue;
                var config = CharacterModelPackage.AsObject(reference["data"], "state configuration");
                if (config.TryGetValue("mStateColorData", out object rawColors))
                {
                    var colors = CharacterModelPackage.AsObject(rawColors, "original state colors");
                    foreach (var colorConfig in ((List<object>)colors["GraphicConfigs"]).OfType<Dictionary<string, object>>())
                    {
                        var dictionary = CharacterModelPackage.AsObject(colorConfig["Color"], "state color dictionary");
                        var colorEntry = ((List<object>)dictionary["mSerializeBuffer"]).OfType<Dictionary<string, object>>()
                            .FirstOrDefault(value => Convert.ToInt32(CharacterModelPackage.AsObject(value["Key"], "state color key")["mValue"]) == state);
                        if (colorEntry == null) continue;
                        Color color = OriginalDetailColor(colorEntry["Value"]);
                        foreach (string path in ((List<object>)colorConfig["Graphics"]).Select(Convert.ToString))
                            if (layoutNodes.TryGetValue(path, out RectTransform target))
                            {
                                Graphic graphic = target.GetComponent<Graphic>();
                                if (graphic != null) graphic.color = color;
                            }
                    }
                }
                if (!config.TryGetValue("mStateGameObjectData", out object rawGo)) continue;
                var go = CharacterModelPackage.AsObject(rawGo, "state game objects");
                foreach (var entry in ((List<object>)go["GoConfig"]).OfType<Dictionary<string, object>>())
                {
                    int entryState = Convert.ToInt32(CharacterModelPackage.AsObject(entry["State"], "state")["mValue"], CultureInfo.InvariantCulture);
                    foreach (string path in ((List<object>)entry["GameObjects"]).Select(value => Convert.ToString(value, CultureInfo.InvariantCulture)))
                        states[path] = (states.TryGetValue(path, out bool active) && active) || entryState == state;
                }
            }
            foreach (var entry in states)
                if (layoutNodes.TryGetValue(entry.Key, out RectTransform target) && target != null)
                    target.gameObject.SetActive(entry.Value);
        }

        private static Button DialogButton(Dictionary<string, Button> dialogButtons, string nodeName)
        {
            return dialogButtons.FirstOrDefault(pair => pair.Key.EndsWith("/" + nodeName,
                StringComparison.Ordinal)).Value;
        }

        private static RectTransform DialogNode(Dictionary<string, RectTransform> dialogNodes,
            string nodeName)
        {
            return dialogNodes.Values.FirstOrDefault(value => value != null && value.name == nodeName);
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
            Text rank = DialogNode(dialogNodes, "Count")?.GetComponent<Text>();
            int ownedRank = OfflineSkillProgression.OwnedRank(progression.Player.Skills, skillId);
            SetTextValue(name, SkillName(skillId));
            SetTextValue(rank, ownedRank.ToString(CultureInfo.InvariantCulture));
            BindOriginalSkillDetail(skillId, dialogNodes);
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
                SetOfficialState("skill_panel_detail", "SkillPanelSkillDetailDialog", "mCollectionStateSwitcher",
                    progression.Player.Skills.FavoriteSkills.Contains(skillId) ? 1 : 0, dialogNodes);
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
                SetOfficialState("skill_promote", "SkillPromotePanel", "ContentStateSwitcher", 1, dialogNodes);
                PlayOfficialLayoutAnimations("skill_promote", skillUiTemplates["skill_promote"]
                    as Dictionary<string, object>, dialogNodes, "SkillPromotePanel_Switch");
            });
            Button levelTab = DialogButton(dialogButtons, "SkillPromotePanelLevelUpTabGroupItem_11");
            if (levelTab != null) levelTab.onClick.AddListener(() =>
            {
                SetOfficialState("skill_promote", "SkillPromotePanel", "ContentStateSwitcher", 0, dialogNodes);
                PlayOfficialLayoutAnimations("skill_promote", skillUiTemplates["skill_promote"]
                    as Dictionary<string, object>, dialogNodes, "SkillPromotePanel_Switch");
            });

            OfflineSkillPlan planState = progression.Player.Skills.Plans[progression.Player.Skills.SelectedPlan];
            int[] skillSlots = itemType == ActiveSkillItemType ? planState.ActiveSkills : planState.PassiveSkills;
            int equippedSlot = Array.IndexOf(skillSlots, skillId);
            SetOfficialState("skill_promote", "SkillPromotePanel", "ContentStateSwitcher", 1, dialogNodes);
            SetOfficialState("skill_promote", "SkillPromotePanel", "IsNoTabSwitcher", equippedSlot >= 0 ? 1 : 0, dialogNodes);
            SetOfficialState("skill_promote", "SkillPromotePanel", "SkillListShowStateSwitcher", equippedSlot >= 0 ? 1 : 0, dialogNodes);
            SetOfficialState("skill_promote", "SkillPromotePanel", "mPopTipStates", 0, dialogNodes);
            int currentLevel = equippedSlot < 0 ? 0 : progression.SkillProgression.SlotLevel(
                progression.Player.Skills, itemType, equippedSlot, progression.SkillContext().CurrentSeasonId);
            BindOriginalPromoteProperties(skillId, rank, currentLevel, dialogNodes);
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
                && value.GetComponentsInParent<Transform>(true).Any(parent => parent.name == "LevelUpContent"));
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

        private void ToggleFavorite(int skillId)
        {
            ProgressionResult result = progression.ToggleSkillFavorite(skillId);
            LastMessage = result.Message ?? result.Status.ToString();
            if (!result.IsApplied) return;
            ShowOriginalSystemMessage(progression.Player.Skills.FavoriteSkills.Contains(skillId)
                ? "ui_tip_skill_collect" : "ui_tip_skill_uncollect");
            Refresh();
            Changed?.Invoke();
        }

        private void SelectTab(int itemType)
        {
            ClearPendingInstall();
            listType = itemType;
            RefreshCategoryTabs();
            selectedType = 0;
            selectedSlot = -1;
            ShowSkillList(itemType == PieceItemType);
            Refresh();
        }

        private void ShowSkillList(bool pieces)
        {
            if (skillScrollList != null) skillScrollList.gameObject.SetActive(!pieces);
            if (skillPieceScrollList != null) skillPieceScrollList.gameObject.SetActive(pieces);
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
            int[] equipped = itemType == ActiveSkillItemType
                ? progression.Player.Skills.Plans[plan].ActiveSkills : progression.Player.Skills.Plans[plan].PassiveSkills;
            if (equipped[slot] != 0)
            {
                OpenSkillDetail(equipped[slot]);
                return;
            }
            selectedType = itemType;
            selectedSlot = slot;
            listType = itemType;
            ShowSkillList(false);
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

        private void BindPlanSelectedBar()
        {
            var auxiliary = CharacterModelPackage.AsObject(skillUiTemplates["auxiliary_data"], "skill auxiliary data");
            var fields = CharacterModelPackage.AsObject(auxiliary["plan_scrollbar"], "original skill plan scrollbar");
            RectTransform rect = FindDeep("PlanSelectedBar");
            planSelectedBar = rect.GetComponent<Scrollbar>() ?? rect.gameObject.AddComponent<Scrollbar>();
            OfficialSkillComponentBindings.Apply(planSelectedBar, fields.Where(value => value.Key == "m_Colors"
                || value.Key == "m_Transition" || value.Key == "m_Navigation").ToDictionary(value => value.Key, value => value.Value), nodes);
            planSelectedBar.handleRect = nodes[Convert.ToString(fields["m_HandleRect"], CultureInfo.InvariantCulture)];
            planSelectedBar.targetGraphic = nodes[Convert.ToString(fields["m_TargetGraphic"], CultureInfo.InvariantCulture)].GetComponent<Graphic>();
            planSelectedBar.direction = (Scrollbar.Direction)Convert.ToInt32(fields["m_Direction"], CultureInfo.InvariantCulture);
            planSelectedBar.size = Convert.ToSingle(fields["m_Size"], CultureInfo.InvariantCulture);
            planSelectedBar.numberOfSteps = Convert.ToInt32(fields["number_of_steps"], CultureInfo.InvariantCulture);
            planSelectedBar.interactable = Convert.ToBoolean(fields["m_Interactable"], CultureInfo.InvariantCulture);
        }

        private void SwitchPlan(int delta)
        {
            int count = progression.Player.Skills.Plans.Length;
            int target = (plan + delta + count) % count;
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
            Dictionary<string, object> item = progression.Catalog.FindItem(OriginalEffectSkillId(itemId)) ?? progression.Catalog.FindItem(itemId);
            string name = item == null ? itemId.ToString(CultureInfo.InvariantCulture)
                : Convert.ToString(item["Name"], CultureInfo.InvariantCulture);
            int rank = OfflineSkillProgression.OwnedRank(progression.Player.Skills, itemId);
            return rank > 1 ? name + " +" + (rank - 1).ToString(CultureInfo.InvariantCulture) : name;
        }

        private long OriginalSkillProfessionScore(int skillId)
        {
            var skill = progression.Catalog.Find("skill", skillId);
            if (skill == null || !skill.TryGetValue("UnlockProfession", out object rawProfession)) return 0;
            int id = Convert.ToInt32(rawProfession);
            var profession = progression.Catalog.Rows("profession_base").FirstOrDefault(row => AuxInt(row, "Profession") == id);
            if (profession == null) return 0;
            int rank = AuxInt(profession, "Rank"), serial = id;
            while (AuxInt(profession, "PrePro") != 0) {
                serial = AuxInt(profession, "PrePro");
                profession = progression.Catalog.Rows("profession_base").First(row => AuxInt(row, "Profession") == serial);
            }
            return ((long)rank * 100 + serial) * 1000 + id;
        }

        private int Quality(int itemId)
        {
            int rank = OfflineSkillProgression.OwnedRank(progression.Player.Skills, itemId);
            OfflineSkillRankDisplay display = rank <= 0 ? null
                : progression.SkillRules.RankDisplays.FirstOrDefault(value => value.Rank == rank);
            if (display != null) return display.Quality;
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
