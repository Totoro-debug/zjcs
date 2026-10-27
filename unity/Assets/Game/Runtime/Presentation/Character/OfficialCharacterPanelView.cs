using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Progression;

namespace Zjcs.Presentation.Character
{
    [Serializable]
    internal sealed class OfficialCharacterPanelsData
    {
        public string manifest;
        public string ui_sha256;
        public OfficialCharacterPanelSet screens;
    }

    [Serializable]
    internal sealed class OfficialCharacterPanelSet
    {
        public OfficialCharacterPanelData character;
        public OfficialCharacterPanelData character_props;
        public OfficialCharacterPanelData rank_upgrade;
        public OfficialCharacterPanelData appearance_preview;
    }

    [Serializable]
    internal sealed class OfficialCharacterPanelData
    {
        public OfficialCharacterPanelNode root;
    }

    [Serializable]
    internal sealed class OfficialCharacterPanelNode
    {
        public string name;
        public string path;
        public bool active = true;
        public float[] anchor_min;
        public float[] anchor_max;
        public float[] anchored_position;
        public float[] size_delta;
        public float[] pivot;
        public float[] scale;
        public float rotation;
        public string[] scripts;
        public string localization_key;
        public Dictionary<string, object> original_bindings;
        public OfficialCharacterPanelNode[] children;
        public OfficialCharacterPanelImage image;
        public OfficialCharacterPanelText text;
        public OfficialCharacterPanelLayoutElement layout_element;
        public OfficialCharacterPanelGroupLayout layout;
        public int[] size_fitter;
        public bool button;
        public bool mask;
    }

    [Serializable]
    internal sealed class OfficialCharacterPanelImage
    {
        public OfficialCharacterPanelSprite sprite;
        public float[] color;
        public int type;
        public bool preserve_aspect;
    }

    [Serializable]
    internal sealed class OfficialCharacterPanelSprite
    {
        public string file;
        public float[] border;
    }

    [Serializable]
    internal sealed class OfficialCharacterPanelText
    {
        public string value;
        public float[] color;
        public float font_size;
        public int horizontal;
        public int vertical;
        public bool word_wrap = true;
        public int overflow;
        public float[] margin;
    }

    [Serializable]
    internal sealed class OfficialCharacterPanelLayoutElement
    {
        public bool ignore;
        public float[] min;
        public float[] preferred;
        public float[] flexible;
    }

    [Serializable]
    internal sealed class OfficialCharacterPanelGroupLayout
    {
        public int axis;
        public int[] padding;
        public int alignment;
        public float spacing;
        public bool[] expand;
        public bool[] control;
        public bool[] child_scale;
        public bool reverse;
    }

    public sealed class OfficialCharacterPanelView : MonoBehaviour
    {
        private const string CharacterScreenPath = "/CharacterPanel";
        private const string CharacterPetModelRootPath = "/CharacterPanel/BgPanel1/Content/TopGo/MyPlayerModelContent/PlayerModelLayout/PetSelectContent/EquipPetContent/PetModelSpawnerForUI/PetModelRoot";
        private const string CharacterPetModelSpawnerPath = "/CharacterPanel/BgPanel1/Content/TopGo/MyPlayerModelContent/PlayerModelLayout/PetSelectContent/EquipPetContent/PetModelSpawnerForUI";
        private readonly Dictionary<string, RectTransform> nodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> nodeActive = new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly Dictionary<string, Button> buttons = new Dictionary<string, Button>(StringComparer.Ordinal);
        private readonly Dictionary<string, RectTransform> dialogNodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
        private readonly Dictionary<string, Button> dialogButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
        private OfficialCharacterAssetCatalog assets;
        private OfflineCharacterProgressionService progression;
        private string characterSlotId;
        private string characterSavesRoot;
        private CharacterPanelNodeFactory factory;
        private Slider experienceSlider;
        private Component experienceText;
        private Component levelText;
        private Component levelLimitText;
        private Component rankText;
        private Component subRankText;
        private Image rankImage;
        private GameObject activeDialog;
        private GameObject characterPanelRoot;
        private Text operationResultText;
        private OfficialCharacterPanelNode characterRoot;
        private Coroutine experienceAnimation;
        private float experienceAnimationDuration = 1f;
        private CharacterModelPreview characterModel;
        private OfficialWardrobePanelView wardrobeView;
        private CharacterPropPanels propPanels;
        private CharacterRankPanel rankPanel;
        private CharacterEquipSlots equipSlots;
        private CharacterSkillPanel skillPanel;
        private CharacterPetEquipmentView petEquipmentView;
        private Component honorTimeText;
        private GameObject honorTimeContent;
        private Component honorPropText;
        private GameObject honorPropContent;
        private long honorTitleExpiry;
        private float honorClock;
        private int templateSpritesBound;
        private int templateSpritesMissing;

        public event Action CloseRequested;

        public event Action AppearanceRequested;
        public event Action PropertiesRequested;
        public event Action RankUpgradeRequested;
        public event Action StoredExperienceHelpRequested;
        public event Action<string> OfficialModuleRequested;

        private void OnDestroy()
        {
            if (experienceAnimation != null) StopCoroutine(experienceAnimation);
            experienceAnimation = null;
            CloseDialog();
            if (propPanels != null) propPanels.CloseDialog();
            if (rankPanel != null) rankPanel.Close();
            if (equipSlots != null) equipSlots.CloseList();
            if (skillPanel != null)
            {
                skillPanel.Changed -= HandleSkillPanelChanged;
                skillPanel.Close();
            }
            if (wardrobeView != null) Destroy(wardrobeView.gameObject);
            wardrobeView = null;
        }

        /// <summary>
        /// Original <c>CharacterPanel.OnLowMemory</c>: release sub pages that are no longer shown.
        /// </summary>
        public void HandleLowMemory()
        {
            if (!gameObject.activeInHierarchy) return;
            if (wardrobeView != null && !wardrobeView.gameObject.activeSelf) CloseWardrobe();
            if (rankPanel != null && !rankPanel.IsOpen) rankPanel.Close();
            if (equipSlots != null && !equipSlots.IsOpen) equipSlots.CloseList();
            if (skillPanel != null && !skillPanel.IsOpen) skillPanel.Close();
        }

        private void Update()
        {
            TickHonorTitle();
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (propPanels != null && propPanels.HandleEscape()) return;
            if (rankPanel != null && rankPanel.IsOpen) { rankPanel.Close(); return; }
            if (equipSlots != null && equipSlots.HandleEscape()) return;
            if (skillPanel != null && skillPanel.HandleEscape()) return;
            if (activeDialog != null) CloseDialog();
            else if (wardrobeView != null)
            {
                if (!wardrobeView.HandleEscape()) CloseWardrobe();
            }
            else CloseRequested?.Invoke();
        }

        public void Initialize(OfficialCharacterAssetCatalog assetCatalog,
            OfflineCharacterProgressionService characterProgression, string selectedSlotId = null,
            string profileSavesRoot = null)
        {
            if (activeDialog != null) Destroy(activeDialog);
            activeDialog = null;
            nodes.Clear();
            nodeActive.Clear();
            buttons.Clear();
            dialogNodes.Clear();
            dialogButtons.Clear();
            for (int i = transform.childCount - 1; i >= 0; i--)
                Destroy(transform.GetChild(i).gameObject);

            assets = assetCatalog != null ? assetCatalog : throw new ArgumentNullException(nameof(assetCatalog));
            progression = characterProgression != null ? characterProgression
                : throw new ArgumentNullException(nameof(characterProgression));
            characterSlotId = selectedSlotId;
            characterSavesRoot = string.IsNullOrWhiteSpace(profileSavesRoot)
                ? Application.persistentDataPath : profileSavesRoot;
            assets.ValidateBindings();
            CharacterPanelNodeFactory.SetUiFont(assets.UiFont);
            factory = new CharacterPanelNodeFactory(assets);

            Dictionary<string, object> panelScreens = ReadPanelScreens();
            if (!panelScreens.TryGetValue("character", out object characterData))
                throw new FormatException("Pinned original character panel data are invalid.");

            Canvas canvas = GetComponent<Canvas>();
            if (canvas == null) canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            if (GetComponent<CanvasScaler>() == null)
            {
                CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080f, 1920f);
                scaler.matchWidthOrHeight = 0f;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            }
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            RectTransform root = gameObject.GetComponent<RectTransform>();
            if (root == null) root = gameObject.AddComponent<RectTransform>();
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            characterRoot = CharacterPanelNodeFactory.ReadRoot(
                CharacterModelPackage.AsObject(characterData, "character panel"));
            templateSpritesBound = 0;
            templateSpritesMissing = 0;
            BuildOfficialCharacterPanel(root);
            ApplyTemplateVisuals(characterRoot);
            Debug.Log("[Character] official prefab sprites bound=" + templateSpritesBound
                + " missing=" + templateSpritesMissing);
            BindOfficialTargets();
            // GameMatchMiniPanel.RefreshUI hides its content when there is no matching target.
            Node("/CharacterPanel/BgPanel1/Content/TopGo/GameMatchMiniPanel/ContentGo")?.gameObject.SetActive(false);
            InitializeCharacterModel();
            InitializePropPanels();
            Refresh();
        }

        private void BuildOfficialCharacterPanel(RectTransform parent)
        {
            GameObject prefab = Resources.Load<GameObject>("Character/CharacterPanel");
            if (prefab == null)
                throw new InvalidOperationException("The pinned official CharacterPanel prefab is not packaged.");

            var staging = new GameObject("CharacterPanelPrefabStaging", typeof(RectTransform));
            staging.transform.SetParent(parent, false);
            staging.SetActive(false);
            GameObject instance = Instantiate(prefab, staging.transform, false);
            instance.name = "CharacterPanel";
            ApplyPrefabSafeAreas(instance.transform);
            ConfigureOfficialCanvases(instance.transform);
            StripUnavailableOfficialBehaviours(instance.transform);
            instance.transform.SetParent(parent, false);
            characterPanelRoot = instance;
            RectTransform pageRoot = instance.transform as RectTransform;
            pageRoot.anchorMin = Vector2.zero;
            pageRoot.anchorMax = Vector2.one;
            pageRoot.offsetMin = Vector2.zero;
            pageRoot.offsetMax = Vector2.zero;
            IndexPrefabNodes(instance.transform, string.Empty);
            Destroy(staging);
        }

        private static void ConfigureOfficialCanvases(Transform root)
        {
            foreach (Canvas canvas in root.GetComponentsInChildren<Canvas>(true))
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.worldCamera = null;
                if (canvas.GetComponent<GraphicRaycaster>() == null)
                    canvas.gameObject.AddComponent<GraphicRaycaster>();
            }
        }

        private void IndexPrefabNodes(Transform current, string parentPath)
        {
            string path = parentPath + "/" + current.name;
            if (current is RectTransform rect)
            {
                nodes[path] = rect;
                nodeActive[path] = current.gameObject.activeSelf;
            }
            Button button = current.GetComponent<Button>();
            if (button != null) buttons[path] = button;
            foreach (Transform child in current) IndexPrefabNodes(child, path);
        }

        private static void ApplyPrefabSafeAreas(Transform root)
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null || behaviour.GetType().Name != "SafeArea") continue;
                if (behaviour.transform is RectTransform rect)
                {
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.pivot = Vector2.zero;
                    rect.sizeDelta = Vector2.zero;
                    rect.anchoredPosition = Vector2.zero;
                }
            }
        }

        internal static void StripUnavailableOfficialBehaviours(Transform root)
        {
            foreach (MonoBehaviour behaviour in root.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null) continue;
                Type type = behaviour.GetType();
                if (type.Assembly == typeof(Button).Assembly || type.Assembly == typeof(TMP_Text).Assembly
                    || behaviour is LayoutGroup || behaviour is ILayoutElement || behaviour is IMeshModifier)
                    continue;
                DestroyImmediate(behaviour);
            }
        }

        private void ApplyTemplateVisuals(OfficialCharacterPanelNode node)
        {
            if (node == null) return;
            if (nodes.TryGetValue(node.path, out RectTransform target))
            {
                if (Array.IndexOf(node.scripts ?? Array.Empty<string>(), "SafeArea") >= 0)
                    CharacterPanelNodeFactory.ApplySafeAreaLayout(node, target);
                if (node.image != null && target.GetComponent<Image>() is Image image)
                {
                    Sprite sprite = null;
                    if (node.image.sprite != null)
                    {
                        string source = node.image.sprite.file;
                        sprite = assets.FindSprite(source)
                            ?? assets.FindSkillAppearanceSprite(source)
                            ?? Resources.Load<Sprite>(source);
                        if (sprite == null) templateSpritesMissing++;
                        else
                        {
                            templateSpritesBound++;
                            image.sprite = sprite;
                        }
                    }
                    image.color = CharacterPanelNodeFactory.ImageColor(node.image,
                        CharacterPanelNodeFactory.ColorValue(node.image.color, Color.white));
                    image.type = (Image.Type)Mathf.Clamp(node.image.type, 0, 3);
                    image.preserveAspect = node.image.preserve_aspect;
                }
                if (node.text != null && !string.IsNullOrEmpty(node.text.value))
                    SetText(FindLabel(target), node.text.value);
            }
            foreach (OfficialCharacterPanelNode child in node.children ?? Array.Empty<OfficialCharacterPanelNode>())
                ApplyTemplateVisuals(child);
        }

        public void Refresh()
        {
            if (progression == null) return;
            Refresh(progression.Experience());
            if (propPanels != null) propPanels.RefreshSummary(transform);
            if (equipSlots != null) equipSlots.Refresh(transform);
            if (petEquipmentView != null) petEquipmentView.RefreshCurrent();
            if (characterModel != null) characterModel.Refresh();
            RefreshHonorTitle();
            RefreshProfession();
        }

        public ProgressionResult AwardExperience(long amount, string eventId)
        {
            if (progression == null) throw new InvalidOperationException("The character panel has not been initialized.");
            long previousExperience = progression.Player.Experience;
            long previousBlessingExperience = progression.Player.BlessingExperience;
            ProgressionResult result = progression.AwardExperience(amount, eventId);
            if (!result.IsApplied) return result;

            if (experienceAnimation != null) StopCoroutine(experienceAnimation);
            experienceAnimation = StartCoroutine(PlayExperience(previousExperience,
                previousBlessingExperience, progression.Player.Experience,
                progression.Player.BlessingExperience));
            return result;
        }

        public void CloseDialog()
        {
            if (activeDialog == null) return;
            Destroy(activeDialog);
            activeDialog = null;
            dialogNodes.Clear();
            dialogButtons.Clear();
            operationResultText = null;
        }

        private void ShowOfficialDialog(OfficialCharacterPanelData panel, string screenPath, bool rankUpgrade)
        {
            CloseDialog();
            if (panel == null || panel.root == null) throw new FormatException("Original character dialog data are invalid.");
            activeDialog = new GameObject(screenPath, typeof(RectTransform));
            activeDialog.transform.SetParent(transform, false);
            RectTransform rect = (RectTransform)activeDialog.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            dialogNodes.Clear();
            dialogButtons.Clear();
            factory.Build(panel.root, rect, dialogNodes, dialogButtons);
            string closePath = dialogButtons.Keys.FirstOrDefault(path => path.EndsWith("/CloseBtn", StringComparison.Ordinal));
            if (closePath != null) BindDialogButton(closePath, CloseDialog);
            if (rankUpgrade)
            {
                string usePath = dialogButtons.Keys.FirstOrDefault(path => path.EndsWith("/UseItemBtn", StringComparison.Ordinal));
                if (usePath != null) BindDialogButton(usePath, UpgradeSubRank);
            }
            operationResultText = dialogNodes.Values.Select(node => node.GetComponent<Text>())
                .FirstOrDefault(text => text != null && text.name.IndexOf("Result", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void UpgradeSubRank()
        {
            ProgressionResult result = progression.UpgradeSubRank("character-rank-up-" + Guid.NewGuid().ToString("N"));
            if (operationResultText != null) operationResultText.text = result.Message ?? result.Status.ToString();
            if (result.IsApplied)
            {
                CloseDialog();
                Refresh();
            }
        }

        private void Refresh(CharacterExperienceSnapshot experience)
        {
            SetText(levelText, experience.Level.ToString());
            SetText(levelLimitText, "/" + experience.LevelLimit.ToString());
            if (experienceText != null)
            {
                if (experience.IsMaxBlessing)
                {
                    SetText(experienceText, OfficialSeasonLimitText());
                }
                else
                {
                    string amount = OfficialPropFormat.Compact(experience.CurrentExperience);
                    if (experience.IsStoredExperience) amount = "<color=#e17126>" + amount + "</color>";
                    SetText(experienceText, amount + "/" + OfficialPropFormat.Compact(experience.RequiredExperience));
                }
            }
            if (experienceSlider != null) experienceSlider.value = experience.Fill;

            SetText(rankText, OfficialRankName(progression.Player.SubRank));
            SetText(subRankText, OfficialSubRankName(progression.Player.SubRank));
            if (rankImage != null) rankImage.sprite = OfficialRankSprite(progression.Player.SubRank);
        }

        private IEnumerator PlayExperience(long previousExperience, long previousBlessingExperience,
            long currentExperience, long currentBlessingExperience)
        {
            float elapsed = 0f;
            while (elapsed < experienceAnimationDuration)
            {
                elapsed += Time.deltaTime;
                float ratio = Mathf.Clamp01(elapsed / experienceAnimationDuration);
                Refresh(progression.Experience(Interpolate(previousExperience, currentExperience, ratio),
                    Interpolate(previousBlessingExperience, currentBlessingExperience, ratio)));
                yield return null;
            }
            Refresh(progression.Experience(currentExperience, currentBlessingExperience));
            experienceAnimation = null;
        }

        private void BindOfficialTargets()
        {
            experienceSlider = ComponentAt<Slider>("/CharacterPanel/BgPanel1/Content/TopGo/CharacterPanelPropPanel/Content/Slot/SlotLevel/ExpSlider/Slider");
            experienceText = TextAt("/CharacterPanel/BgPanel1/Content/TopGo/CharacterPanelPropPanel/Content/Slot/SlotLevel/ExpTitle/ExpText");
            levelText = TextAt("/CharacterPanel/BgPanel1/Content/TopGo/CharacterPanelPropPanel/Content/Slot/SlotLevel/LevelText/LevelTitle/LevelTitle/LevelText");
            levelLimitText = TextAt("/CharacterPanel/BgPanel1/Content/TopGo/CharacterPanelPropPanel/Content/Slot/SlotLevel/LevelText/LevelTitle/LevelTitle/LevelText/MaxLevelText");
            rankText = TextAt("/CharacterPanel/BgPanel1/Content/TopGo/CharacterPanelPropPanel/Content/Slot/SlotRank/Content/RankInfoContent/RankInfo");
            subRankText = TextAt("/CharacterPanel/BgPanel1/Content/TopGo/CharacterPanelPropPanel/Content/Slot/SlotRank/Content/RankInfoContent/SubLevelInfo");
            rankImage = ComponentAt<Image>("/CharacterPanel/BgPanel1/Content/TopGo/CharacterPanelPropPanel/Content/Slot/SlotRank/Content/Mask/SubRankImage");
            experienceAnimationDuration = OfficialExperienceAnimationDuration();

            BindButton("/CharacterPanel/BgPanel1/Content/TopGo/CharacterPanelPropPanel/Content/AppearanceBtn",
                OpenWardrobe);
            BindButton("/CharacterPanel/BgPanel1/Content/TopGo/CharacterPanelPropPanel/Content/Slot/SlotRank",
                OpenRankPanel);
            BindButton("/CharacterPanel/BgPanel1/Content/TopGo/CharacterPanelPropPanel/Content/Slot/SlotProp",
                OpenProperties);
            BindButton("/CharacterPanel/BgPanel1/Content/TopGo/CharacterPanelPropPanel/Content/Slot/SlotLevel/ExpTitle/BtnGo/RuleBtn",
                OpenStoredExperienceHelp);
            BindButton("/CharacterPanel/BgPanel1/Content/TopGo/CharacterPanelPropPanel/Content/Slot/SlotOther/CharacterPanelProfessionBtn",
                () => OfficialModuleRequested?.Invoke("profession"));
            BindButton("/CharacterPanel/BgPanel1/Content/TopGo/CharacterPanelPropPanel/Content/Slot/SlotOther/CharacterPanelSkillBtn",
                OpenSkillPanel);
            BindButton("/CharacterPanel/BgPanel1/Content/TopGo/CharacterPanelPropPanel/Content/Slot/SlotOther/CharacterPanelPetBtn",
                () => OfficialModuleRequested?.Invoke("pets"));
            string closePath = buttons.Keys.FirstOrDefault(path => path.StartsWith(CharacterScreenPath, StringComparison.Ordinal)
                && (path.EndsWith("/CloseBtn", StringComparison.Ordinal)
                    || path.EndsWith("/CloseBtn_dark", StringComparison.Ordinal)));
            if (closePath != null) BindButton(closePath, () => CloseRequested?.Invoke());
        }

        private void OpenProperties()
        {
            PropertiesRequested?.Invoke();
            if (propPanels == null) throw new InvalidOperationException("The property panels are not initialized.");
            propPanels.OpenDialog(transform, "ui_player_prop_text");
        }

        private void InitializePropPanels()
        {
            var holder = new GameObject("CharacterPropPanels", typeof(RectTransform));
            holder.transform.SetParent(transform, false);
            RectTransform rect = (RectTransform)holder.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            propPanels = holder.AddComponent<CharacterPropPanels>();
            propPanels.Initialize(assets, progression.Catalog,
                () => progression.EvaluateProps(DateTimeOffset.UtcNow.ToUnixTimeSeconds()));
            rankPanel = holder.AddComponent<CharacterRankPanel>();
            rankPanel.Initialize(assets, progression);
            rankPanel.Upgraded += Refresh;
            equipSlots = holder.AddComponent<CharacterEquipSlots>();
            equipSlots.Initialize(assets, progression, progression.EquipmentCatalog);
            equipSlots.Changed += Refresh;
            skillPanel = holder.AddComponent<CharacterSkillPanel>();
            skillPanel.Initialize(assets, progression);
            skillPanel.Changed += HandleSkillPanelChanged;
            skillPanel.PlayerHeaderVisibilityChanged += visible =>
            {
                if (skillHeader != null) skillHeader.gameObject.SetActive(visible);
            };

            petEquipmentView = holder.AddComponent<CharacterPetEquipmentView>();
            RectTransform petModelRoot = ResolvePetModelRoot();
            petEquipmentView.Initialize(assets, progression,
                Node("/CharacterPanel/BgPanel1/Content/TopGo/MyPlayerModelContent/PlayerModelLayout/PetSelectContent"),
                Node("/CharacterPanel/BgPanel1/Content/TopGo/MyPlayerModelContent/PlayerModelLayout/PetSelectContent/EquipPetContent"),
                Node("/CharacterPanel/BgPanel1/Content/TopGo/MyPlayerModelContent/PlayerModelLayout/PetSelectContent/EmptyPetContent"),
                petModelRoot,
                Node("/CharacterPanel/BgPanel1/Content/TopGo/MyPlayerModelContent/PlayerModelLayout/PetSelectContent/EmptyPetContent/Contet/Add"),
                Node("/CharacterPanel/BgPanel1/Content/TopGo/MyPlayerModelContent/PlayerModelLayout/PetSelectContent/EquipPetContent/RecommendBtn"),
                transform);
            petEquipmentView.Changed += Refresh;
        }

        private RectTransform ResolvePetModelRoot()
        {
            RectTransform modelRoot = Node(CharacterPetModelRootPath);
            if (modelRoot != null) return modelRoot;

            RectTransform spawner = Node(CharacterPetModelSpawnerPath);
            if (spawner == null)
                throw new InvalidOperationException("The official character pet model spawner is missing.");
            Transform existing = spawner.Find("PetModelRoot");
            if (existing is RectTransform existingRect)
            {
                nodes[CharacterPetModelRootPath] = existingRect;
                return existingRect;
            }

            var root = new GameObject("PetModelRoot", typeof(RectTransform));
            modelRoot = (RectTransform)root.transform;
            modelRoot.SetParent(spawner, false);
            modelRoot.anchorMin = new Vector2(0.5f, 0f);
            modelRoot.anchorMax = new Vector2(0.5f, 0f);
            modelRoot.anchoredPosition = Vector2.zero;
            modelRoot.sizeDelta = new Vector2(100f, 100f);
            modelRoot.pivot = new Vector2(0f, 0.5f);
            modelRoot.localScale = new Vector3(-1f, 1f, 1f);
            nodes[CharacterPetModelRootPath] = modelRoot;
            return modelRoot;
        }

        private RectTransform Node(string path)
        {
            if (nodes.TryGetValue(path, out RectTransform value) && value != null) return value;
            Transform found = transform.Find(path.TrimStart('/'));
            if (found is RectTransform rect)
            {
                nodes[path] = rect;
                return rect;
            }
            return null;
        }

        private void HandleSkillPanelChanged()
        {
            SetSkillPageState(skillPanel != null && skillPanel.IsOpen);
            if (petEquipmentView != null)
            {
                if (skillPanel != null && skillPanel.IsOpen)
                    petEquipmentView.ShowPlan(progression.Player.Skills.SelectedPlan);
                else petEquipmentView.ShowFollowing();
            }
            Refresh();
        }

        private void OpenStoredExperienceHelp()
        {
            StoredExperienceHelpRequested?.Invoke();
            ShowOfficialDialog(ReadScreens().character_props, "StoredExperienceHelp", false);
            Dictionary<string, object> overview = (Dictionary<string, object>)OfficialEquipmentJson.Parse(
                assets.CharacterOverview.text);
            Dictionary<string, object> texts = (Dictionary<string, object>)overview["texts"];
            string title = Convert.ToString(texts["player_exp_max_title"], System.Globalization.CultureInfo.InvariantCulture);
            string body = Convert.ToString(texts["player_exp_max_desc1"], System.Globalization.CultureInfo.InvariantCulture)
                + "\n\n" + Convert.ToString(texts["player_exp_max_desc2"], System.Globalization.CultureInfo.InvariantCulture);
            Text titleText = dialogNodes.Values.Select(value => value.GetComponent<Text>())
                .FirstOrDefault(text => text != null && text.name == "TitleText");
            SetText(titleText, title);
            RectTransform content = dialogNodes.Values.FirstOrDefault(value => value.name == "OverallContent");
            if (content != null)
            {
                var bodyObject = new GameObject("StoredExperienceBody", typeof(RectTransform), typeof(Text));
                bodyObject.transform.SetParent(content, false);
                RectTransform bodyRect = (RectTransform)bodyObject.transform;
                bodyRect.anchorMin = new Vector2(0.05f, 0.1f);
                bodyRect.anchorMax = new Vector2(0.95f, 0.95f);
                bodyRect.offsetMin = Vector2.zero;
                bodyRect.offsetMax = Vector2.zero;
                Text text = bodyObject.GetComponent<Text>();
                text.font = CharacterPanelNodeFactory.UiFont;
                text.fontSize = 26;
                text.color = Color.white;
                text.supportRichText = true;
                text.alignment = TextAnchor.UpperLeft;
                text.text = body;
            }
        }

        private void OpenSkillPanel()
        {
            OfficialModuleRequested?.Invoke("skills");
            if (skillPanel == null) throw new InvalidOperationException("The skill panel is not initialized.");
            if (progression.SkillRules == null)
            {
                Debug.LogWarning("Official skill rules are unavailable.");
                return;
            }
            skillPanel.Open(transform);
            SetSkillPageState(true);
            if (petEquipmentView != null)
                petEquipmentView.ShowPlan(progression.Player.Skills.SelectedPlan);
        }

        private bool skillPageShown;
        private OfficialCharacterSkillHeader skillHeader;

        private void SetSkillPageState(bool show)
        {
            if (skillPageShown == show)
            {
                if (show && skillHeader != null) skillHeader.Refresh();
                return;
            }
            skillPageShown = show;
            if (show && skillHeader == null)
            {
                var headerRoot = new GameObject("OfficialSkillPlayerHeader", typeof(RectTransform));
                headerRoot.transform.SetParent(transform, false);
                RectTransform headerRect = (RectTransform)headerRoot.transform;
                headerRect.anchorMin = Vector2.zero; headerRect.anchorMax = Vector2.one;
                headerRect.offsetMin = Vector2.zero; headerRect.offsetMax = Vector2.zero;
                skillHeader = headerRoot.AddComponent<OfficialCharacterSkillHeader>();
                Transform topSlot = transform.GetComponentsInChildren<Transform>(true).First(value =>
                    value.name == "CharacterPanelSkillPanel").Find("NewPublicTopBarParent/Slot");
                skillHeader.Initialize(assets, progression, characterSlotId, characterSavesRoot, topSlot);
            }
            if (skillHeader != null)
            {
                if (show) { skillHeader.gameObject.SetActive(true); skillHeader.Refresh(); }
                else { Destroy(skillHeader.gameObject); skillHeader = null; }
            }
            if (characterPanelRoot != null) characterPanelRoot.SetActive(true);
            Animator animator = Node("/CharacterPanel/BgPanel1")?.GetComponent<Animator>();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetInteger("StatusId", show ? 1 : 0);
                return;
            }
            Dictionary<string, object> screens = ReadPanelScreens();
            skillPanel.PlayCharacterPageState(CharacterModelPackage.AsObject(screens["character"],
                "official character page"), nodes, show ? "CharacterPanel_Skill" : "CharacterPanel_SkillSwitchmain");
        }

        private void OpenRankPanel()
        {
            RankUpgradeRequested?.Invoke();
            if (rankPanel == null) throw new InvalidOperationException("The rank panel is not initialized.");
            rankPanel.Open(transform);
        }

        private void RefreshProfession()
        {
            int profession = progression.Player.Wardrobe.Profession;
            RectTransform button = nodes.Values.FirstOrDefault(value => value.name == "CharacterPanelProfessionBtn");
            if (button == null) return;
            Component text = button.GetComponentsInChildren<Component>(true)
                .FirstOrDefault(value => value != null && value.name == "Text"
                    && (value is TMP_Text || value is Text));
            string professionName = progression.Catalog.ProfessionName(profession);
            SetText(text, string.IsNullOrEmpty(professionName)
                ? "\u804c\u4e1a " + profession : professionName);
            Image icon = button.GetComponentsInChildren<Image>(true)
                .FirstOrDefault(value => value.name == "Item");
            if (icon != null)
            {
                string uri = progression.Catalog.ProfessionIconUri(profession);
                Sprite sprite = string.IsNullOrEmpty(uri) ? null : assets.FindSprite(uri);
                icon.sprite = sprite;
                icon.enabled = sprite != null;
            }
        }

        private void RefreshHonorTitle()
        {
            const string providerPath =
                "/CharacterPanel/BgPanel1/Content/TopGo/MyPlayerModelContent/HonorTitleItemProvider/Slot";
            const string timePath =
                "/CharacterPanel/BgPanel1/Content/TopGo/MyPlayerModelContent/HonorTitleTimeLimitContent";
            OfflineWardrobeProgress wardrobe = progression.Player.Wardrobe;
            int itemId = wardrobe.WornTitleId;
            RectTransform slot = nodes.TryGetValue(providerPath, out RectTransform mount) ? mount : null;
            if (slot != null)
            {
                for (int i = slot.childCount - 1; i >= 0; i--) Destroy(slot.GetChild(i).gameObject);
                string uri = progression.Catalog.ItemIconUri(itemId);
                Sprite sprite = string.IsNullOrEmpty(uri) ? null : assets.FindSprite(uri);
                if (sprite != null)
                {
                    var icon = new GameObject("TitleIcon", typeof(RectTransform), typeof(Image));
                    icon.transform.SetParent(slot, false);
                    RectTransform rect = (RectTransform)icon.transform;
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                    Image image = icon.GetComponent<Image>();
                    image.sprite = sprite;
                    image.preserveAspect = true;
                    image.raycastTarget = false;
                }
            }
            honorTimeContent = nodes.TryGetValue(timePath, out RectTransform timeRoot) ? timeRoot.gameObject : null;
            honorTimeText = FindTextByPath("/RemainTimeText");
            honorPropText = FindTextByPath("/TitlePropContent/PropText");
            honorPropContent = nodes.TryGetValue(timePath + "/TitlePropContent", out RectTransform propRoot)
                ? propRoot.gameObject : null;
            honorTitleExpiry = itemId == 0 ? 0 : wardrobe.Expiries
                .Where(value => value.ItemId == itemId).Select(value => value.ExpiresAtUnixSeconds)
                .DefaultIfEmpty(0).Max();
            IReadOnlyDictionary<int, long> props = itemId == 0
                ? new Dictionary<int, long>() : progression.Catalog.TitleProperties(itemId);
            if (honorPropText != null)
            {
                var parts = new List<string>();
                foreach (KeyValuePair<int, long> prop in props.OrderBy(pair => pair.Key))
                {
                    OfficialPropDefinition definition = progression.Catalog.Props.Get(prop.Key);
                    if (definition == null) continue;
                    parts.Add(definition.Name + " +" + OfficialPropFormat.Value(definition, prop.Value));
                }
                SetText(honorPropText, string.Join("\u3001", parts));
            }
            if (honorPropContent != null) honorPropContent.SetActive(props.Count > 0);
            if (honorTimeContent != null) honorTimeContent.SetActive(honorTitleExpiry > 0);
            UpdateHonorTimeText();
        }

        private void TickHonorTitle()
        {
            if (honorTitleExpiry <= 0 || honorTimeText == null) return;
            honorClock += Time.unscaledDeltaTime;
            if (honorClock < 1f) return;
            honorClock = 0f;
            UpdateHonorTimeText();
        }

        private void UpdateHonorTimeText()
        {
            if (honorTimeText == null) return;
            long remaining = honorTitleExpiry - DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (remaining <= 0)
            {
                SetText(honorTimeText, string.Empty);
                if (honorTimeContent != null) honorTimeContent.SetActive(false);
                return;
            }
            long days = remaining / 86400;
            long hours = remaining % 86400 / 3600;
            long minutes = remaining % 3600 / 60;
            long seconds = remaining % 60;
            SetText(honorTimeText, days > 0 ? days + "\u5929" + hours + "\u5c0f\u65f6"
                : hours > 0 ? hours + "\u5c0f\u65f6" + minutes + "\u5206\u949f"
                : minutes > 0 ? minutes + "\u5206\u949f" + seconds + "\u79d2" : seconds + "\u79d2");
        }

        private Component FindTextByPath(string suffix)
        {
            return nodes.Where(pair => pair.Key.EndsWith(suffix, StringComparison.Ordinal))
                .Select(pair => FindLabel(pair.Value)).FirstOrDefault(value => value != null);
        }

        private void InitializeCharacterModel()
        {
            const string modelPath = "/CharacterPanel/BgPanel1/Content/TopGo/MyPlayerModelContent/PlayerModelLayout/PlayerModelRoot/MyPlayerModel_UI";
            if (!nodes.TryGetValue(modelPath, out RectTransform mount))
                throw new FormatException("The official character page has no PlayerModelRoot mount.");
            characterModel = mount.GetComponent<CharacterModelPreview>();
            if (characterModel == null) characterModel = mount.gameObject.AddComponent<CharacterModelPreview>();
            characterModel.Initialize(progression, null, characterSlotId, characterSavesRoot);
            Component name = FindText("CharacterName") ?? FindText("PlayerName") ?? FindText("NameText");
            SetText(name, progression.Player.CharacterName);
        }

        private Component FindText(string nameFragment)
        {
            return nodes.Where(pair => pair.Key.IndexOf(nameFragment, StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(pair => FindLabel(pair.Value)).FirstOrDefault(value => value != null);
        }

        private void OpenWardrobe()
        {
            AppearanceRequested?.Invoke();
            if (wardrobeView != null) return;
            SetCharacterModelOnly(true);
            characterModel?.EnterWardrobe();
            GameObject overlay = new GameObject("OfficialWardrobeOverlay", typeof(RectTransform));
            overlay.transform.SetParent(transform, false);
            RectTransform rect = (RectTransform)overlay.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            wardrobeView = overlay.AddComponent<OfficialWardrobePanelView>();
            wardrobeView.Initialize(assets, progression, characterModel, CloseWardrobe, HandleWardrobeModule,
                characterSlotId, characterSavesRoot);
        }

        private void HandleWardrobeModule(string module)
        {
            if (module == "skill-appearance")
            {
                wardrobeView?.ShowSkillAppearancePage();
                return;
            }
            OfficialModuleRequested?.Invoke(module);
        }

        private void CloseWardrobe()
        {
            if (wardrobeView != null) Destroy(wardrobeView.gameObject);
            wardrobeView = null;
            SetCharacterModelOnly(false);
            if (characterModel != null) characterModel.LeaveWardrobe();
            if (propPanels != null) propPanels.RefreshSummary(transform);
            if (equipSlots != null) equipSlots.Refresh(transform);
            if (characterModel != null) characterModel.Refresh();
            RefreshHonorTitle();
            RefreshProfession();
        }

        private void SetCharacterModelOnly(bool onlyModel)
        {
            const string modelPath = "/CharacterPanel/BgPanel1/Content/TopGo/MyPlayerModelContent/PlayerModelLayout/PlayerModelRoot";
            foreach (KeyValuePair<string, RectTransform> pair in nodes)
            {
                bool belongsToModel = pair.Key.StartsWith(modelPath, StringComparison.Ordinal)
                    || modelPath.StartsWith(pair.Key + "/", StringComparison.Ordinal);
                if (pair.Key == CharacterScreenPath) continue;
                pair.Value.gameObject.SetActive(onlyModel ? belongsToModel
                    : nodeActive.TryGetValue(pair.Key, out bool active) && active);
            }
        }

        private OfficialCharacterPanelSet ReadScreens()
        {
            Dictionary<string, object> screens = ReadPanelScreens();
            return new OfficialCharacterPanelSet
            {
                character = ReadPanelData(screens, "character"),
                character_props = ReadPanelData(screens, "character_props"),
                rank_upgrade = ReadPanelData(screens, "rank_upgrade"),
                appearance_preview = ReadPanelData(screens, "appearance_preview")
            };
        }

        private Dictionary<string, object> ReadPanelScreens()
        {
            Dictionary<string, object> package = CharacterModelPackage.AsObject(
                OfficialEquipmentJson.Parse(assets.CharacterPanels.text), "character panel package");
            if (CharacterModelPackage.String(package, "manifest") != "854_156383"
                || !package.TryGetValue("screens", out object rawScreens))
                throw new FormatException("Pinned original character panel data are invalid.");
            return CharacterModelPackage.AsObject(rawScreens, "character panel screens");
        }

        private static OfficialCharacterPanelData ReadPanelData(
            Dictionary<string, object> screens, string key)
        {
            if (!screens.TryGetValue(key, out object rawPanel)) return null;
            return new OfficialCharacterPanelData
            {
                root = CharacterPanelNodeFactory.ReadRoot(
                    CharacterModelPackage.AsObject(rawPanel, "character panel " + key))
            };
        }

        private string OfficialRankName(int subRank)
        {
            Dictionary<string, object> item = RankRecord(subRank);
            return item == null ? string.Empty : Convert.ToString(item["name"], System.Globalization.CultureInfo.InvariantCulture);
        }

        private string OfficialSubRankName(int subRank)
        {
            Dictionary<string, object> item = RankRecord(subRank);
            return item == null ? string.Empty : Convert.ToString(item["sub"], System.Globalization.CultureInfo.InvariantCulture);
        }

        private Sprite OfficialRankSprite(int subRank)
        {
            Dictionary<string, object> item = RankRecord(subRank);
            if (item == null) return null;
            Dictionary<string, object> icon = (Dictionary<string, object>)item["icon"];
            return assets.FindSprite(Convert.ToString(icon["file"], System.Globalization.CultureInfo.InvariantCulture));
        }

        private Dictionary<string, object> RankRecord(int subRank)
        {
            Dictionary<string, object> root = (Dictionary<string, object>)OfficialEquipmentJson.Parse(assets.CharacterOverview.text);
            Dictionary<string, object> ranks = (Dictionary<string, object>)root["ranks"];
            return ranks.TryGetValue(subRank.ToString(System.Globalization.CultureInfo.InvariantCulture), out object value)
                ? value as Dictionary<string, object> : null;
        }

        private string OfficialSeasonLimitText()
        {
            Dictionary<string, object> root = (Dictionary<string, object>)OfficialEquipmentJson.Parse(assets.CharacterOverview.text);
            Dictionary<string, object> texts = (Dictionary<string, object>)root["texts"];
            Dictionary<string, object> seasons = (Dictionary<string, object>)root["season_names"];
            string rank = progression.Player.SeasonRank.ToString(System.Globalization.CultureInfo.InvariantCulture);
            string name = seasons.TryGetValue(rank, out object value)
                ? Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) : string.Empty;
            return Convert.ToString(texts["function_limit_season"], System.Globalization.CultureInfo.InvariantCulture)
                .Replace("{0}", name);
        }

        private float OfficialExperienceAnimationDuration()
        {
            Dictionary<string, object> root = (Dictionary<string, object>)OfficialEquipmentJson.Parse(assets.CharacterOverview.text);
            Dictionary<string, object> bindings = (Dictionary<string, object>)root["bindings"];
            Dictionary<string, object> experience = (Dictionary<string, object>)bindings["PlayerExpSlider"];
            Dictionary<string, object> fields = (Dictionary<string, object>)experience["fields"];
            float duration = Convert.ToSingle(fields["mExpSliderPlayTime"], System.Globalization.CultureInfo.InvariantCulture);
            if (duration <= 0f || float.IsNaN(duration) || float.IsInfinity(duration))
                throw new FormatException("Original character experience animation duration is invalid.");
            return duration;
        }

        private void BindButton(string path, Action action)
        {
            if (buttons.TryGetValue(path, out Button button)) button.onClick.AddListener(() => action());
        }

        private void BindDialogButton(string path, Action action)
        {
            if (dialogButtons.TryGetValue(path, out Button button)) button.onClick.AddListener(() => action());
        }

        private T ComponentAt<T>(string path) where T : Component
        {
            return nodes.TryGetValue(path, out RectTransform rect) ? rect.GetComponent<T>() : null;
        }

        private Component TextAt(string path)
        {
            return nodes.TryGetValue(path, out RectTransform rect) ? FindLabel(rect) : null;
        }

        private static Component FindLabel(Transform root)
        {
            if (root == null) return null;
            TMP_Text tmp = root.GetComponent<TMP_Text>() ?? root.GetComponentInChildren<TMP_Text>(true);
            return tmp != null ? (Component)tmp : root.GetComponent<Text>() ?? root.GetComponentInChildren<Text>(true);
        }

        private static void SetText(Component label, string value)
        {
            if (label is TMP_Text tmp) tmp.text = value ?? string.Empty;
            else if (label is Text text) text.text = value ?? string.Empty;
        }

        private static long Interpolate(long from, long to, float ratio)
        {
            double value = from + ((double)to - from) * ratio;
            return checked((long)Math.Round(value, MidpointRounding.AwayFromZero));
        }

        private static void EnsureEventSystem()
        {
            EventSystem eventSystem = FindObjectOfType<EventSystem>();
            if (eventSystem == null)
            {
                var eventSystemObject = new GameObject("EventSystem", typeof(EventSystem));
                DontDestroyOnLoad(eventSystemObject);
                eventSystem = eventSystemObject.GetComponent<EventSystem>();
            }
            BaseInputModule inputModule = eventSystem.GetComponent<BaseInputModule>();
            if (inputModule == null)
                inputModule = eventSystem.gameObject.AddComponent<StandaloneInputModule>();
            if (!inputModule.enabled) inputModule.enabled = true;
        }
    }
}
