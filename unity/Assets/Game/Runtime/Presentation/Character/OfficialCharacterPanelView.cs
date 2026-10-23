using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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
        private readonly Dictionary<string, RectTransform> nodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
        private readonly Dictionary<string, bool> nodeActive = new Dictionary<string, bool>(StringComparer.Ordinal);
        private readonly Dictionary<string, Button> buttons = new Dictionary<string, Button>(StringComparer.Ordinal);
        private readonly Dictionary<string, RectTransform> dialogNodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
        private readonly Dictionary<string, Button> dialogButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
        private OfficialCharacterAssetCatalog assets;
        private OfflineCharacterProgressionService progression;
        private CharacterPanelNodeFactory factory;
        private Slider experienceSlider;
        private Text experienceText;
        private Text levelText;
        private Text levelLimitText;
        private Text rankText;
        private Text subRankText;
        private Image rankImage;
        private GameObject activeDialog;
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
        private Text honorTimeText;
        private GameObject honorTimeContent;
        private Text honorPropText;
        private GameObject honorPropContent;
        private long honorTitleExpiry;
        private float honorClock;

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
            if (skillPanel != null) skillPanel.Close();
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
            OfflineCharacterProgressionService characterProgression)
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
            assets.ValidateBindings();
            CharacterPanelNodeFactory.SetUiFont(assets.UiFont);
            factory = new CharacterPanelNodeFactory(assets);

            OfficialCharacterPanelsData panelData = JsonUtility.FromJson<OfficialCharacterPanelsData>(assets.CharacterPanels.text);
            if (panelData == null || panelData.manifest != "854_156383" || panelData.screens == null
                || panelData.screens.character == null || panelData.screens.character.root == null)
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
                scaler.matchWidthOrHeight = 0.5f;
            }
            if (GetComponent<GraphicRaycaster>() == null) gameObject.AddComponent<GraphicRaycaster>();
            EnsureEventSystem();

            RectTransform root = gameObject.GetComponent<RectTransform>();
            if (root == null) root = gameObject.AddComponent<RectTransform>();
            root.anchorMin = Vector2.zero;
            root.anchorMax = Vector2.one;
            root.offsetMin = Vector2.zero;
            root.offsetMax = Vector2.zero;
            characterRoot = panelData.screens.character.root;
            factory.Build(characterRoot, root, nodes, buttons);
            RecordActive(characterRoot);
            BindOfficialTargets();
            InitializeCharacterModel();
            InitializePropPanels();
            Refresh();
        }

        private void RecordActive(OfficialCharacterPanelNode node)
        {
            if (node == null) return;
            nodeActive[node.path] = node.active;
            foreach (OfficialCharacterPanelNode child in node.children ?? Array.Empty<OfficialCharacterPanelNode>())
                RecordActive(child);
        }

        public void Refresh()
        {
            if (progression == null) return;
            Refresh(progression.Experience());
            if (propPanels != null) propPanels.RefreshSummary(transform);
            if (equipSlots != null) equipSlots.Refresh(transform);
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
                    experienceText.text = OfficialSeasonLimitText();
                }
                else
                {
                    string amount = OfficialPropFormat.Compact(experience.CurrentExperience);
                    if (experience.IsStoredExperience) amount = "<color=#e17126>" + amount + "</color>";
                    experienceText.text = amount + "/" + OfficialPropFormat.Compact(experience.RequiredExperience);
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
                () => OfficialModuleRequested?.Invoke("pet"));
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
            skillPanel.Changed += Refresh;
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
            Text text = button.GetComponentsInChildren<Text>(true)
                .FirstOrDefault(value => value.name == "Text");
            if (text != null)
            {
                string professionName = progression.Catalog.ProfessionName(profession);
                text.text = string.IsNullOrEmpty(professionName)
                    ? "\u804c\u4e1a " + profession : professionName;
            }
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
                honorPropText.text = string.Join("\u3001", parts);
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
                honorTimeText.text = string.Empty;
                if (honorTimeContent != null) honorTimeContent.SetActive(false);
                return;
            }
            long days = remaining / 86400;
            long hours = remaining % 86400 / 3600;
            long minutes = remaining % 3600 / 60;
            long seconds = remaining % 60;
            honorTimeText.text = days > 0 ? days + "\u5929" + hours + "\u5c0f\u65f6"
                : hours > 0 ? hours + "\u5c0f\u65f6" + minutes + "\u5206\u949f"
                : minutes > 0 ? minutes + "\u5206\u949f" + seconds + "\u79d2" : seconds + "\u79d2";
        }

        private Text FindTextByPath(string suffix)
        {
            return nodes.Where(pair => pair.Key.EndsWith(suffix, StringComparison.Ordinal))
                .Select(pair => pair.Value.GetComponent<Text>()).FirstOrDefault(value => value != null);
        }

        private void InitializeCharacterModel()
        {
            const string modelPath = "/CharacterPanel/BgPanel1/Content/TopGo/MyPlayerModelContent/PlayerModelLayout/PlayerModelRoot";
            if (!nodes.TryGetValue(modelPath, out RectTransform mount))
                throw new FormatException("The official character page has no PlayerModelRoot mount.");
            characterModel = mount.GetComponent<CharacterModelPreview>();
            if (characterModel == null) characterModel = mount.gameObject.AddComponent<CharacterModelPreview>();
            try
            {
                characterModel.Initialize(progression);
            }
            catch (Exception error)
            {
                Debug.LogWarning("Character model preview is unavailable: " + error.Message);
                characterModel = null;
            }
            Text name = FindText("CharacterName") ?? FindText("PlayerName") ?? FindText("NameText");
            SetText(name, progression.Player.CharacterName);
        }

        private Text FindText(string nameFragment)
        {
            return nodes.Where(pair => pair.Key.IndexOf(nameFragment, StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(pair => pair.Value.GetComponent<Text>()).FirstOrDefault(value => value != null);
        }

        private void OpenWardrobe()
        {
            AppearanceRequested?.Invoke();
            if (wardrobeView != null) return;
            SetCharacterModelOnly(true);
            GameObject overlay = new GameObject("OfficialWardrobeOverlay", typeof(RectTransform));
            overlay.transform.SetParent(transform, false);
            RectTransform rect = (RectTransform)overlay.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            wardrobeView = overlay.AddComponent<OfficialWardrobePanelView>();
            wardrobeView.Initialize(assets, progression, characterModel, CloseWardrobe);
        }

        private void CloseWardrobe()
        {
            if (wardrobeView != null) Destroy(wardrobeView.gameObject);
            wardrobeView = null;
            SetCharacterModelOnly(false);
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
            OfficialCharacterPanelsData data = JsonUtility.FromJson<OfficialCharacterPanelsData>(assets.CharacterPanels.text);
            if (data == null || data.screens == null) throw new FormatException("Original character panel set is invalid.");
            return data.screens;
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

        private Text TextAt(string path)
        {
            return ComponentAt<Text>(path);
        }

        private static void SetText(Text text, string value)
        {
            if (text != null) text.text = value ?? string.Empty;
        }

        private static long Interpolate(long from, long to, float ratio)
        {
            double value = from + ((double)to - from) * ratio;
            return checked((long)Math.Round(value, MidpointRounding.AwayFromZero));
        }

        private static void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) return;
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(eventSystem);
        }
    }
}
