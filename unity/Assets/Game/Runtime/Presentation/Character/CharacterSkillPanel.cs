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
            }
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
            var owned = profile.Skills.Where(skill => skill != null && skill.ClassId > 0)
                .ToDictionary(skill => skill.ClassId, skill => skill.Rank);
            var entries = new List<int>();
            foreach (OfflineSkillDefinition definition in progression.SkillRules?.Skills ?? Array.Empty<OfflineSkillDefinition>())
            {
                if (definition.ItemType != listType) continue;
                bool has = owned.ContainsKey(definition.ClassId);
                if (!has && !(showAll != null && showAll.isOn)) continue;
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
            starText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            starText.fontSize = 30;
            starText.alignment = TextAnchor.MiddleCenter;
            starText.color = new Color(1f, 0.85f, 0.3f);
            starText.raycastTarget = false;
            starText.text = OfflineSkillProgression.IsFavorite(progression.Player.Skills, skillId)
                ? "\u2605" : "\u2606";
            Button starButton = starObject.GetComponent<Button>();
            starButton.targetGraphic = starImage;
            starButton.onClick.AddListener(() => ToggleFavorite(skillId));
            Text text = label.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 18;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = !available ? new Color(0.6f, 0.6f, 0.6f)
                : has ? Color.white : new Color(1f, 0.55f, 0.45f);
            text.text = SkillName(skillId) + (available ? string.Empty : "\uff08\u804c\u4e1a\u4e0d\u7b26\uff09");
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
                text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
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
