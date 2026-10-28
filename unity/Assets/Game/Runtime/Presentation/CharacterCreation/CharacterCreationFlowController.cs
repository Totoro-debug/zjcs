using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Zjcs.Offline.CharacterCreation;
using Zjcs.Offline.Home;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Presentation.Character;
using Zjcs.Presentation.Home;
using Zjcs.Presentation.Opening;
using Zjcs.Presentation.World;

namespace Zjcs.Presentation.CharacterCreation
{
    public sealed class CharacterCreationFlowController : MonoBehaviour
    {
        private const string SlotPrefix = "slot-";
        private const int StoryHomePlayerSortingLayer = 11;
        private static readonly string[] Tabs = { "整体", "头发", "脸部", "特征" };
        private CharacterOpeningFlow opening;
        private CharacterCreationOfflineService service;
        private CharacterCreationAssetCatalog assets;
        private CharacterCreationPerformancePlayer performancePlayer;
        private CharacterCreationPerformanceAssets performanceAssets;
        private TMP_FontAsset font;
        private AudioSource uiAudio;
        private Canvas canvas;
        private GameObject screen;
        private GameObject officialPanel;
        private GameObject activeConfirmation;
        private RectTransform controlsRoot;
        private Component status;
        private Component nameCounter;
        private string pendingStorageError;
        private Transform selectSexContent;
        private Transform diyContent;
        private Transform setNameContent;
        private string populatedOptionKey;
        private string populatedDyeKey;
        private readonly Dictionary<string, Transform> optionItems = new Dictionary<string, Transform>(StringComparer.Ordinal);
        private readonly Dictionary<int, Transform> dyeItems = new Dictionary<int, Transform>();
        private bool customColorOpen;
        private bool customColorControlsBound;
        private CharacterCreationDraft draft;
        private string slotId;
        private int dyeType;
        private int dyeColorId;
        private int dyeHue;
        private int dyeSaturation;
        private int dyeValue;
        private Coroutine pendingSave;
        private GameObject characterFeatureRoot;
        private CharacterFeatureRuntime characterRuntime;
        private Zjcs.Presentation.Home.HomeFeatureRuntime homeRuntime;
        private OfficialCharacterPanelView characterPanel;
        private CharacterCreationNativeModelPreview malePreview;
        private CharacterCreationNativeModelPreview femalePreview;
        private Transform maleModelNode;
        private Transform femaleModelNode;
        private HomeAssetCatalog firstStoryHomeAssets;
        private HomeSceneView firstStoryHomeScene;
        private GameObject firstStoryHomeActor;
        private CharacterCreationMeshFramePlayer firstStoryHomeActorAnimation;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void StartWithFirstScene()
        {
#if UNITY_EDITOR
            // Battle acceptance creates its own scene and isolated profile.
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_INPUT_AUTOMATION") == "1") return;
#endif
#if UNITY_EDITOR || DEVELOPMENT_BUILD || ZJCS_MAINLINE_MODULE_VALIDATION
            if (Array.IndexOf(Environment.GetCommandLineArgs(), OfficialMainlineMapModuleProbe.Argument) >= 0) return;
#endif
            if (FindObjectOfType<CharacterCreationFlowController>() != null) return;
            if (FindObjectOfType<Zjcs.Offline.Startup.OfflineStartupFlow>() != null) return;
            new GameObject("OfficialStartup").AddComponent<Zjcs.Offline.Startup.OfflineStartupFlow>().Begin(
                () => new GameObject("CharacterCreationFlow").AddComponent<CharacterCreationFlowController>());
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            if (FindObjectOfType<AudioListener>() == null)
                gameObject.AddComponent<AudioListener>();
            uiAudio = gameObject.AddComponent<AudioSource>();
            uiAudio.playOnAwake = false;
            uiAudio.spatialBlend = 0f;
            try
            {
                assets = CharacterCreationAssetCatalog.LoadDefault();
                service = CharacterCreationOfflineService.OpenDefault(SaveSelectionProbe.ProfileRoot);
                font = assets.OfficialFont as TMP_FontAsset;
                if (font == null) throw new InvalidOperationException("The fixed-version official TMP font is not bound.");
                EnsureEventSystem();
                CreateCanvas();
                opening = gameObject.AddComponent<CharacterOpeningFlow>();
                ShowSlots();
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                ShowStartupError(error.Message);
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) PersistDraft();
        }

        private void OnApplicationQuit()
        {
            PersistDraft();
        }

        private void OnDestroy()
        {
            DisposeFirstStoryHome();
        }

        private void EnsureEventSystem()
        {
            if (FindObjectOfType<EventSystem>() != null) return;
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            DontDestroyOnLoad(eventSystem);
        }

        private void CreateCanvas()
        {
            var root = new GameObject("CharacterCreationCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            DontDestroyOnLoad(root);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(720, 1280);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }

        private void ShowStartupError(string message)
        {
            if (canvas == null) CreateCanvas();
            ClearScreen();
            screen = CreateScreen("启动失败");
            AddText(screen.transform, message, 30, Color.white,
                new Vector2(0.1f, 0.35f), new Vector2(0.9f, 0.65f), TextAlignmentOptions.Center);
        }

        private void ShowSlots(string message = null)
        {
            if (!PersistDraft()) return;
            DisposeFirstStoryHome();
            ClearScreen();
            canvas.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1080, 1920);
            canvas.GetComponent<CanvasScaler>().screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            screen = new GameObject("Screen_选择存档", typeof(RectTransform));
            screen.transform.SetParent(canvas.transform, false);
            Place(screen.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            screen.AddComponent<OfficialSaveSelectionView>().Initialize(service, CreateSlot, OpenSlot,
                RecoverCorruptDraft, message);
        }

        private void BeginOpening()
        {
            ClearScreen();
            status = null;
            opening.Begin(slotId, ShowCreation, message => Debug.LogWarning("开场剧情: " + message, this), null, null,
                () => CharacterOpeningFlow.ForceSkipButtonForAcceptance);
        }

        private void CreateSlot()
        {
            try
            {
                if (!PersistDraft()) return;
                draft = null;
                slotId = service.CreateSlot();
                ProfileSelectionPreference.Write(SaveSelectionProbe.ProfileRoot, slotId);
                draft = service.OpenDraft(slotId);
                BeginOpening();
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                ShowSlots(error.Message);
            }
        }

        private void RecoverCorruptDraft(string id)
        {
            try
            {
                if (!PersistDraft()) return;
                slotId = id;
                draft = service.PreserveCorruptDraftAndRestart(id);
                BeginOpening();
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                ShowSlots(error.Message);
            }
        }

        private Action OpenSlot(string id)
        {
            return () =>
            {
                try
                {
                    if (!PersistDraft()) return;
                    draft = null;
                    slotId = id;
                    bool hasCharacter = service.HasCharacter(id);
                    ProfileSelectionPreference.Write(SaveSelectionProbe.ProfileRoot, slotId);
                    if (!hasCharacter)
                    {
                        draft = service.OpenDraft(id);
                        BeginOpening();
                    }
                    else if (service.GetInitialWeaponType(id) == 0)
                    {
                        ShowInitialWeapon();
                    }
                    else
                    {
                        ShowComplete("角色已创建：" + service.GetCharacterName(id));
                    }
                }
                catch (Exception error)
                {
                    Debug.LogException(error, this);
                    ShowSlots("存档无法读取，原文件已保留。请选择其他存档。");
                }
            };
        }

        private void ShowCreation()
        {
            ClearScreen();
            canvas.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1080, 1920);
            officialPanel = Instantiate(assets.CreatePlayerPanelPrefab, canvas.transform, false);
            officialPanel.name = "OfficialCreatePlayerPanel";
            ApplyOfficialFont(officialPanel);
            screen = officialPanel;
            RectTransform panelRect = officialPanel.GetComponent<RectTransform>();
            if (panelRect != null) Place(panelRect, Vector2.zero, Vector2.one);

            selectSexContent = RequireNode(officialPanel.transform, "SelectSexContent");
            diyContent = RequireNode(officialPanel.transform, "DIYContent");
            setNameContent = RequireNode(officialPanel.transform, "SetNameContent");
            BindOfficialButtons();
            InitializeNativePreviews();
            performanceAssets = CharacterCreationPerformanceAssets.LoadDefault();
            performancePlayer = officialPanel.AddComponent<CharacterCreationPerformancePlayer>();
            performancePlayer.Initialize(performanceAssets, ResetDIYContentTabGroup,
                PlayCreationRunAnimation, PlayCreationIdleAnimation);
            populatedOptionKey = null;
            populatedDyeKey = null;
            customColorControlsBound = false;
            optionItems.Clear();
            dyeItems.Clear();
            RenderDraftPage();
            performancePlayer.PlaySelection(draft.body_type == "female" ? "SelFemale" : "SelMale");
            performancePlayer.PlaySelectedGlow(draft.body_type);
            if (draft.page == "body")
                performancePlayer.PlayRoot(draft.body_type == "female" ? "Shape_Female" : "Shape");
            else
                performancePlayer.ApplyInitialPageState(draft.body_type, draft.page);
        }

        private void BindOfficialButtons()
        {
            foreach (Transform node in officialPanel.GetComponentsInChildren<Transform>(true))
            {
                switch (node.name)
                {
                    case "MaleBtn": EnsureButton(node.gameObject).onClick.AddListener(() => SelectBody("male")); break;
                    case "FemaleBtn": EnsureButton(node.gameObject).onClick.AddListener(() => SelectBody("female")); break;
                    case "NextBtn":
                        EnsureButton(node.gameObject).onClick.AddListener(() =>
                        {
                            PlayUiSound("Main_Cursor_Normal");
                            NextPage();
                        });
                        break;
                    case "BackBtn":
                    case "CloseBtn_dark":
                        EnsureButton(node.gameObject).onClick.AddListener(BackFromCreationPage);
                        break;
                    case "CommonBtn_2":
                        EnsureButton(node.gameObject).onClick.AddListener(SubmitCharacter);
                        break;
                    case "RandomNameBtn":
                        node.gameObject.SetActive(false);
                        break;
                    case "RandomSkinBtn":
                    case "RandomBtn":
                        EnsureButton(node.gameObject).onClick.AddListener(() =>
                        {
                            PlayUiSound("Main_Cursor_Normal");
                            ResetAppearance(true);
                        });
                        break;
                    case "DefaultSkinBtn":
                    case "DefaultBtn":
                    case "ResetBtn":
                        EnsureButton(node.gameObject).onClick.AddListener(() =>
                        {
                            PlayUiSound("Main_Cursor_Normal");
                            ConfirmDefaultAppearanceReset();
                        });
                        break;
                }
            }

            BindTabButtons(FindPath(diyContent, "BottomContent", "TabGroupGo", "TabGroup"),
                "BottomContent/TabGroupGo/TabGroup", Tabs, SelectTab);
            BindFieldTabButtons("WholeTabGroup", FieldsForTab("整体"));
            BindFieldTabButtons("HairTabGroup", FieldsForTab("头发"));
            BindFieldTabButtons("FaceTabGroup", FieldsForTab("脸部"));
            BindFieldTabButtons("RaceTabGroup", FieldsForTab("特征"));
        }

        private void RenderDraftPage(bool setPageVisibility = true)
        {
            bool bodyPage = draft.page == "body";
            bool appearancePage = draft.page == "appearance";
            SetLabel(FindText(FindDeep(officialPanel.transform, "Title")),
                assets.CreationPageTitle(bodyPage ? 0 : appearancePage ? 1 : 2));
            Transform roles = FindDeep(officialPanel.transform, "RoleContent");
            foreach (Button button in roles.GetComponentsInChildren<Button>(true))
            {
                button.interactable = bodyPage;
                if (button.targetGraphic != null) button.targetGraphic.raycastTarget = bodyPage;
            }
            if (setPageVisibility)
            {
                selectSexContent.gameObject.SetActive(bodyPage);
                diyContent.gameObject.SetActive(appearancePage);
                setNameContent.gameObject.SetActive(!bodyPage && !appearancePage);
            }
            if (draft.page == "body") RenderBodyPage();
            else if (draft.page == "appearance") RenderAppearancePage();
            else RenderNamePage();
            RefreshStatus();
        }

        private void RenderBodyPage()
        {
            SetOfficialSelected(FindDeep(selectSexContent, "MaleBtn"), draft.body_type == "male");
            SetOfficialSelected(FindDeep(selectSexContent, "FemaleBtn"), draft.body_type == "female");
        }

        private void RenderAppearancePage()
        {
            Transform appearanceContent = FindDeep(diyContent, "AppearanceContent");
            Transform tryOnTitle = FindPath(appearanceContent, "AppearanceContent", "TitleGo");
            SetLabel(FindText(tryOnTitle), "幻装试穿");
            Transform sourceGo = FindDeep(diyContent, "SourceGo");
            int sex = draft.body_type == "female" ? 1 : 0;
            CharacterAppearanceDraft appearance = ActiveAppearance();
            int selectedGroupId = appearance.appearance_group_id == 0
                ? assets.DefaultAppearanceGroupId(sex) : appearance.appearance_group_id;
            if (!assets.TryGetAppearanceGroup(sex, selectedGroupId, out CharacterCreationAppearanceGroupInfo selectedGroup)
                || selectedGroup.condition_id != 0)
                throw new InvalidOperationException("The selected official appearance group is unavailable.");
            SetLabel(FindText(sourceGo), selectedGroup.localized_label);
            RenderAppearanceGroups(FindPath(appearanceContent, "AppearanceContent", "IconGroup"), sex,
                selectedGroupId);

            SetTabSelection(FindPath(diyContent, "BottomContent", "TabGroupGo", "TabGroup"),
                Array.IndexOf(Tabs, draft.tab));
            string activeGroup = GroupForTab(draft.tab);
            Transform subTabRoot = FindDeep(diyContent, "SubTabGroupGo");
            foreach (string group in new[] { "WholeTabGroup", "HairTabGroup", "FaceTabGroup", "RaceTabGroup" })
            {
                Transform root = FindDeep(subTabRoot, group);
                if (root == null) throw new InvalidOperationException("Official character creation tab is missing: " + group);
                root.gameObject.SetActive(group == activeGroup);
            }
            SetTabSelection(FindPath(subTabRoot, activeGroup, "TabGroup"),
                Array.IndexOf(FieldsForTab(draft.tab), draft.field));

            Transform editRoot = RequireNode(diyContent, "EditGroupGo");
            Transform colorRoot = RequireNode(editRoot, "ColorGo");
            bool colorField = IsColorField(draft.field);
            colorRoot.gameObject.SetActive(colorField);
            foreach (string name in new[] { "HairTypeSelect", "EyeTypeSelect", "MouthTypeSelect", "EthnicityTypeSelect" })
            {
                Transform root = FindDeep(editRoot, name);
                if (root == null) throw new InvalidOperationException("Official character creation option view is missing: " + name);
                root.gameObject.SetActive(!colorField && name == OptionViewForField(draft.field));
            }
            if (colorField) RenderColorPicker(appearance, DyeType(draft.field), colorRoot);
            else RenderAppearanceOptions(appearance, draft.field, FindDeep(editRoot, OptionViewForField(draft.field)));
        }

        private void RenderAppearanceGroups(Transform groupRoot, int sex, int selectedGroupId)
        {
            if (groupRoot == null) throw new InvalidOperationException("Official appearance group container is missing.");
            RectTransform rootRect = groupRoot as RectTransform;
            if (rootRect == null) throw new InvalidOperationException("Official appearance group container has no RectTransform.");
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = new Vector2(8, 4);
            rootRect.offsetMax = new Vector2(-8, -28);
            ClearChildren(groupRoot);

            IReadOnlyList<CharacterCreationAppearanceGroupInfo> groups = assets.GetAppearanceGroups(sex);
            int visibleIndex = 0;
            foreach (CharacterCreationAppearanceGroupInfo group in groups)
            {
                if (group.condition_id != 0) continue;
                Sprite sprite = assets.FindAppearanceGroupIcon(group.group_id, sex);
                if (sprite == null) throw new InvalidOperationException("Official appearance group icon is missing: " + group.group_id);

                const int columns = 6;
                const float cell = 52f;
                const float gap = 8f;
                int row = visibleIndex / columns;
                int column = visibleIndex % columns;
                var item = new GameObject("AppearanceGroup_" + group.group_id,
                    typeof(RectTransform), typeof(Image), typeof(Button));
                item.transform.SetParent(groupRoot, false);
                RectTransform rect = item.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = new Vector2(0, 1);
                rect.pivot = new Vector2(0, 1);
                rect.sizeDelta = new Vector2(cell, cell);
                rect.anchoredPosition = new Vector2(column * (cell + gap), -row * (cell + gap));

                Image image = item.GetComponent<Image>();
                image.sprite = sprite;
                image.preserveAspect = true;
                image.raycastTarget = true;
                Button button = item.GetComponent<Button>();
                button.targetGraphic = image;
                ColorBlock colors = button.colors;
                colors.highlightedColor = Color.Lerp(Color.white, new Color32(241, 213, 153, 255), 0.42f);
                colors.pressedColor = Color.Lerp(Color.white, new Color32(194, 146, 67, 255), 0.45f);
                button.colors = colors;
                button.interactable = group.group_id != selectedGroupId;
                int groupId = group.group_id;
                button.onClick.AddListener(() => SelectAppearanceGroup(sex, groupId));

                var outline = new GameObject("Selected", typeof(RectTransform), typeof(Image));
                outline.transform.SetParent(item.transform, false);
                RectTransform outlineRect = outline.GetComponent<RectTransform>();
                Place(outlineRect, Vector2.zero, Vector2.one);
                outlineRect.offsetMin = new Vector2(-3, -3);
                outlineRect.offsetMax = new Vector2(3, 3);
                Image outlineImage = outline.GetComponent<Image>();
                outlineImage.color = new Color32(235, 187, 93, 210);
                outlineImage.raycastTarget = false;
                var inset = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                inset.transform.SetParent(outline.transform, false);
                RectTransform insetRect = inset.GetComponent<RectTransform>();
                Place(insetRect, Vector2.zero, Vector2.one);
                insetRect.offsetMin = new Vector2(3, 3);
                insetRect.offsetMax = new Vector2(-3, -3);
                Image insetImage = inset.GetComponent<Image>();
                insetImage.sprite = sprite;
                insetImage.preserveAspect = true;
                insetImage.raycastTarget = false;
                outline.SetActive(group.group_id == selectedGroupId);
                visibleIndex++;
            }
        }

        private void SelectAppearanceGroup(int sex, int groupId)
        {
            if (!assets.TryGetAppearanceGroup(sex, groupId, out CharacterCreationAppearanceGroupInfo group)
                || group.condition_id != 0) return;
            CharacterAppearanceDraft appearance = sex == 0 ? draft.bodies.male : draft.bodies.female;
            appearance.appearance_group_id = groupId;
            RefreshNativePreviews();
            QueueDraftSave();
            RenderAppearancePage();
        }

        private void RenderNamePage()
        {
            Transform inputRoot = RequireNode(setNameContent, "NameInput");
            TMP_InputField input = inputRoot.GetComponent<TMP_InputField>();
            if (input == null) throw new InvalidOperationException("Official character name TMP_InputField is missing.");
            input.characterLimit = service.GetOfficialNameMaximum();
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.SetTextWithoutNotify(draft.name ?? string.Empty);
            // Offline manual naming follows the official enabled-input branch.
            // The server moderation mask otherwise remains active in the prefab.
            foreach (Transform node in inputRoot.GetComponentsInChildren<Transform>(true))
                if (node.name == "InputFieldMask") node.gameObject.SetActive(false);
            input.onValueChanged.RemoveAllListeners();
            input.onValueChanged.AddListener(value =>
            {
                draft.name = value;
                UpdateNameCounter();
                QueueDraftSave();
            });
            Transform limit = FindDeep(setNameContent, "WordLimit");
            nameCounter = FindText(limit);
            UpdateNameCounter();
        }

        private void RenderAppearanceOptions(CharacterAppearanceDraft appearance, string field, Transform viewRoot)
        {
            if (viewRoot == null) throw new InvalidOperationException("Official character creation option view is missing: " + field);
            string key = appearance.body_type + ":" + field;
            if (populatedOptionKey != key)
            {
                string templateName = field == "hairstyle_id" ? "HairTypeItem" : "EditTypeSelectItem";
                Transform template = RequireNode(viewRoot, templateName);
                Transform content = FindDeep(viewRoot, "Content");
                if (content == null) content = template.parent;
                if (content == null) throw new InvalidOperationException("Official character creation option content is missing: " + field);
                template.gameObject.SetActive(false);
                ClearChildrenExcept(content, template);
                optionItems.Clear();
                EnsureOptionScroll(viewRoot, content);

                foreach (CharacterCreationOptionInfo option in assets.GetAppearanceOptions(appearance.body_type, field))
                {
                    Transform item = Instantiate(template.gameObject, content, false).transform;
                    item.name = templateName + "_" + option.Id;
                    item.gameObject.SetActive(true);
                    Image icon = FindDeep(item, "Icon")?.GetComponent<Image>();
                    if (icon == null)
                        icon = item.GetComponentsInChildren<Image>(true).FirstOrDefault(image =>
                            image.name != "Select" && image.name != "Selected" && image.name != "Background");
                    if (icon != null)
                    {
                        icon.sprite = assets.FindSkinIcon(option.SkinName);
                        icon.preserveAspect = true;
                    }
                    BindOptionItem(item, option, appearance, field);
                    optionItems.Add(option.Id, item);
                }
                populatedOptionKey = key;
            }

            string selectedId = CurrentOption(appearance, field);
            foreach (KeyValuePair<string, Transform> item in optionItems)
                SetOfficialSelected(item.Value, item.Key == selectedId);
        }

        private void RenderColorPicker(CharacterAppearanceDraft appearance, int type, Transform colorRoot)
        {
            string hex = CurrentColor(appearance, type);
            Transform fixedRoot = RequireNode(colorRoot, "FixedColorGo");
            Transform customRoot = RequireNode(colorRoot, "CustomColorGo");
            fixedRoot.gameObject.SetActive(!customColorOpen);
            customRoot.gameObject.SetActive(customColorOpen);

            string key = type.ToString();
            if (populatedDyeKey != key)
            {
                Transform scrollView = RequireNode(fixedRoot, "FixedColorScrollView");
                Transform content = FindDeep(scrollView, "ItemGroup");
                Transform template = RequireNode(scrollView, "FixedColorItem");
                if (content == null) throw new InvalidOperationException("Official fixed dye color content is missing.");
                template.gameObject.SetActive(false);
                ClearChildrenExcept(content, template);
                dyeItems.Clear();
                EnsureDyeScroll(scrollView, content);

                foreach (CharacterCreationDyeChoice choice in service.GetFixedDyeChoices(type))
                {
                    Transform item = Instantiate(template.gameObject, content, false).transform;
                    item.name = "FixedColorItem_" + choice.ColorId;
                    item.gameObject.SetActive(true);
                    Image swatch = FindDeep(item, "Color")?.GetComponent<Image>();
                    if (swatch != null) swatch.color = ParseColor(choice.Hex);
                    BindDyeItem(item, type, choice);
                    dyeItems.Add(choice.ColorId, item);
                }
                BindCustomColorControls(colorRoot);
                populatedDyeKey = key;
            }

            LoadDyeState(appearance, type, hex);
            int[] range = service.GetDyeHsvRange(type);
            ConfigureHsvSlider(colorRoot, "HSlider", range[0], range[1], dyeHue, value => dyeHue = value, type);
            ConfigureHsvSlider(colorRoot, "SSlider", range[2], range[3], dyeSaturation, value => dyeSaturation = value, type);
            ConfigureHsvSlider(colorRoot, "VSlider", range[4], range[5], dyeValue, value => dyeValue = value, type);
            RefreshHsvGradient(colorRoot);
            foreach (KeyValuePair<int, Transform> item in dyeItems)
            {
                SetOfficialSelected(item.Value, item.Key == dyeColorId);
            }
            Transform tip = FindDeep(customRoot, "Tip");
            SetLabel(FindText(tip),
                "#" + CurrentColor(appearance, type));
        }

        private void ConfigureHsvSlider(Transform colorRoot, string sliderName, int minimum, int maximum,
            int current, Action<int> update, int type)
        {
            Transform sliderNode = RequireNode(colorRoot, sliderName);
            Slider slider = sliderNode.GetComponent<Slider>();
            if (slider == null)
            {
                slider = sliderNode.gameObject.AddComponent<Slider>();
                slider.direction = Slider.Direction.LeftToRight;
            }
            if (slider.handleRect == null)
                slider.handleRect = FindDeep(sliderNode, "Handle") as RectTransform;
            if (slider.targetGraphic == null && slider.handleRect != null)
                slider.targetGraphic = slider.handleRect.GetComponent<Graphic>();
            if (slider.handleRect == null || slider.targetGraphic == null)
                throw new InvalidOperationException("Official character dye slider handle is incomplete: " + sliderName);
            slider.onValueChanged.RemoveAllListeners();
            slider.minValue = minimum;
            slider.maxValue = maximum;
            slider.wholeNumbers = true;
            slider.SetValueWithoutNotify(Mathf.Clamp(current, minimum, maximum));
            slider.onValueChanged.AddListener(value =>
            {
                update(Mathf.RoundToInt(value));
                SetDye(type, 0, dyeHue, dyeSaturation, dyeValue);
                RefreshHsvGradient(colorRoot);
                Transform tip = FindDeep(RequireNode(colorRoot, "CustomColorGo"), "Tip");
                SetLabel(FindText(tip),
                    "#" + CurrentColor(ActiveAppearance(), type));
            });
        }

        private void SetDye(int type, int colorId, int hue, int saturation, int value)
        {
            if (!service.TrySetDraftDye(ActiveAppearance(), type, colorId, hue, saturation, value, out string error))
            {
                ShowStatus(error);
                return;
            }
            dyeType = type;
            dyeColorId = colorId;
            dyeHue = hue;
            dyeSaturation = saturation;
            dyeValue = value;
            QueueDraftSave();
            RefreshNativePreviews();
            foreach (KeyValuePair<int, Transform> item in dyeItems)
                SetOfficialSelected(item.Value, item.Key == dyeColorId);
            Transform colorRoot = FindDeep(diyContent, "ColorGo");
            Transform tip = FindDeep(colorRoot, "Tip");
            SetLabel(FindText(tip), "#" + CurrentColor(ActiveAppearance(), type));
        }

        private static void RefreshHsvGradient(Transform colorRoot)
        {
            var gradient = colorRoot.GetComponent<CharacterCreationHsvGradient>();
            if (gradient == null) gradient = colorRoot.gameObject.AddComponent<CharacterCreationHsvGradient>();
            gradient.Refresh();
        }

        private void ResetAppearance(bool random)
        {
            if (!service.TryResetDraftAppearance(ActiveAppearance(), random, out string error))
            {
                ShowStatus(error);
                return;
            }
            QueueDraftSave();
            RefreshNativePreviews();
            RenderAppearancePage();
        }

        private void LoadDyeState(CharacterAppearanceDraft appearance, int type, string hex)
        {
            dyeType = type;
            CharacterCreationDyeChoice fixedChoice = service.GetFixedDyeChoices(type)
                .FirstOrDefault(choice => string.Equals(choice.Hex, hex, StringComparison.OrdinalIgnoreCase));
            if (fixedChoice != null)
            {
                dyeColorId = fixedChoice.ColorId;
                dyeHue = fixedChoice.Hue;
                dyeSaturation = fixedChoice.Saturation;
                dyeValue = fixedChoice.Value;
                return;
            }

            dyeColorId = 0;
            if (ColorUtility.TryParseHtmlString("#" + hex, out Color color))
            {
                Color.RGBToHSV(color, out float hue, out float saturation, out float value);
                dyeHue = Mathf.RoundToInt(hue * 360f) % 360;
                dyeSaturation = Mathf.RoundToInt(saturation * 100f);
                dyeValue = Mathf.RoundToInt(value * 100f);
            }
        }

        private void AddBottomNavigation(Action back, Action next, string backText, string nextText)
        {
            Button backButton = AddButton(controlsRoot, backText, 22, back, new Color32(57, 66, 65, 255), 64,
                "Main_Cursor_Back");
            Button nextButton = AddButton(controlsRoot, nextText, 22, next, new Color32(142, 102, 53, 255), 64);
            Place(backButton.GetComponent<RectTransform>(), new Vector2(0.08f, 0.015f), new Vector2(0.46f, 0.08f));
            Place(nextButton.GetComponent<RectTransform>(), new Vector2(0.54f, 0.015f), new Vector2(0.92f, 0.08f));
        }

        private void AddButtonRow(string[] values, string[] labels, float minY, float maxY, Action<string> click,
            Color color, string soundEvent = "Main_Cursor_Normal")
        {
            if (values.Length == 0) return;
            float width = 0.86f / values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                string value = values[i];
                Button button = AddButton(controlsRoot, labels[i], 17, () => click(value), color, 48, soundEvent);
                float minX = 0.07f + i * width;
                Place(button.GetComponent<RectTransform>(), new Vector2(minX, minY), new Vector2(minX + width - 0.008f, maxY));
            }
        }

        private void CreateStepPanel(string title, float minX, float minY, float maxX, float maxY)
        {
            RectTransform panel = CreateRect(controlsRoot, "StepPanel", new Vector2(minX, minY), new Vector2(maxX, maxY));
            Image image = panel.gameObject.AddComponent<Image>();
            image.color = new Color32(20, 27, 31, 228);
            AddText(panel, title, 25, new Color32(251, 223, 175, 255),
                new Vector2(0.05f, 0.77f), new Vector2(0.95f, 0.98f), TextAlignmentOptions.Center);
        }

        private void SelectBody(string body)
        {
            if (draft.page != "body" || body == draft.body_type
                || (performancePlayer != null && performancePlayer.IsTransitionPlaying)) return;
            PlayUiSound("Main_Page");
            draft.body_type = body;
            QueueDraftSave();
            RefreshNativePreviews(true);
            performancePlayer.PlaySelection(body == "female" ? "SelFemale" : "SelMale");
            performancePlayer.PlaySelectedGlow(body);
            RenderDraftPage(false);
        }

        private void BackFromCreationPage()
        {
            if (performancePlayer != null && performancePlayer.IsTransitionPlaying) return;
            PlayUiSound("Main_Cursor_Back");
            if (draft.page == "body") BackToSlots();
            else PreviousPage();
        }

        private void ConfirmDefaultAppearanceReset()
        {
            ShowOfficialConfirmation("ui_reset_appearance_tip_text", () => ResetAppearance(false));
        }

        private void ShowOfficialConfirmation(string textKey, Action confirmed)
        {
            string message = textKey == "ui_reset_appearance_tip_text"
                ? "是否恢复默认外观？"
                : textKey;
            ShowOfficialDialog("确认", message, "确认", "取消", true, confirmed);
        }

        private void ShowOfficialMessage(string message)
        {
            if (activeConfirmation != null)
            {
                pendingStorageError = message;
                return;
            }
            ShowOfficialDialog("提示", message, "确认", null, false, null);
        }

        private void ShowOfficialDialog(string title, string message, string confirmText,
            string cancelText, bool showCancel, Action confirmed)
        {
            if (activeConfirmation != null) return;
            if (assets.ConfirmDialogPrefab == null)
                throw new InvalidOperationException("Official character creation confirmation dialog is not bound.");

            activeConfirmation = Instantiate(assets.ConfirmDialogPrefab, canvas.transform, false);
            activeConfirmation.name = "OfficialConfirmDialog";
            activeConfirmation.transform.SetAsLastSibling();
            Canvas dialogCanvas = activeConfirmation.GetComponent<Canvas>();
            if (dialogCanvas != null)
            {
                dialogCanvas.overrideSorting = true;
                dialogCanvas.sortingOrder = canvas.sortingOrder + 1;
            }

            SetLabel(FindText(RequireNode(activeConfirmation.transform, "TitleText")), title);
            SetLabel(FindText(RequireNode(activeConfirmation.transform, "ContentTxt")), message);

            Transform confirmNode = RequireNode(activeConfirmation.transform, "ConfirmBtn");
            Transform cancelNode = RequireNode(activeConfirmation.transform, "CancelBtn");
            SetLabel(FindText(confirmNode), confirmText);
            cancelNode.gameObject.SetActive(showCancel);
            if (showCancel) SetLabel(FindText(cancelNode), cancelText);
            Button confirm = EnsureButton(confirmNode.gameObject);
            confirm.onClick.RemoveAllListeners();
            confirm.onClick.AddListener(() =>
            {
                CloseOfficialConfirmation();
                PlayUiSound("Main_Cursor_Normal");
                confirmed?.Invoke();
            });
            if (showCancel)
            {
                Button cancel = EnsureButton(cancelNode.gameObject);
                cancel.onClick.RemoveAllListeners();
                cancel.onClick.AddListener(() =>
                {
                    CloseOfficialConfirmation();
                    PlayUiSound("Main_Cursor_Back");
                });
            }
        }

        private void CloseOfficialConfirmation()
        {
            GameObject dialog = activeConfirmation;
            activeConfirmation = null;
            if (dialog != null) Destroy(dialog);
            if (!string.IsNullOrEmpty(pendingStorageError))
            {
                string message = pendingStorageError;
                pendingStorageError = null;
                StartCoroutine(ShowOfficialMessageAfterDialog(message));
            }
        }

        private IEnumerator ShowOfficialMessageAfterDialog(string message)
        {
            yield return null;
            ShowOfficialMessage(message);
        }

        private void BindFieldTabButtons(string groupName, string[] fields)
        {
            Transform group = FindPath(diyContent, "BottomContent", "SubTabGroupGo", groupName, "TabGroup");
            BindTabButtons(group, "BottomContent/SubTabGroupGo/" + groupName + "/TabGroup", fields, SelectField);
        }

        private void BindTabButtons(Transform group, string groupPath, string[] values, Action<string> onSelect)
        {
            Transform[] items = GetTabItems(group);
            if (group == null || items.Length != values.Length)
                throw new InvalidOperationException("Official character creation tab count differs from its data: "
                    + (group == null ? "missing " + groupPath
                        : group.name + " has " + items.Length + " items; expected " + values.Length));

            for (int i = 0; i < values.Length; i++)
            {
                string value = values[i];
                Transform item = items[i];
                Button button = item.GetComponent<Button>() ?? item.GetComponentInChildren<Button>(true);
                if (button == null) button = EnsureButton(item.gameObject);
                button.onClick.AddListener(() =>
                {
                    PlayUiSound("Main_Tag");
                    onSelect(value);
                });
            }
        }

        private void BindOptionItem(Transform item, CharacterCreationOptionInfo option,
            CharacterAppearanceDraft appearance, string field)
        {
            Toggle toggle = item.GetComponentInChildren<Toggle>(true);
            Action choose = () =>
            {
                if (!service.TrySetAppearanceOption(appearance, field, option.Id, out string error))
                {
                    ShowStatus(error);
                    return;
                }
                PlayUiSound("Main_Cursor_Normal");
                QueueDraftSave();
                RefreshNativePreviews();
                SetOfficialSelected(item, true);
                foreach (KeyValuePair<string, Transform> other in optionItems)
                    if (other.Key != option.Id) SetOfficialSelected(other.Value, false);
            };

            if (toggle != null)
            {
                toggle.group = null;
                toggle.onValueChanged.RemoveAllListeners();
                toggle.onValueChanged.AddListener(isOn => { if (isOn) choose(); });
                toggle.SetIsOnWithoutNotify(CurrentOption(appearance, field) == option.Id);
            }
            else
            {
                EnsureButton(item.gameObject).onClick.AddListener(() =>
                {
                    choose();
                    Button button = item.GetComponent<Button>();
                    if (button != null) button.Select();
                });
            }
            SetOfficialSelected(item, CurrentOption(appearance, field) == option.Id);
        }

        private void BindDyeItem(Transform item, int type, CharacterCreationDyeChoice choice)
        {
            Toggle toggle = item.GetComponentInChildren<Toggle>(true);
            Action choose = () =>
            {
                PlayUiSound("Main_Cursor_Normal");
                SetDye(type, choice.ColorId, choice.Hue, choice.Saturation, choice.Value);
            };
            if (toggle != null)
            {
                toggle.group = null;
                toggle.onValueChanged.RemoveAllListeners();
                toggle.onValueChanged.AddListener(isOn => { if (isOn) choose(); });
            }
            else
            {
                EnsureButton(item.gameObject).onClick.AddListener(() => choose());
            }
        }

        private void BindCustomColorControls(Transform colorRoot)
        {
            if (customColorControlsBound) return;
            Transform customItem = FindDeep(colorRoot, "CustomColorItem");
            if (customItem != null)
            {
                Toggle toggle = customItem.GetComponentInChildren<Toggle>(true);
                if (toggle != null)
                    toggle.onValueChanged.AddListener(isOn => { if (isOn) OpenCustomColor(); });
                else EnsureButton(customItem.gameObject).onClick.AddListener(OpenCustomColor);
            }

            Transform close = FindDeep(colorRoot, "CloseCustomColorBtn");
            if (close != null) EnsureButton(close.gameObject).onClick.AddListener(CloseCustomColor);
            customColorControlsBound = true;
        }

        private void OpenCustomColor()
        {
            PlayUiSound("Main_Tag");
            customColorOpen = true;
            RenderAppearancePage();
        }

        private void CloseCustomColor()
        {
            PlayUiSound("Main_Cursor_Back");
            customColorOpen = false;
            RenderAppearancePage();
        }

        private void EnsureOptionScroll(Transform viewRoot, Transform content)
        {
            Transform viewport = FindDeep(viewRoot, "Viewport");
            if (viewport == null || !(viewport is RectTransform viewportRect)
                || !(content is RectTransform contentRect))
                throw new InvalidOperationException("Official character creation option scroll hierarchy is incomplete.");
            Transform scrollRoot = FindDeep(viewRoot, "ScrollRoot") ?? viewport.parent;
            ScrollRect scroll = scrollRoot.GetComponent<ScrollRect>();
            if (scroll == null) scroll = scrollRoot.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            if (viewport.GetComponent<RectMask2D>() == null && viewport.GetComponent<Mask>() == null)
                viewport.gameObject.AddComponent<RectMask2D>();
            if (content.GetComponent<ContentSizeFitter>() == null)
            {
                ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
        }

        private void EnsureDyeScroll(Transform scrollView, Transform content)
        {
            Transform viewport = FindDeep(scrollView, "Viewport");
            if (viewport == null || !(viewport is RectTransform viewportRect)
                || !(content is RectTransform contentRect))
                throw new InvalidOperationException("Official fixed dye color scroll hierarchy is incomplete.");
            ScrollRect scroll = scrollView.GetComponent<ScrollRect>();
            if (scroll == null) scroll = scrollView.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.viewport = viewportRect;
            scroll.content = contentRect;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            if (viewport.GetComponent<RectMask2D>() == null && viewport.GetComponent<Mask>() == null)
                viewport.gameObject.AddComponent<RectMask2D>();
            if (content.GetComponent<LayoutGroup>() == null)
            {
                HorizontalLayoutGroup layout = content.gameObject.AddComponent<HorizontalLayoutGroup>();
                layout.childControlWidth = false;
                layout.childControlHeight = false;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
            }
            if (content.GetComponent<ContentSizeFitter>() == null)
            {
                ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
                fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
        }

        private static Button EnsureButton(GameObject target)
        {
            Button button = target.GetComponent<Button>();
            if (button == null) button = target.AddComponent<Button>();
            if (button.targetGraphic == null)
            {
                Graphic graphic = target.GetComponent<Graphic>();
                if (graphic == null)
                {
                    graphic = target.AddComponent<Image>();
                    graphic.color = Color.clear;
                }
                button.targetGraphic = graphic;
            }
            if (button.targetGraphic != null) button.targetGraphic.raycastTarget = true;
            button.interactable = true;
            return button;
        }

        private static Transform RequireNode(Transform root, string name)
        {
            Transform result = FindDeep(root, name);
            if (result == null) throw new InvalidOperationException("Official character creation UI node is missing: " + name);
            return result;
        }

        private static Transform FindPath(Transform root, params string[] names)
        {
            Transform current = root;
            foreach (string name in names)
            {
                current = current == null ? null : current.Find(name);
                if (current == null) return null;
            }
            return current;
        }

        private static void ClearChildrenExcept(Transform root, Transform keep)
        {
            for (int i = root.childCount - 1; i >= 0; i--)
            {
                Transform child = root.GetChild(i);
                if (child != keep && child.name != "CustomColorItem") Destroy(child.gameObject);
            }
        }

        private static Component FindText(Transform root)
        {
            if (root == null) return null;
            TMP_Text tmp = root.GetComponent<TMP_Text>() ?? root.GetComponentInChildren<TMP_Text>(true);
            return tmp != null ? (Component)tmp : root.GetComponent<Text>() ?? root.GetComponentInChildren<Text>(true);
        }

        private static void SetLabel(Component label, string value)
        {
            if (label is TMP_Text tmp) tmp.text = value ?? string.Empty;
            else if (label is Text text) text.text = value ?? string.Empty;
        }

        private static void SetOfficialSelected(Transform item, bool selected)
        {
            if (item == null) return;
            Toggle toggle = item.GetComponentInChildren<Toggle>(true);
            if (toggle != null) toggle.SetIsOnWithoutNotify(selected);
            foreach (string name in new[] { "Checkmark", "Select", "Sel", "Selected" })
            {
                Transform marker = FindDeep(item, name);
                if (marker != null && marker != item)
                {
                    marker.gameObject.SetActive(selected);
                }
            }
        }

        private static void SetTabSelection(Transform group, int index)
        {
            Transform[] items = GetTabItems(group);
            if (group == null || index < 0 || index >= items.Length)
                throw new InvalidOperationException("Official character creation tab selection is invalid.");
            for (int i = 0; i < items.Length; i++) SetOfficialSelected(items[i], i == index);
        }

        private static Transform[] GetTabItems(Transform group)
        {
            if (group == null) return Array.Empty<Transform>();
            return group.Cast<Transform>().Where(item =>
                item.name.StartsWith("CreatePlayerPanelTabGroupItem", StringComparison.Ordinal)
                || item.GetComponent<Button>() != null
                || item.GetComponentInChildren<Button>(true) != null).ToArray();
        }

        private static string GroupForTab(string tab)
        {
            switch (tab)
            {
                case "整体": return "WholeTabGroup";
                case "头发": return "HairTabGroup";
                case "脸部": return "FaceTabGroup";
                case "特征": return "RaceTabGroup";
                default: throw new InvalidOperationException("Unknown official character creation tab: " + tab);
            }
        }

        private static string OptionViewForField(string field)
        {
            switch (field)
            {
                case "hairstyle_id": return "HairTypeSelect";
                case "eyebrow_id":
                case "eye_id":
                case "makeup_id": return "EyeTypeSelect";
                case "mouth_id": return "MouthTypeSelect";
                case "feature_id": return "EthnicityTypeSelect";
                default: throw new InvalidOperationException("Character field has no official option view: " + field);
            }
        }

        private void SelectTab(string tab)
        {
            if (performancePlayer != null && performancePlayer.IsTransitionPlaying) return;
            draft.tab = tab;
            draft.field = FieldsForTab(tab)[0];
            customColorOpen = false;
            QueueDraftSave();
            RefreshNativePreviews();
            RenderAppearancePage();
        }

        private void SelectField(string field)
        {
            if (performancePlayer != null && performancePlayer.IsTransitionPlaying) return;
            draft.field = field;
            customColorOpen = false;
            QueueDraftSave();
            RenderAppearancePage();
        }

        private void NextPage()
        {
            if (performancePlayer != null && performancePlayer.IsTransitionPlaying) return;
            if (!PersistDraft()) return;

            string previousPage = draft.page;
            string animation = null;
            if (draft.page == "body")
            {
                draft.page = "appearance";
                animation = draft.body_type == "female" ? "ShapeToDiy_Female" : "ShapeToDiy_Male";
            }
            else if (draft.page == "appearance")
            {
                draft.page = "name";
                animation = "DiyToSetName";
            }
            else return;

            if (!PersistDraft())
            {
                draft.page = previousPage;
                return;
            }
            RefreshNativePreviews();
            RenderDraftPage(false);
            performancePlayer.PlayRoot(animation);
        }

        private void PreviousPage()
        {
            if (performancePlayer != null && performancePlayer.IsTransitionPlaying) return;
            if (!PersistDraft()) return;

            string previousPage = draft.page;
            string animation;
            if (draft.page == "name")
            {
                draft.page = "appearance";
                animation = "SetNameToDiy";
            }
            else
            {
                draft.page = "body";
                animation = draft.body_type == "female" ? "DiyToShape_Female" : "DiyToShape";
            }
            if (!PersistDraft())
            {
                draft.page = previousPage;
                return;
            }
            RefreshNativePreviews();
            RenderDraftPage(false);
            performancePlayer.PlayRoot(animation);
        }

        private void BackToSlots()
        {
            if (!PersistDraft()) return;
            ShowSlots();
        }

        private void SubmitCharacter()
        {
            if (performancePlayer != null && performancePlayer.IsTransitionPlaying) return;
            if (!PersistDraft()) return;
            if (!service.TryCreateCharacter(slotId, draft, out string error))
            {
                ShowOfficialMessage(error);
                return;
            }
            ClearScreen();
            try
            {
                PrepareFirstStoryHome();
            }
            catch (Exception storyHomeError)
            {
                Debug.LogException(storyHomeError, this);
                DisposeFirstStoryHome();
                ShowStatus("主城演出资源不可用，继续剧情剩余节点。");
            }
            opening.FirstStoryHomeAction = PlayFirstStoryHomeAction;
            opening.PlayFirstStory(ShowInitialWeapon, message => ShowStatus(message));
        }

        private void ShowInitialWeapon()
        {
            if (opening != null) opening.FirstStoryHomeAction = null;
            DisposeFirstStoryHome();
            ClearScreen();
            screen = CreateScreen("选择初始职业");
            AddText(screen.transform, "选择使用剑或法杖", 24, new Color32(242, 218, 174, 255),
                new Vector2(0.1f, 0.68f), new Vector2(0.9f, 0.76f), TextAlignmentOptions.Center);
            Button sword = AddButton(screen.transform, "战士", 30,
                () => ChooseWeapon(CharacterCreationOfflineService.SwordInitialWeaponType),
                new Color32(110, 82, 49, 255), 92);
            Button wand = AddButton(screen.transform, "法师", 30,
                () => ChooseWeapon(CharacterCreationOfflineService.WandInitialWeaponType),
                new Color32(60, 77, 92, 255), 92);
            Place(sword.GetComponent<RectTransform>(), new Vector2(0.12f, 0.42f), new Vector2(0.88f, 0.53f));
            Place(wand.GetComponent<RectTransform>(), new Vector2(0.12f, 0.27f), new Vector2(0.88f, 0.38f));
            Button back = AddButton(screen.transform, "返回存档", 20, () => ShowSlots(),
                new Color32(55, 66, 67, 255), 64);
            Place(back.GetComponent<RectTransform>(), new Vector2(0.2f, 0.1f), new Vector2(0.8f, 0.17f));
        }

        private void ChooseWeapon(int weaponType)
        {
            if (!service.TrySetInitialWeapon(slotId, weaponType, out string error))
            {
                ShowSlots(error);
                return;
            }
            draft = null;
            ShowComplete("角色已创建：" + service.GetCharacterName(slotId));
        }

        private void ShowComplete(string message)
        {
            ClearScreen();
            screen = CreateScreen("角色创建完成");
            AddText(screen.transform, message, 26, new Color32(250, 224, 180, 255),
                new Vector2(0.08f, 0.52f), new Vector2(0.92f, 0.62f), TextAlignmentOptions.Center);
            Button character = AddButton(screen.transform, "人物", 24, OpenCharacterProfile,
                new Color32(77, 92, 79, 255), 74);
            Place(character.GetComponent<RectTransform>(), new Vector2(0.16f, 0.4f), new Vector2(0.84f, 0.49f));
            Button enterMap = AddButton(screen.transform, "进入冒险", 24, EnterInitialIsland,
                new Color32(76, 91, 71, 255), 74);
            Place(enterMap.GetComponent<RectTransform>(), new Vector2(0.16f, 0.29f), new Vector2(0.84f, 0.38f));
            Button enterHome = AddButton(screen.transform, "进入家园", 24, EnterHome,
                new Color32(74, 88, 96, 255), 74);
            Place(enterHome.GetComponent<RectTransform>(), new Vector2(0.16f, 0.18f), new Vector2(0.84f, 0.27f));
            Button slots = AddButton(screen.transform, "返回存档", 24, () => ShowSlots(),
                new Color32(122, 86, 46, 255), 74);
            Place(slots.GetComponent<RectTransform>(), new Vector2(0.16f, 0.07f), new Vector2(0.84f, 0.16f));
        }

        private void EnterHome()
        {
            if (string.IsNullOrEmpty(slotId) || !service.HasCharacter(slotId)
                || service.GetInitialWeaponType(slotId) == 0)
            {
                ShowComplete("请先完成角色创建和职业选择。");
                return;
            }

            try
            {
                homeRuntime = Zjcs.Presentation.Home.HomeFeatureRuntime.Begin(slotId,
                    SaveSelectionProbe.ProfileRoot, canvas.transform, OnHomeClosed, OpenInitialIslandFromHome);
                if (screen != null) screen.SetActive(false);
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                if (homeRuntime != null)
                {
                    Destroy(homeRuntime.gameObject);
                    homeRuntime = null;
                }
                if (screen != null) screen.SetActive(true);
                ShowComplete("家园未能打开：" + error.Message);
            }
        }

        private void OnHomeClosed()
        {
            homeRuntime = null;
            if (screen != null) screen.SetActive(true);
            ShowComplete("已返回主菜单");
        }

        private void OpenInitialIslandFromHome()
        {
            Zjcs.Presentation.Home.HomeFeatureRuntime runtime = homeRuntime;
            homeRuntime = null;
            runtime?.CloseForNavigation();
            EnterInitialIsland();
        }

        private void EnterInitialIsland()
        {
            if (string.IsNullOrEmpty(slotId) || !service.HasCharacter(slotId)
                || service.GetInitialWeaponType(slotId) == 0)
            {
                ShowComplete("请先完成角色创建和职业选择。");
                return;
            }

            GameObject runtimeObject = null;
            GameObject model = null;
            try
            {
                var slots = new ProfileSlotCatalog(SaveSelectionProbe.ProfileRoot);
                OfflineMainlineProgressSession progress = OfflineMainlineProgressSession.OpenCharacter(slots, slotId);
                int mapClassId = OfficialMainlineMapRuntime.ResolveHomeReturnMap(progress.Save);
                model = CharacterCreationNativeModelFactory.CreateWorldModel(slotId,
                    SaveSelectionProbe.ProfileRoot, out string name);
                runtimeObject = new GameObject("Map" + mapClassId + "GameRuntime");
                OfficialMainlineMapRuntime runtime = runtimeObject.AddComponent<OfficialMainlineMapRuntime>();
                runtime.MapMusicVolume = () => Zjcs.Presentation.Home.HomeAudioSettings.MusicVolume;
                runtime.MapVoiceVolume = () => Zjcs.Presentation.Home.HomeAudioSettings.VoiceVolume;
                runtime.MapSoundRegistration = (source, registered) =>
                {
                    if (registered) Zjcs.Presentation.Home.HomeAudioSettings.Register(source, Zjcs.Presentation.Home.HomeAudioChannel.Sound);
                    else Zjcs.Presentation.Home.HomeAudioSettings.Unregister(source);
                };
                runtime.Begin(mapClassId, slotId, SaveSelectionProbe.ProfileRoot, canvas.transform, model, name,
                    () => { if (screen != null) screen.SetActive(true); });
                if (screen != null) screen.SetActive(false);
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                if (runtimeObject != null) Destroy(runtimeObject);
                if (model != null) Destroy(model);
                if (screen != null) screen.SetActive(true);
                ShowComplete("冒险地图未能打开：" + error.Message);
            }
        }

        private void OpenCharacterProfile()
        {
            if (string.IsNullOrEmpty(slotId) || !service.HasCharacter(slotId)
                || service.GetInitialWeaponType(slotId) == 0)
            {
                ShowComplete("请先完成角色创建和职业选择。");
                return;
            }
            if (characterFeatureRoot == null)
            {
                characterFeatureRoot = new GameObject("CharacterFeatureRuntime", typeof(RectTransform));
                characterFeatureRoot.transform.SetParent(canvas.transform, false);
                characterPanel = characterFeatureRoot.AddComponent<OfficialCharacterPanelView>();
                characterRuntime = characterFeatureRoot.AddComponent<CharacterFeatureRuntime>();
                characterPanel.CloseRequested += CloseCharacterProfile;
            }

            try
            {
                canvas.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1080, 1920);
                characterFeatureRoot.SetActive(true);
                characterRuntime.OpenProfile(slotId);
                if (screen != null) screen.SetActive(false);
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                characterFeatureRoot.SetActive(false);
                canvas.GetComponent<CanvasScaler>().referenceResolution = new Vector2(720, 1280);
                if (screen != null) screen.SetActive(true);
                ShowComplete("人物界面未能打开：" + error.Message);
            }
        }

        private void CloseCharacterProfile()
        {
            if (characterFeatureRoot != null) characterFeatureRoot.SetActive(false);
            canvas.GetComponent<CanvasScaler>().referenceResolution = new Vector2(720, 1280);
            if (screen != null) screen.SetActive(true);
        }

        private void ShowStatus(string message)
        {
            SetLabel(status != null ? status : nameCounter, message);
            Debug.LogWarning(message, this);
        }

        private void RefreshStatus()
        {
            if (status == null) return;
            SetLabel(status, draft.page == "body" ? SlotLabel(slotId)
                : draft.page == "appearance" ? FieldLabel(draft.field)
                : string.Empty);
        }

        private void UpdateNameCounter()
        {
            if (nameCounter == null) return;
            int count = service.CountOfficialNameCharacters(draft.name);
            SetLabel(nameCounter, count + " / " + service.GetOfficialNameMaximum());
        }

        private void QueueDraftSave()
        {
            if (pendingSave != null) StopCoroutine(pendingSave);
            pendingSave = StartCoroutine(SaveDraftAfterDelay());
        }

        private IEnumerator SaveDraftAfterDelay()
        {
            yield return new WaitForSecondsRealtime(0.25f);
            pendingSave = null;
            PersistDraft();
        }

        private bool PersistDraft()
        {
            if (service == null || draft == null || string.IsNullOrEmpty(slotId)) return true;
            if (pendingSave != null)
            {
                StopCoroutine(pendingSave);
                pendingSave = null;
            }
            try
            {
                service.SaveDraft(slotId, draft);
                return true;
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                ShowOfficialMessage("创角草稿保存失败，当前操作已取消。\n" + error.Message);
                return false;
            }
        }

        private CharacterAppearanceDraft ActiveAppearance()
        {
            return draft.body_type == "female" ? draft.bodies.female : draft.bodies.male;
        }

        private void ClearScreen()
        {
            if (canvas != null) canvas.GetComponent<CanvasScaler>().screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            CloseOfficialConfirmation();
            malePreview?.Dispose();
            femalePreview?.Dispose();
            malePreview = null;
            femalePreview = null;
            maleModelNode = null;
            femaleModelNode = null;
            if (screen != null) Destroy(screen);
            screen = null;
            officialPanel = null;
            controlsRoot = null;
            status = null;
            nameCounter = null;
            selectSexContent = null;
            diyContent = null;
            setNameContent = null;
            performancePlayer = null;
            performanceAssets = null;
            customColorControlsBound = false;
            customColorOpen = false;
        }

        private void InitializeNativePreviews()
        {
            maleModelNode = FindDeep(officialPanel.transform, "MaleModel");
            femaleModelNode = FindDeep(officialPanel.transform, "FemaleModel");
            if (!(maleModelNode is RectTransform maleRect) || !(femaleModelNode is RectTransform femaleRect))
                throw new InvalidOperationException("Official character preview placeholders are missing.");
            malePreview = new CharacterCreationNativeModelPreview(transform, maleRect, "Male", 30);
            femalePreview = new CharacterCreationNativeModelPreview(transform, femaleRect, "Female", 29);
            RefreshNativePreviews(true);
        }

        private void RefreshNativePreviews(bool restartAnimation = false)
        {
            if (draft == null || malePreview == null || femalePreview == null) return;
            bool isFemale = draft.body_type == "female";
            if (maleModelNode != null) maleModelNode.gameObject.SetActive(!isFemale);
            if (femaleModelNode != null) femaleModelNode.gameObject.SetActive(isFemale);
            malePreview.SetVisible(!isFemale);
            femalePreview.SetVisible(isFemale);

            CharacterAppearanceDraft appearance = ActiveAppearance();
            int sex = isFemale ? 1 : 0;
            Dictionary<int, CharacterCreationSkinInfo> selectedSkins = ResolveSelectedSkins(
                appearance, draft.page == "appearance");
            var skinNames = selectedSkins.Values.Select(info => info.Name).Distinct(StringComparer.Ordinal).ToList();
            Dictionary<int, Vector4[]> dyeChanges = ResolveDyeChanges(appearance, selectedSkins);

            CharacterCreationNativeModelPreview active = isFemale ? femalePreview : malePreview;
            active.ApplySkins(skinNames);
            active.ApplyDyeChannels(dyeChanges);
            if (restartAnimation) active.Play("home/idle_01");
        }

        private Dictionary<int, CharacterCreationSkinInfo> ResolveSelectedSkins(
            CharacterAppearanceDraft appearance, bool applyDiyMutex = false)
        {
            int sex = appearance.body_type == "female" ? 1 : 0;
            var selected = new Dictionary<int, CharacterCreationSkinInfo>();
            foreach (CharacterCreationSkinInfo info in assets.GetDefaultSkinInfos(sex,
                CharacterCreationOfflineService.DefaultProfession))
                selected[info.Type] = info;

            foreach (string field in new[] { "hairstyle_id", "eyebrow_id", "eye_id", "mouth_id", "makeup_id", "feature_id" })
                if (assets.TryGetAppearanceOption(appearance.body_type, field, CurrentOption(appearance, field),
                    out CharacterCreationOptionInfo option)
                    && assets.TryGetSkinInfo(option.SkinId, out CharacterCreationSkinInfo info))
                    selected[option.SkinType] = info;

            int groupId = appearance.appearance_group_id == 0
                ? assets.DefaultAppearanceGroupId(sex) : appearance.appearance_group_id;
            if (!assets.TryGetAppearanceGroup(sex, groupId, out CharacterCreationAppearanceGroupInfo group)
                || group.condition_id != 0)
                throw new InvalidOperationException("The selected official appearance group is unavailable.");

            bool suppressHairAppendAppearance = applyDiyMutex
                && (draft.tab == "头发" || draft.tab == "特征");
            foreach (CharacterCreationAppearanceGroupSkin skin in group.appearance_skins)
            {
                if (suppressHairAppendAppearance && skin.skin_type == 3) continue;
                if (!assets.TryGetSkinInfo(skin.skin_id, out CharacterCreationSkinInfo info))
                    throw new InvalidOperationException("Official appearance group skin data is missing: " + skin.skin_id);
                selected[skin.skin_type] = info;
            }
            return selected;
        }

        private Dictionary<int, Vector4[]> ResolveDyeChanges(CharacterAppearanceDraft appearance,
            IReadOnlyDictionary<int, CharacterCreationSkinInfo> selectedSkins)
        {
            var changes = new Dictionary<int, Vector4[]>();
            foreach (KeyValuePair<int, CharacterCreationSkinInfo> selected in selectedSkins)
                if (selected.Value.HasDye)
                    changes.Add(selected.Key, service.GetDyeHsvChanges(appearance, selected.Key));
            return changes;
        }

        private void PrepareFirstStoryHome()
        {
            DisposeFirstStoryHome();
            if (draft == null) draft = service.OpenDraft(slotId);
            firstStoryHomeAssets ??= HomeAssetCatalog.LoadDefault();
            firstStoryHomeScene = new HomeSceneView(firstStoryHomeAssets, transform);
            foreach (HomeFurnitureScene furniture in firstStoryHomeAssets.Scene.furniture ?? Array.Empty<HomeFurnitureScene>())
                firstStoryHomeScene.SetFurnitureState(furniture.type,
                    furniture.type == 0 ? HomeFurnitureState.Functional : HomeFurnitureState.Locked);

            firstStoryHomeActor = new GameObject("FirstStoryHomePlayer");
            firstStoryHomeActor.transform.SetParent(firstStoryHomeScene.Root.transform, false);
            firstStoryHomeActor.transform.localPosition = new Vector3(4.5f, -5.44f, 0f);
            firstStoryHomeActor.transform.localScale = Vector3.one * 0.4f;
            SortingGroup sorting = firstStoryHomeActor.AddComponent<SortingGroup>();
            sorting.sortingLayerName = "Default";
            sorting.sortingOrder = firstStoryHomeScene.GetNextSortingOrder(StoryHomePlayerSortingLayer, 10);

            firstStoryHomeActorAnimation = firstStoryHomeActor.AddComponent<CharacterCreationMeshFramePlayer>();
            firstStoryHomeActorAnimation.Initialize(draft.body_type);
            CharacterAppearanceDraft appearance = ActiveAppearance();
            Dictionary<int, CharacterCreationSkinInfo> skins = ResolveSelectedSkins(appearance);
            firstStoryHomeActorAnimation.ApplySkins(skins.Values.Select(info => info.Name));
            firstStoryHomeActorAnimation.ApplyDye(ResolveDyeChanges(appearance, skins));
        }

        private IEnumerator PlayFirstStoryHomeAction(string behaviour)
        {
            if (behaviour != "StoryStun" || firstStoryHomeActor == null
                || firstStoryHomeActorAnimation == null) return null;
            firstStoryHomeActorAnimation.Play("home/idle_01");
            SortingGroup sorting = firstStoryHomeActor.GetComponent<SortingGroup>();
            if (sorting != null)
                sorting.sortingOrder = firstStoryHomeScene.GetNextSortingOrder(StoryHomePlayerSortingLayer, 11);
            return null;
        }

        private void DisposeFirstStoryHome()
        {
            firstStoryHomeActorAnimation = null;
            firstStoryHomeActor = null;
            if (firstStoryHomeScene != null)
            {
                firstStoryHomeScene.Dispose();
                firstStoryHomeScene = null;
            }
        }

        private void ResetDIYContentTabGroup()
        {
            if (draft == null || draft.page != "appearance") return;
            draft.tab = Tabs[0];
            draft.field = FieldsForTab(draft.tab)[0];
            QueueDraftSave();
            RenderAppearancePage();
        }

        private void PlayCreationRunAnimation()
        {
            ActivePreview()?.Play("map/run_01");
        }

        private void PlayCreationIdleAnimation()
        {
            ActivePreview()?.Play("home/idle_01");
        }

        private CharacterCreationNativeModelPreview ActivePreview()
        {
            if (draft == null) return null;
            return draft.body_type == "female" ? femalePreview : malePreview;
        }

        private GameObject CreateScreen(string title)
        {
            canvas.GetComponent<CanvasScaler>().referenceResolution = new Vector2(720, 1280);
            var root = new GameObject("Screen_" + title, typeof(RectTransform), typeof(Image));
            root.transform.SetParent(canvas.transform, false);
            Place(root.GetComponent<RectTransform>(), Vector2.zero, Vector2.one);
            root.GetComponent<Image>().color = new Color32(25, 34, 38, 255);
            AddText(root.transform, "杖剑传说", 30, new Color32(245, 220, 175, 255),
                new Vector2(0.08f, 0.88f), new Vector2(0.92f, 0.96f), TextAlignmentOptions.Center);
            AddText(root.transform, title, 24, Color.white,
                new Vector2(0.08f, 0.82f), new Vector2(0.92f, 0.88f), TextAlignmentOptions.Center);
            return root;
        }

        private Button AddButton(Transform parent, string label, float size, Action action, Color color, float height,
            string soundEvent = "Main_Cursor_Normal")
        {
            var go = new GameObject("Button_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0, height);
            Image image = go.GetComponent<Image>();
            image.color = color;
            Button button = go.GetComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.highlightedColor = Color.Lerp(color, Color.white, 0.14f);
            colors.pressedColor = Color.Lerp(color, Color.black, 0.18f);
            button.colors = colors;
            button.onClick.AddListener(() =>
            {
                PlayUiSound(soundEvent);
                action?.Invoke();
            });
            TMP_Text text = AddText(go.transform, label, size, Color.white,
                new Vector2(0.04f, 0.06f), new Vector2(0.96f, 0.94f), TextAlignmentOptions.Center);
            text.raycastTarget = false;
            text.enableWordWrapping = true;
            return button;
        }

        private void PlayUiSound(string eventName)
        {
            if (uiAudio == null || assets == null) return;
            AudioClip clip = assets.FindAudio(eventName);
            if (clip != null) uiAudio.PlayOneShot(clip);
        }

        private TMP_InputField CreateInput(Transform parent, string value, Vector2 anchorMin, Vector2 anchorMax)
        {
            var root = new GameObject("NameInput", typeof(RectTransform), typeof(Image), typeof(TMP_InputField));
            root.transform.SetParent(parent, false);
            Place(root.GetComponent<RectTransform>(), anchorMin, anchorMax);
            Image image = root.GetComponent<Image>();
            image.color = new Color32(235, 232, 221, 255);

            RectTransform viewport = CreateRect(root.transform, "Viewport", new Vector2(0.03f, 0.08f), new Vector2(0.97f, 0.92f));
            viewport.gameObject.AddComponent<RectMask2D>();
            TMP_Text text = AddText(viewport, value, 24, new Color32(38, 43, 43, 255),
                Vector2.zero, Vector2.one, TextAlignmentOptions.MidlineLeft);
            TMP_Text placeholder = AddText(viewport, "请输入角色名字", 21, new Color32(120, 123, 118, 255),
                Vector2.zero, Vector2.one, TextAlignmentOptions.MidlineLeft);
            TMP_InputField input = root.GetComponent<TMP_InputField>();
            input.textViewport = viewport;
            input.textComponent = text;
            input.placeholder = placeholder;
            input.targetGraphic = image;
            input.lineType = TMP_InputField.LineType.SingleLine;
            input.contentType = TMP_InputField.ContentType.Standard;
            input.text = value ?? string.Empty;
            return input;
        }

        private TMP_Text AddText(Transform parent, string value, float size, Color color,
            Vector2 anchorMin, Vector2 anchorMax, TextAlignmentOptions alignment)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            Place(go.GetComponent<RectTransform>(), anchorMin, anchorMax);
            TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
            text.text = value ?? string.Empty;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.font = font != null ? font : TMP_Settings.defaultFontAsset;
            text.raycastTarget = false;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Ellipsis;
            return text;
        }

        private RectTransform CreateRect(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            RectTransform rect = go.GetComponent<RectTransform>();
            Place(rect, anchorMin, anchorMax);
            return rect;
        }

        private static void Place(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
        {
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--) Destroy(parent.GetChild(i).gameObject);
        }

        private static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), name);
                if (found != null) return found;
            }
            return null;
        }

        private void ApplyOfficialFont(GameObject target)
        {
            if (target == null || font == null) return;
            foreach (TMP_Text text in target.GetComponentsInChildren<TMP_Text>(true))
                text.font = font;

            Font sourceFont = font.sourceFontFile;
            if (sourceFont == null) return;
            foreach (Text text in target.GetComponentsInChildren<Text>(true))
                text.font = sourceFont;
        }

        private static string SlotLabel(string id)
        {
            return int.TryParse(id.Substring(SlotPrefix.Length), out int number) ? "存档 " + number : id;
        }

        private static string[] FieldsForTab(string tab)
        {
            switch (tab)
            {
                case "整体": return new[] { "skin_color" };
                case "头发": return new[] { "hairstyle_id", "hair_color" };
                case "脸部": return new[] { "eyebrow_id", "eye_id", "eye_color", "mouth_id", "makeup_id" };
                case "特征": return new[] { "feature_id" };
                default: return new[] { "skin_color" };
            }
        }

        private static string FieldLabel(string field)
        {
            switch (field)
            {
                case "skin_color": return "肤色";
                case "hairstyle_id": return "发型";
                case "hair_color": return "发色";
                case "eyebrow_id": return "眉毛";
                case "eye_id": return "眼瞳";
                case "eye_color": return "瞳色";
                case "mouth_id": return "嘴型";
                case "makeup_id": return "妆容";
                case "feature_id": return "特征";
                default: return field;
            }
        }

        private static bool IsColorField(string field)
        {
            return field == "skin_color" || field == "hair_color" || field == "eye_color";
        }

        private static int DyeType(string field)
        {
            return field == "skin_color" ? 1 : field == "hair_color" ? 2 : 3;
        }

        private static string CurrentColor(CharacterAppearanceDraft appearance, int type)
        {
            return type == 1 ? appearance.skin_color : type == 2 ? appearance.hair_color : appearance.eye_color;
        }

        private static string CurrentOption(CharacterAppearanceDraft appearance, string field)
        {
            switch (field)
            {
                case "hairstyle_id": return appearance.hairstyle_id;
                case "eyebrow_id": return appearance.eyebrow_id;
                case "eye_id": return appearance.eye_id;
                case "mouth_id": return appearance.mouth_id;
                case "makeup_id": return appearance.makeup_id;
                case "feature_id": return appearance.feature_id;
                default: return string.Empty;
            }
        }

        private static string ShortOptionName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            int separator = name.LastIndexOf(" - ", StringComparison.Ordinal);
            return separator >= 0 ? name.Substring(separator + 3) : name;
        }

        private static Color ParseColor(string hex)
        {
            return ColorUtility.TryParseHtmlString("#" + hex, out Color color) ? color : Color.white;
        }
    }
}
