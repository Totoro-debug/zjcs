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

        public event Action CloseRequested;

        public event Action AppearanceRequested;
        public event Action PropertiesRequested;
        public event Action RankUpgradeRequested;
        public event Action StoredExperienceHelpRequested;
        public event Action<string> OfficialModuleRequested;

        private void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (propPanels != null && propPanels.HandleEscape()) return;
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
                    string amount = experience.CurrentExperience.ToString("N0");
                    if (experience.IsStoredExperience) amount = "<color=#e37d38>" + amount + "</color>";
                    experienceText.text = amount + "/" + experience.RequiredExperience.ToString("N0");
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
                () => { RankUpgradeRequested?.Invoke(); ShowOfficialDialog(ReadScreens().rank_upgrade,
                    "CharacterUpgradePanel", true); });
            BindButton("/CharacterPanel/BgPanel1/Content/TopGo/CharacterPanelPropPanel/Content/Slot/SlotProp",
                OpenProperties);
            BindButton("/CharacterPanel/BgPanel1/Content/TopGo/CharacterPanelPropPanel/Content/Slot/SlotLevel/ExpTitle/BtnGo/RuleBtn",
                () => StoredExperienceHelpRequested?.Invoke());
            BindButton("/CharacterPanel/BgPanel1/Content/TopGo/CharacterPanelPropPanel/Content/Slot/SlotOther/CharacterPanelProfessionBtn",
                () => OfficialModuleRequested?.Invoke("profession"));
            BindButton("/CharacterPanel/BgPanel1/Content/TopGo/CharacterPanelPropPanel/Content/Slot/SlotOther/CharacterPanelSkillBtn",
                () => OfficialModuleRequested?.Invoke("skills"));
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
        }

        private void InitializeCharacterModel()
        {
            const string modelPath = "/CharacterPanel/BgPanel1/Content/TopGo/MyPlayerModelContent/PlayerModelLayout/PlayerModelRoot";
            if (!nodes.TryGetValue(modelPath, out RectTransform mount))
                throw new FormatException("The official character page has no PlayerModelRoot mount.");
            characterModel = mount.GetComponent<CharacterModelPreview>();
            if (characterModel == null) characterModel = mount.gameObject.AddComponent<CharacterModelPreview>();
            characterModel.Initialize(progression);
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
