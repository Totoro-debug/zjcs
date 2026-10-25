using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using Zjcs.Offline.Equipment;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;

namespace Zjcs.Presentation.Character
{
    [DisallowMultipleComponent]
    public sealed class OfficialSkillAppearancePanelView : MonoBehaviour
    {
        private sealed class SkinRow
        {
            public int ItemId;
            public int SkillId;
            public int EffectSkillId;
            public int ItemType;
        }

        private sealed class SkinCard
        {
            public int ItemId;
            public int SkillId;
            public GameObject Root;
            public GameObject Selected;
            public GameObject Equipped;
            public GameObject NewMark;
        }

        private readonly Dictionary<string, RectTransform> nodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
        private readonly Dictionary<string, Button> buttons = new Dictionary<string, Button>(StringComparer.Ordinal);
        private readonly Dictionary<int, GameObject> tabSelectedMarkers = new Dictionary<int, GameObject>();
        private readonly List<SkinRow> allSkins = new List<SkinRow>();
        private readonly List<SkinCard> cards = new List<SkinCard>();

        private OfficialCharacterAssetCatalog assets;
        private OfflineCharacterProgressionService progression;
        private Dictionary<string, object> texts;
        private Dictionary<string, object> gridItemTemplate;
        private Dictionary<string, object> skillIconTemplate;
        private Dictionary<string, object> wearingAttachmentTemplate;
        private Dictionary<string, object> newAttachmentTemplate;
        private Dictionary<string, object> gridLayoutData;
        private string skillIconSlotPath;
        private string skillIconSelectedPath;
        private RectTransform contentRoot;
        private RectTransform videoRoot;
        private RectTransform grid;
        private RectTransform viewport;
        private GridLayoutGroup gridLayout;
        private ScrollRect scroll;
        private RawImage videoImage;
        private VideoPlayer videoPlayer;
        private RenderTexture videoTexture;
        private Text emptyText;
        private GameObject emptyState;
        private GameObject videoControls;
        private Button takeInButton;
        private Button takeOffButton;
        private int itemType = 100;
        private int selectedItemId;
        private bool visible;

        public void Initialize(OfficialCharacterAssetCatalog assetCatalog,
            OfflineCharacterProgressionService characterProgression, Transform contentParent, Transform videoParent)
        {
            assets = assetCatalog != null ? assetCatalog : throw new ArgumentNullException(nameof(assetCatalog));
            progression = characterProgression != null ? characterProgression
                : throw new ArgumentNullException(nameof(characterProgression));
            if (contentParent == null || videoParent == null)
                throw new ArgumentNullException(contentParent == null ? nameof(contentParent) : nameof(videoParent));

            TextAsset ui = Resources.Load<TextAsset>("Character/skill-appearance-ui");
            if (ui == null) throw new FileNotFoundException("The pinned skill appearance prefab data is missing.");
            Dictionary<string, object> data = CharacterModelPackage.AsObject(
                OfficialEquipmentJson.Parse(ui.text), "official skill appearance UI");
            if (!string.Equals(CharacterModelPackage.String(data, "manifest"), "854_156383", StringComparison.Ordinal))
                throw new FormatException("The skill appearance UI is not pinned to the original 854_156383 package.");
            texts = CharacterModelPackage.AsObject(data["texts"], "skill appearance text");
            gridItemTemplate = CharacterModelPackage.AsObject(data["grid_item"], "official skill appearance grid item");
            skillIconTemplate = CharacterModelPackage.AsObject(data["skill_icon"], "official skill item icon");
            wearingAttachmentTemplate = CharacterModelPackage.AsObject(
                data["skill_wearing_attachment"], "official skill appearance wearing attachment");
            newAttachmentTemplate = CharacterModelPackage.AsObject(
                data["skill_new_attachment"], "official new item attachment");
            skillIconSlotPath = CharacterModelPackage.String(data, "skill_icon_slot_path");
            skillIconSelectedPath = CharacterModelPackage.String(data, "skill_icon_selected_path");
            foreach (object raw in CharacterModelPackage.AsArray(data["skill_skins"], "skill appearance rows"))
            {
                Dictionary<string, object> row = CharacterModelPackage.AsObject(raw, "skill appearance row");
                allSkins.Add(new SkinRow
                {
                    ItemId = Integer(row, "item_id"),
                    SkillId = Integer(row, "skill_id"),
                    EffectSkillId = Integer(row, "effect_skill_id"),
                    ItemType = Integer(row, "skin_type")
                });
            }
            if (allSkins.Count == 0) throw new FormatException("The official skill appearance table is empty.");

            var factory = new CharacterPanelNodeFactory(assets);
            Dictionary<string, object> rootData = CharacterModelPackage.AsObject(data["root"], "skill appearance root");
            gridLayoutData = FindNodeData(rootData, "/AppearanceSkillContent/ScrollRect/Viewport/Grid");
            contentRoot = factory.Build(CharacterPanelNodeFactory.Read(rootData), contentParent, nodes, buttons);
            Dictionary<string, object> videoData = CharacterModelPackage.AsObject(data["video_root"], "skill appearance video root");
            videoRoot = factory.Build(CharacterPanelNodeFactory.Read(videoData), videoParent, nodes, buttons);

            Transform videoActionGroup = FindByName(videoRoot, "BtnGroup");
            if (videoActionGroup != null) videoActionGroup.gameObject.SetActive(false);
            takeInButton = ButtonAt("/AppearanceSkillContent/BtnGroup/TakeInBtn");
            takeOffButton = ButtonAt("/AppearanceSkillContent/BtnGroup/TakeOffBtn");
            if (takeInButton != null) takeInButton.onClick.AddListener(WearSelected);
            if (takeOffButton != null) takeOffButton.onClick.AddListener(TakeOffSelected);

            BindTabs(CharacterModelPackage.AsArray(data["tabs"], "skill appearance tabs"));
            ConfigureGrid();
            ConfigureVideo();
            SetVisible(false);
            RefreshList(true);
        }

        public void SetVisible(bool show)
        {
            visible = show;
            if (contentRoot != null) contentRoot.gameObject.SetActive(show);
            if (videoRoot != null) videoRoot.gameObject.SetActive(show);
            if (!show) StopVideo();
            else
            {
                RefreshState();
                SkinRow selected = SelectedSkin();
                if (selected != null) PlayVideo(selected.EffectSkillId);
            }
        }

        public void Refresh()
        {
            RefreshList(false);
        }

        private void OnEnable()
        {
            SkinRow selected = SelectedSkin();
            if (visible && selected != null) PlayVideo(selected.EffectSkillId);
        }

        private void OnDestroy()
        {
            if (videoPlayer != null)
            {
                videoPlayer.prepareCompleted -= OnVideoPrepared;
                videoPlayer.errorReceived -= OnVideoError;
                videoPlayer.Stop();
                videoPlayer.targetTexture = null;
            }
            if (videoTexture != null)
            {
                videoTexture.Release();
                Destroy(videoTexture);
                videoTexture = null;
            }
        }

        private void BindTabs(List<object> tabData)
        {
            foreach (object raw in tabData)
            {
                Dictionary<string, object> tab = CharacterModelPackage.AsObject(raw, "skill appearance tab");
                string path = CharacterModelPackage.String(tab, "path");
                int type = Integer(tab, "item_type");
                if (!buttons.TryGetValue(path, out Button button))
                    throw new FormatException("The original skill appearance tab is missing: " + path);
                if (nodes.TryGetValue(path, out RectTransform tabNode))
                {
                    Transform selected = tabNode.Find("Selected");
                    if (selected != null) tabSelectedMarkers[type] = selected.gameObject;
                }
                button.onClick.AddListener(() => SelectItemType(type));
            }
            RefreshTabSelection();
        }

        private void ConfigureGrid()
        {
            viewport = NodeEnding("/ScrollRect/Viewport");
            grid = NodeEnding("/ScrollRect/Viewport/Grid");
            RectTransform scrollRoot = NodeEnding("/ScrollRect");
            if (viewport == null || grid == null || scrollRoot == null)
                throw new FormatException("The official skill appearance scroll list is incomplete.");
            scroll = scrollRoot.GetComponent<ScrollRect>();
            if (scroll == null) scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            gridLayout = grid.GetComponent<GridLayoutGroup>();
            if (gridLayout == null) gridLayout = grid.gameObject.AddComponent<GridLayoutGroup>();
            Dictionary<string, object> cellRoot = CharacterModelPackage.AsObject(
                gridItemTemplate["root"], "original skill appearance cell root");
            List<object> cellSize = CharacterModelPackage.AsArray(cellRoot["size_delta"],
                "original skill appearance cell size");
            gridLayout.cellSize = new Vector2(
                Convert.ToSingle(cellSize[0], CultureInfo.InvariantCulture),
                Convert.ToSingle(cellSize[1], CultureInfo.InvariantCulture));
            if (gridLayoutData == null || !gridLayoutData.TryGetValue("grid_layout", out object rawGridLayout))
                throw new FormatException("The original LoopScrollGridLayoutGroup fields are missing.");
            Dictionary<string, object> originalGridLayout = CharacterModelPackage.AsObject(
                rawGridLayout, "original skill appearance grid settings");
            bool flexible = Convert.ToBoolean(originalGridLayout["flexible"], CultureInfo.InvariantCulture);
            List<object> minSpacing = CharacterModelPackage.AsArray(
                originalGridLayout["min_spacing"], "original skill appearance minimum spacing");
            gridLayout.spacing = new Vector2(
                Convert.ToSingle(minSpacing[0], CultureInfo.InvariantCulture),
                Convert.ToSingle(minSpacing[1], CultureInfo.InvariantCulture));
            gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
            gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
            gridLayout.childAlignment = TextAnchor.UpperLeft;
            gridLayout.constraint = flexible
                ? GridLayoutGroup.Constraint.Flexible : GridLayoutGroup.Constraint.FixedColumnCount;
            gridLayout.constraintCount = Mathf.Max(1, Integer(originalGridLayout, "minimum", 1));
            grid.anchorMin = new Vector2(0f, 1f);
            grid.anchorMax = new Vector2(1f, 1f);
            grid.pivot = new Vector2(0.5f, 1f);
            grid.anchoredPosition = Vector2.zero;
            scroll.viewport = viewport;
            scroll.content = grid;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 36f;
        }

        private void ConfigureVideo()
        {
            Transform screen = FindByName(videoRoot, "Screen");
            Transform content = FindByName(videoRoot, "VideoContent");
            emptyState = FindByName(videoRoot, "FrontImg")?.gameObject;
            Transform empty = FindByName(videoRoot, "NullText");
            emptyText = empty == null ? null : empty.GetComponent<Text>();
            Transform videoControl = FindByName(videoRoot, "CommonBtn_PlayVideo");
            videoControls = videoControl == null ? null : videoControl.gameObject;
            if (screen == null || content == null || emptyState == null)
                throw new FormatException("The original skill appearance video and empty state are missing.");

            videoImage = screen.GetComponent<RawImage>();
            if (videoImage == null) videoImage = screen.gameObject.AddComponent<RawImage>();
            videoImage.raycastTarget = false;
            videoPlayer = screen.GetComponent<VideoPlayer>();
            if (videoPlayer == null) videoPlayer = screen.gameObject.AddComponent<VideoPlayer>();
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = true;
            videoPlayer.skipOnDrop = true;
            videoPlayer.waitForFirstFrame = true;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;
            videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            videoTexture = new RenderTexture(852, 480, 0, RenderTextureFormat.ARGB32);
            videoTexture.Create();
            videoImage.texture = videoTexture;
            videoPlayer.targetTexture = videoTexture;
            videoPlayer.prepareCompleted += OnVideoPrepared;
            videoPlayer.errorReceived += OnVideoError;
            if (videoControl != null)
            {
                Button play = videoControl.GetComponent<Button>();
                if (play != null) play.onClick.AddListener(ToggleVideo);
            }
        }

        private void SelectItemType(int nextType)
        {
            if (nextType != 100 && nextType != 101) return;
            if (itemType == nextType) return;
            itemType = nextType;
            selectedItemId = 0;
            RefreshTabSelection();
            RefreshList(true);
        }

        private void RefreshTabSelection()
        {
            foreach (KeyValuePair<int, GameObject> entry in tabSelectedMarkers)
                if (entry.Value != null) entry.Value.SetActive(entry.Key == itemType);
        }

        private void RefreshList(bool resetScroll)
        {
            if (progression == null || grid == null) return;
            int[] owned = progression.Player.Inventory.Where(value => value != null && value.Count > 0)
                .Select(value => value.ItemId).ToArray();
            var ownedSet = new HashSet<int>(owned);
            List<SkinRow> visibleSkins = new List<SkinRow>();
            var byItem = allSkins.ToDictionary(value => value.ItemId);
            foreach (int itemId in owned)
            {
                if (!byItem.TryGetValue(itemId, out SkinRow skin) || skin.ItemType == 0
                    || progression.Catalog.ItemType(skin.SkillId) != itemType) continue;
                visibleSkins.Add(skin);
            }
            if (!ownedSet.Contains(selectedItemId)) selectedItemId = 0;
            ClearCards();
            foreach (SkinRow skin in visibleSkins) CreateCard(skin);
            if (resetScroll && scroll != null) scroll.verticalNormalizedPosition = 1f;
            RefreshState();
        }

        private void CreateCard(SkinRow skin)
        {
            Dictionary<string, object> itemRoot = CharacterModelPackage.AsObject(gridItemTemplate["root"],
                "official skill appearance grid item root");
            OfficialCharacterPanelNode node = CharacterPanelNodeFactory.Read(itemRoot);
            var cardNodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            var cardButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
            var factory = new CharacterPanelNodeFactory(assets);
            RectTransform cardRoot = factory.Build(node, grid, cardNodes, cardButtons);
            cardRoot.name = "SkillAppearanceItem_" + skin.ItemId.ToString(CultureInfo.InvariantCulture);
            if (!cardNodes.TryGetValue(skillIconSlotPath, out RectTransform slot))
                throw new FormatException("The original SkillSkinGridItem slot path is missing: " + skillIconSlotPath);

            var iconNodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            var iconButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
            RectTransform iconRoot = factory.Build(CharacterPanelNodeFactory.Read(skillIconTemplate),
                slot, iconNodes, iconButtons);
            iconRoot.name = "ItemIconSkill_" + skin.ItemId.ToString(CultureInfo.InvariantCulture);
            Button select = iconRoot.GetComponent<Button>();
            if (select == null) throw new FormatException("The original ItemIconSkill prefab has no button.");
            select.onClick.AddListener(() => SelectSkin(skin.ItemId));

            if (!iconNodes.TryGetValue("/ItemIconSkill/Content/mask/icon", out RectTransform iconNode))
                throw new FormatException("The original ItemIconSkill icon image is missing.");
            Image icon = iconNode.GetComponent<Image>();
            if (icon == null) throw new FormatException("The original ItemIconSkill icon component is missing.");
            icon.sprite = assets.FindSkillSkinSprite(progression.Catalog, skin.ItemId);
            if (icon.sprite == null)
                throw new InvalidOperationException("The official skill appearance icon is missing for item " + skin.ItemId + ".");

            Dictionary<string, object> item = progression.Catalog.FindItem(skin.ItemId);
            string itemName = item != null && item.TryGetValue("Name", out object rawName)
                ? Convert.ToString(rawName, CultureInfo.InvariantCulture) : string.Empty;
            RectTransform labelNode = iconNodes.TryGetValue("/ItemIconSkill/Content/BottomText", out RectTransform nameNode)
                ? nameNode : null;
            Text label = labelNode == null ? null : labelNode.GetComponent<Text>();
            if (label != null)
            {
                label.text = itemName;
                label.gameObject.SetActive(true);
            }

            RectTransform qualityNode = iconNodes.TryGetValue("/ItemIconSkill/Content/QualityImg", out RectTransform frameNode)
                ? frameNode : null;
            Image qualityImage = qualityNode == null ? null : qualityNode.GetComponent<Image>();
            if (qualityImage != null && item != null)
            {
                int quality = Convert.ToInt32(item["Quality"], CultureInfo.InvariantCulture);
                qualityImage.sprite = assets.FindQualityBackgroundSprite(quality);
            }

            if (!iconNodes.TryGetValue(skillIconSelectedPath, out RectTransform selectedNode))
                throw new FormatException("The original ItemIconSkill selected-object path is missing: "
                    + skillIconSelectedPath);
            RectTransform attachmentNode = iconNodes.TryGetValue(
                "/ItemIconSkill/Content/ItemIconAttachment", out RectTransform attachment)
                ? attachment : throw new FormatException("The original ItemIconSkill attachment slot is missing.");
            GameObject equipped = BuildAttachment(factory, wearingAttachmentTemplate, attachmentNode);
            GameObject newMark = BuildAttachment(factory, newAttachmentTemplate, attachmentNode);
            cards.Add(new SkinCard
            {
                ItemId = skin.ItemId,
                SkillId = skin.SkillId,
                Root = cardRoot.gameObject,
                Selected = selectedNode.gameObject,
                Equipped = equipped,
                NewMark = newMark
            });
        }

        private static GameObject BuildAttachment(CharacterPanelNodeFactory factory,
            Dictionary<string, object> template, Transform parent)
        {
            var attachmentNodes = new Dictionary<string, RectTransform>(StringComparer.Ordinal);
            var attachmentButtons = new Dictionary<string, Button>(StringComparer.Ordinal);
            return factory.Build(CharacterPanelNodeFactory.Read(template), parent,
                attachmentNodes, attachmentButtons).gameObject;
        }

        private void SelectSkin(int itemId)
        {
            if (progression.IsWardrobeItemNew(itemId)) progression.MarkWardrobeItemSeen(itemId);
            if (selectedItemId == itemId) return;
            selectedItemId = itemId;
            RefreshState();
            SkinRow skin = SelectedSkin();
            if (skin == null) return;
            PlayVideo(skin.EffectSkillId);
        }

        private void WearSelected()
        {
            SkinRow skin = SelectedSkin();
            if (skin == null) return;
            ProgressionResult result = progression.WearSkillSkin(skin.ItemId,
                EventId("wear", skin.ItemId));
            if (result.Status == ProgressionResultStatus.InvalidState)
            {
                ShowStatus(result.Message ?? "尚未拥有该技能的原始技能。");
                return;
            }
            RefreshList(false);
        }

        private void TakeOffSelected()
        {
            SkinRow skin = SelectedSkin();
            if (skin != null) TakeOff(skin.SkillId);
        }

        private void TakeOff(int skillId)
        {
            ProgressionResult result = progression.TakeOffSkillSkin(skillId,
                EventId("takeoff", skillId));
            if (result.Status == ProgressionResultStatus.InvalidState)
            {
                ShowStatus(result.Message ?? "该技能当前没有装备幻彩。");
                return;
            }
            RefreshList(false);
        }

        private void RefreshState()
        {
            SkinRow selected = SelectedSkin();
            bool equipped = selected != null
                && progression.EquippedSkillSkinId(selected.SkillId) == selected.ItemId;
            if (takeInButton != null) takeInButton.gameObject.SetActive(selected != null && !equipped);
            if (takeOffButton != null) takeOffButton.gameObject.SetActive(selected != null && equipped);
            foreach (SkinCard card in cards)
            {
                bool wearing = progression.EquippedSkillSkinId(card.SkillId) == card.ItemId;
                if (card.Selected != null) card.Selected.SetActive(card.ItemId == selectedItemId);
                if (card.Equipped != null) card.Equipped.SetActive(wearing);
                if (card.NewMark != null) card.NewMark.SetActive(progression.IsWardrobeItemNew(card.ItemId));
            }
            if (selected == null)
            {
                StopVideo();
                if (emptyState != null) emptyState.SetActive(true);
                if (emptyText != null) emptyText.text = TextValue("ui_skill_skin_unselect", "");
            }
            else if (emptyState != null)
            {
                emptyState.SetActive(false);
            }
        }

        private void PlayVideo(int effectSkillId)
        {
            if (videoPlayer == null || videoImage == null) return;
            string path = Path.Combine(Application.streamingAssetsPath, "Character", "SkillSkinVideos",
                effectSkillId.ToString(CultureInfo.InvariantCulture) + ".mp4");
            if (!File.Exists(path))
            {
                ShowStatus("官方技能演示视频缺失：" + effectSkillId.ToString(CultureInfo.InvariantCulture));
                return;
            }
            videoPlayer.Stop();
            videoImage.enabled = true;
            videoPlayer.url = new Uri(path).AbsoluteUri;
            videoPlayer.Prepare();
            if (videoControls != null) videoControls.SetActive(true);
        }

        private void OnVideoPrepared(VideoPlayer player)
        {
            if (player == videoPlayer && visible && SelectedSkin() != null) player.Play();
        }

        private void OnVideoError(VideoPlayer player, string message)
        {
            if (player != videoPlayer) return;
            Debug.LogWarning("Official skill appearance video failed: " + message);
            ShowStatus("技能演示视频无法播放");
        }

        private void ToggleVideo()
        {
            if (videoPlayer == null || !videoPlayer.isPrepared) return;
            if (videoPlayer.isPlaying) videoPlayer.Pause();
            else videoPlayer.Play();
        }

        private void StopVideo()
        {
            if (videoPlayer != null && videoPlayer.isPlaying) videoPlayer.Stop();
            if (videoImage != null) videoImage.enabled = false;
            if (videoControls != null) videoControls.SetActive(false);
        }

        private void ShowStatus(string message)
        {
            if (emptyText == null) return;
            if (emptyState != null) emptyState.SetActive(true);
            emptyText.text = message;
        }

        private SkinRow SelectedSkin()
        {
            return allSkins.FirstOrDefault(value => value.ItemId == selectedItemId);
        }

        private void ClearCards()
        {
            foreach (SkinCard card in cards)
                if (card.Root != null) Destroy(card.Root);
            cards.Clear();
        }

        private Button ButtonAt(string path)
        {
            return buttons.TryGetValue(path, out Button button) ? button : null;
        }

        private RectTransform NodeEnding(string suffix)
        {
            return nodes.Where(pair => pair.Key.EndsWith(suffix, StringComparison.Ordinal))
                .Select(pair => pair.Value).FirstOrDefault();
        }

        private static RectTransform FindNode(Dictionary<string, RectTransform> source, string suffix)
        {
            return source.Where(pair => pair.Key.EndsWith(suffix, StringComparison.Ordinal))
                .Select(pair => pair.Value).FirstOrDefault();
        }

        private static Dictionary<string, object> FindNodeData(Dictionary<string, object> root, string path)
        {
            if (string.Equals(CharacterModelPackage.String(root, "path"), path, StringComparison.Ordinal))
                return root;
            if (!root.TryGetValue("children", out object rawChildren) || !(rawChildren is List<object> children))
                return null;
            foreach (object rawChild in children)
            {
                Dictionary<string, object> child = CharacterModelPackage.AsObject(rawChild,
                    "skill appearance layout node");
                Dictionary<string, object> found = FindNodeData(child, path);
                if (found != null) return found;
            }
            return null;
        }

        private static Transform FindByName(Transform root, string name)
        {
            if (root == null) return null;
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = FindByName(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private string TextValue(string key, string fallback)
        {
            return texts != null && texts.TryGetValue(key, out object value)
                ? Convert.ToString(value, CultureInfo.InvariantCulture) : fallback;
        }

        private static int Integer(Dictionary<string, object> value, string key)
        {
            return Integer(value, key, 0);
        }

        private static int Integer(Dictionary<string, object> value, string key, int fallback)
        {
            return value.TryGetValue(key, out object raw) && raw != null
                ? Convert.ToInt32(raw, CultureInfo.InvariantCulture) : fallback;
        }

        private static string EventId(string operation, int id)
        {
            return "skill-appearance-" + operation + "-" + id.ToString(CultureInfo.InvariantCulture)
                + "-" + DateTimeOffset.UtcNow.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
        }
    }
}
