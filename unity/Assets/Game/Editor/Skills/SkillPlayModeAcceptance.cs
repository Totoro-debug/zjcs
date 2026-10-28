using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Offline.State;
using Zjcs.Presentation.Character;

namespace Zjcs.Editor.Skills
{
    public static class SkillPlayModeAcceptance
    {
        private const string ActiveKey = "Zjcs.Skills.Acceptance.Active";
        private const string RootKey = "Zjcs.Skills.Acceptance.Root";
        private const string SlotKey = "Zjcs.Skills.Acceptance.Slot";
        private const string ResultKey = "Zjcs.Skills.Acceptance.Passed";
        private const string ExitKey = "Zjcs.Skills.Acceptance.ExitPending";
        private static string ValidationDirectory {
            get {
                string path = Environment.GetEnvironmentVariable("ZJCS_VALIDATION_DIR");
                if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
                    throw new InvalidOperationException("Skills validation requires ZJCS_VALIDATION_DIR.");
                return path;
            }
        }
        private static bool started;
        private static bool reloadLocked;
        private static Exception runtimeFailure;
        [InitializeOnLoadMethod]
        private static void AutoRunInWindowedEditor()
        {
            if (SessionState.GetBool(ExitKey, false) || Environment.GetEnvironmentVariable("ZJCS_SKILLS_AUTORUN") != "1" || Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            EditorApplication.update -= AutoRunTick;
            EditorApplication.update += AutoRunTick;
        }

        private static void ConfigurePortraitGameView(int width, int height)
        {
            Assembly editor = typeof(UnityEditor.Editor).Assembly;
            Type sizesType = editor.GetType("UnityEditor.GameViewSizes");
            Type singleton = typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
            object sizes = singleton.GetProperty("instance").GetValue(null);
            object group = sizesType.GetMethod("GetGroup").Invoke(sizes,
                new[] { Enum.ToObject(editor.GetType("UnityEditor.GameViewSizeGroupType"), 1) });
            Type sizeType = editor.GetType("UnityEditor.GameViewSize");
            object size = Activator.CreateInstance(sizeType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new object[] { Enum.ToObject(editor.GetType("UnityEditor.GameViewSizeType"), 1), width, height, "Skills reference" }, null);
            group.GetType().GetMethod("AddCustomSize").Invoke(group, new[] { size });
            int index = (int)group.GetType().GetMethod("GetTotalCount").Invoke(group, null) - 1;
            Type viewType = editor.GetType("UnityEditor.GameView");
            EditorWindow view = EditorWindow.GetWindow(viewType, false, "Game", false);
            viewType.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).SetValue(view, index);
        }

        private static void AutoRunTick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;
            EditorApplication.update -= AutoRunTick;
            if (!SessionState.GetBool(ActiveKey, false))
                Run();
        }

        [MenuItem("Zjcs/Validate/Skills Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before running skill acceptance.");
            OfficialSkillAssetTools.Validate();
            ConfigurePortraitGameView(675, 1463);
            string root = Path.Combine(ValidationDirectory, "save-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            var slots = new ProfileSlotCatalog(root);
            string slot = slots.CreateSlot();
            var save = new OfflineMainlineSave();
            save.CreatedAtUnixSeconds = DateTimeOffset.Now.ToUnixTimeSeconds();
            save.Player.CharacterCreated = true;
            save.Player.CharacterName = "技能验收";
            save.Player.Level = 20;
            save.Player.Rank = 1;
            save.Player.SubRank = 2;
            save.Player.Wardrobe.Profession = 1;
            save.Player.Experience = OfficialCharacterAssetCatalog.LoadDefault().Load().CumulativeLevelExperience[save.Player.Level];
            save.Player.Inventory.Add(new OfflineItemStack { ItemId = 12000, Count = 120 });
            save.Player.Skills.Skills.Add(new OfflineOwnedSkill { ClassId = 10015, Rank = 1 });
            File.WriteAllText(slots.GetProgressPath(slot), JsonUtility.ToJson(save, true));
            SessionState.SetString(RootKey, root);
            SessionState.SetString(SlotKey, slot);
            SessionState.SetBool(ActiveKey, true);
            started = false;
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.isPlaying = true;
        }

        [InitializeOnLoadMethod]
        private static void RestoreAfterDomainReload()
        {
            if (SessionState.GetBool(ExitKey, false))
            {
                EditorApplication.update -= ExitWhenReady;
                EditorApplication.update += ExitWhenReady;
                return;
            }
            if (!SessionState.GetBool(ActiveKey, false))
                return;
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnUpdate()
        {
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying)
                return;
            EditorApplication.QueuePlayerLoopUpdate();
            if (started)
                return;
            started = true;
            EditorApplication.LockReloadAssemblies();
            reloadLocked = true;
            Application.runInBackground = true;
            runtimeFailure = null;
            Application.logMessageReceived -= CaptureRuntimeFailure;
            Application.logMessageReceived += CaptureRuntimeFailure;
            var host = new GameObject("SkillAcceptanceRunner");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<Runner>().StartCoroutine(Guarded(Scenario()));
        }

        private static void CaptureRuntimeFailure(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
                runtimeFailure = new InvalidOperationException(message + "\n" + stackTrace);
        }

        private static IEnumerator Guarded(IEnumerator scenario)
        {
            var stack = new System.Collections.Generic.Stack<IEnumerator>();
            stack.Push(scenario);
            while (stack.Count > 0)
            {
                object value;
                try
                {
                    if (runtimeFailure != null) throw runtimeFailure;
                    var current = stack.Peek();
                    if (!current.MoveNext()) { stack.Pop(); continue; }
                    value = current.Current;
                    if (value is IEnumerator child) { stack.Push(child); continue; }
                }
                catch (Exception exception)
                {
                    Finish(false, exception);
                    yield break;
                }

                yield return value;
            }
        }

        private static void ValidateOriginalDetailProperties(OfflineCharacterProgressionService progression)
        {
            Type type = typeof(CharacterSkillPanel).Assembly.GetType("Zjcs.Presentation.Character.OfficialSkillDetailProperties", true);
            object provider = Activator.CreateInstance(type, new object[] { progression.Catalog,
                Zjcs.Presentation.Skills.OfficialSkillAssetCatalog.LoadDefault() });
            MethodInfo calculate = type.GetMethod("Calculate");
            int count = 0;
            foreach (var skill in progression.SkillRules.Skills)
            {
                calculate.Invoke(provider, new object[] { skill.ClassId, 1, 1, 1, 4 });
                calculate.Invoke(provider, new object[] { skill.ClassId, 1, 20, 2, 4 });
                count++;
            }
            Require(count == 394, "Original detail property validation omitted skills: " + count);
            Debug.Log("[Skills] All 394 original skill property previews calculated at two progression states.");
        }

        private static IEnumerator Scenario()
        {
            while (GameObject.Find("OfficialSaveSelectionPanel") == null) yield return null;
            yield return null;
            foreach (Canvas existing in UnityEngine.Object.FindObjectsOfType<Canvas>(true))
                existing.gameObject.SetActive(false);
            foreach (GraphicRaycaster raycaster in UnityEngine.Object.FindObjectsOfType<GraphicRaycaster>(true))
                raycaster.enabled = false;
            foreach (Camera existing in UnityEngine.Object.FindObjectsOfType<Camera>(true))
                existing.enabled = false;
            string root = SessionState.GetString(RootKey, string.Empty);
            string slot = SessionState.GetString(SlotKey, string.Empty);
            ProfileSlotCatalog slots = null;
            OfflineCharacterProgressionService progression = null;
            CharacterSkillPanel panel = null;
            OfficialCharacterPanelView characterView = null;
            GameObject canvasObject = null;
            GameObject eventSystem = null;
            bool ownsEventSystem = false;
            var expectedRecommendationSkills = new System.Collections.Generic.HashSet<int>();
            slots = new ProfileSlotCatalog(root);
            OfflineMainlineProgressSession session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            OfficialCharacterAssetCatalog assets = OfficialCharacterAssetCatalog.LoadDefault();
            progression = new OfflineCharacterProgressionService(session, assets.Load());
            ProgressionResult wardrobe = progression.InitializeWardrobe("skills-acceptance-wardrobe");
            Require(wardrobe.Status != ProgressionResultStatus.InvalidState,
                "Wardrobe initialization failed: " + wardrobe.Message);
            OfficialSkillRecommendationSet recommendations = assets.LoadSkillRecommendations();
            progression.AttachSkillRules(assets.LoadSkillRules(), recommendations.recommendations);
            ValidateOriginalDetailProperties(progression);
            if (Environment.GetEnvironmentVariable("ZJCS_SKILLS_HOME_ONLY") == "1") {
                Require(session.ApplyCharacterMutation(player => {
                    player.Level = 40;
                    player.Experience = assets.Load().CumulativeLevelExperience[40];
                    player.Skills.Skills.Add(new OfflineOwnedSkill { ClassId = 10014, Rank = 1 });
                }, "skills-home-damage-fixture").IsApplied, "Could not save the damage-test fixture.");
                Require(progression.EquipSkill(10014, 0).IsApplied, "Could not prepare the damage-test character's active skill.");
                yield return ValidateHomeDamageTest(slot, root);
                Finish(true, null);
                yield break;
            }
            OfflineSkillPlayerContext skillContext = progression.SkillContext();
            foreach (OfficialSkillRecommendation recommendation in recommendations.recommendations.Where(value => value != null && skillContext.CurrentProfessionChain.Contains(value.Profession)))
            {
                foreach (int skillId in (recommendation.ActiveSkillList ?? Array.Empty<int>()).Concat(recommendation.PassiveSkillList ?? Array.Empty<int>()))
                {
                    OfflineSkillDefinition definition = progression.SkillDefinition(skillId);
                    if (skillId == 10000 || !OfflineSkillProgression.IsAvailableFor(definition, skillContext))
                        continue;
                    expectedRecommendationSkills.Add(skillId);
                    if (!progression.Player.Skills.Skills.Any(value => value.ClassId == skillId))
                        progression.Player.Skills.Skills.Add(new OfflineOwnedSkill { ClassId = skillId, Rank = 1 });
                }
            }

            Require(expectedRecommendationSkills.Count > 0, "The official profession chain has no available recommendation skills.");
            canvasObject = new GameObject("SkillsAcceptanceCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 0;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.matchWidthOrHeight = 1f;
            var backgroundObject = new GameObject("SkillAcceptanceBackground", typeof(RectTransform), typeof(Image));
            backgroundObject.transform.SetParent(canvas.transform, false);
            RectTransform backgroundRect = (RectTransform)backgroundObject.transform;
            backgroundRect.anchorMin = Vector2.zero;
            backgroundRect.anchorMax = Vector2.one;
            backgroundRect.offsetMin = Vector2.zero;
            backgroundRect.offsetMax = Vector2.zero;
            Image background = backgroundObject.GetComponent<Image>();
            background.color = new Color(0.055f, 0.075f, 0.08f);
            background.raycastTarget = false;
            var portraitObject = new GameObject("PortraitAcceptanceViewport", typeof(RectTransform));
            portraitObject.transform.SetParent(canvas.transform, false);
            RectTransform portrait = (RectTransform)portraitObject.transform;
            portrait.anchorMin = Vector2.zero;
            portrait.anchorMax = Vector2.one;
            portrait.pivot = new Vector2(0.5f, 0.5f);
            portrait.offsetMin = Vector2.zero;
            portrait.offsetMax = Vector2.zero;
            EventSystem existingEventSystem = UnityEngine.Object.FindObjectsOfType<EventSystem>().FirstOrDefault(value => value.isActiveAndEnabled);
            if (existingEventSystem != null)
            {
                eventSystem = existingEventSystem.gameObject;
                BaseInputModule inputModule = existingEventSystem.GetComponent<BaseInputModule>();
                if (inputModule == null)
                    inputModule = existingEventSystem.gameObject.AddComponent<StandaloneInputModule>();
                inputModule.enabled = true;
            }
            else
            {
                eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                ownsEventSystem = true;
            }

            var characterObject = new GameObject("OfficialCharacterAcceptancePage", typeof(RectTransform));
            characterObject.transform.SetParent(portrait, false);
            characterView = characterObject.AddComponent<OfficialCharacterPanelView>();
            characterView.Initialize(assets, progression, slot, root);
            panel = characterView.GetComponentInChildren<CharacterSkillPanel>(true);
            Require(panel != null, "The character page did not create its official skill controller.");
            yield return null;
            SimulatePointerClick(FindButton("CharacterPanelSkillBtn"));
            Require(panel.IsOpen, "The simulated character-page skill entry did not open the full skill page.");
            Require(characterView.GetComponentInChildren<TMPro.TMP_InputField>(true) != null,
                "The official plan name input did not bind.");
            foreach (GraphicRaycaster raycaster in UnityEngine.Object.FindObjectsOfType<GraphicRaycaster>(true))
                if (raycaster.transform.root != canvasObject.transform)
                    raycaster.enabled = false;
            // The official show animation starts the bottom bar at alpha zero.
            float openingDeadline = Time.realtimeSinceStartup + 0.75f;
            while (Time.realtimeSinceStartup < openingDeadline)
                yield return null;
            Canvas.ForceUpdateCanvases();
            RawImage head = characterView.GetComponentsInChildren<RawImage>(true).First(value => value.name == "HeadImg");
            Texture2D headTexture = head.texture as Texture2D;
            Require(headTexture != null && headTexture.GetPixels32().Count(value => value.a > 10) > 100,
                "The original shared player portrait rendered an empty image.");
            File.WriteAllBytes(Path.Combine(ValidationDirectory, "zjcs-skills-head.png"), headTexture.EncodeToPNG());
            ScreenCapture.CaptureScreenshot(Path.Combine(ValidationDirectory, "zjcs-skills-opening.png"));
            yield return null;
            if (Environment.GetEnvironmentVariable("ZJCS_SKILLS_ATTACHMENTS_ONLY") == "1") {
                yield return ValidateListAttachments(session, progression, panel);
                Finish(true, null);
                yield break;
            }
            var planInput = characterView.GetComponentsInChildren<TMPro.TMP_InputField>(true).Single(value => value.name == "SkillPlanName");
            Require(planInput.characterLimit == 0 && planInput.interactable, "Original TMP plan input settings were not restored.");
            string originalPlanName = progression.Player.Skills.Plans[progression.Player.Skills.SelectedPlan].Name;
            foreach (string candidate in new[] { "中文六字方案", "中文七字方案名", "abc def", "abcdefghijkl", originalPlanName }) {
                SimulatePointerClick(planInput);
                yield return null;
                Require(planInput.isFocused, "Simulated mouse input did not focus the original plan input.");
                planInput.ProcessEvent(new Event { type = EventType.KeyDown, keyCode = KeyCode.A, modifiers = EventModifiers.Control });
                foreach (char character in candidate)
                    planInput.ProcessEvent(new Event { type = EventType.KeyDown, character = character });
                SimulatePointerClick(FindButton("NextPlanBtn"));
                yield return null;
                SimulatePointerClick(FindButton("PrevPlanBtn"));
                yield return null;
                bool valid = progression.SkillProgression.CheckPlanName(candidate) == 0;
                if (valid) originalPlanName = candidate;
                Require(planInput.text == originalPlanName && progression.Player.Skills.Plans[progression.Player.Skills.SelectedPlan].Name == originalPlanName,
                    "Official plan name acceptance/rejection differed after keyboard input and switching plans: " + candidate);
                var saved = JsonUtility.FromJson<OfflineMainlineSave>(File.ReadAllText(new ProfileSlotCatalog(root).GetProgressPath(slot)));
                Require(saved.Player.Skills.Plans[progression.Player.Skills.SelectedPlan].Name == originalPlanName, "The renamed plan was not restored from disk.");
            }
            ScreenCapture.CaptureScreenshot(Path.Combine(ValidationDirectory, "zjcs-skills-plan-input.png"));
            yield return null;
            Button entrySkill = FindVisibleOwnedSkillRowButton("SkillListItem", progression, 10015, out _);
            Require(entrySkill != null, "The owned skill with original entry links is missing.");
            SimulatePointerClick(entrySkill);
            yield return new WaitForSecondsRealtime(.7f);
            var linkedText = UnityEngine.Object.FindObjectsOfType<TMPro.TMP_Text>()
                .First(value => value.name == "FuncDetailText" && value.isActiveAndEnabled);
            linkedText.ForceMeshUpdate();
            Require(linkedText.textInfo.linkCount > 0, "Original skill description lost its entry hyperlinks.");
            var link = linkedText.textInfo.linkInfo[0];
            var firstCharacter = linkedText.textInfo.characterInfo[link.linkTextfirstCharacterIndex];
            Vector3 localLinkPoint = (firstCharacter.bottomLeft + firstCharacter.topRight) * .5f;
            Vector2 linkPoint = RectTransformUtility.WorldToScreenPoint(null, linkedText.transform.TransformPoint(localLinkPoint));
            SimulatePointerClickAt(linkedText.gameObject, linkPoint);
            yield return new WaitForSecondsRealtime(.7f);
            var entryTip = UnityEngine.Object.FindObjectsOfType<Transform>()
                .FirstOrDefault(value => value.name == "skill_entry_tip" && value.gameObject.activeInHierarchy);
            Require(entryTip != null && entryTip.GetComponentsInChildren<TMPro.TMP_Text>()
                .Any(value => !string.IsNullOrEmpty(value.text)), "Clicking an original entry link did not populate its native tip.");
            ScreenCapture.CaptureScreenshot(Path.Combine(ValidationDirectory, "skills-entry-tip.png"));
            yield return new WaitForEndOfFrame();
            Require(panel.HandleEscape(), "The original entry tip did not close.");
            yield return null;
            Require(panel.HandleEscape(), "The underlying skill detail did not close.");
            yield return null;
            Button returnButton = FindButton("CloseBtn_dark");
            Button skillPageTab = FindButton("CharacterPanelSkillDetailPanelSkillTabGroupItem_11");
            Button achievementPageTab = FindButton("AchievementTabGroupItem_11");
            RectTransform skillContent = UnityEngine.Object.FindObjectsOfType<RectTransform>(true).FirstOrDefault(value => value.name == "SkillContentPanel");
            RectTransform achievementContent = UnityEngine.Object.FindObjectsOfType<RectTransform>(true).FirstOrDefault(value => value.name == "AchievementPanelWithTypes");
            Require(returnButton != null && skillPageTab != null && achievementPageTab != null, "The official full-screen skill panel frame or bottom tabs are missing.");
            Require(skillContent != null && skillContent.gameObject.activeInHierarchy && achievementContent != null && !achievementContent.gameObject.activeInHierarchy, "The official skill page did not start in the skills state.");
            yield return null;
            progression.Player.Skills.SeniorMaterialSpentOnLevelUp = 500;
            long giftBefore = progression.Player.Inventory.Where(value => value.ItemId == 41300).Sum(value => value.Count);
            SimulatePointerClick(achievementPageTab);
            Require(!skillContent.gameObject.activeInHierarchy && achievementContent.gameObject.activeInHierarchy, "The official gift tab did not switch to its Achievement state.");
            yield return null;
            Button giftIcon = achievementContent.GetComponentsInChildren<Button>()
                .First(value => value.name == "ItemIconDefault");
            SimulatePointerClick(giftIcon);
            yield return new WaitForSecondsRealtime(.7f);
            var giftDetail = UnityEngine.Object.FindObjectsOfType<Transform>()
                .FirstOrDefault(value => value.name == "skill_gift_item_detail" && value.gameObject.activeInHierarchy);
            Require(giftDetail != null && giftDetail.GetComponentsInChildren<TMPro.TMP_Text>()
                .Any(value => !string.IsNullOrEmpty(value.text)), "A gift reward icon did not open its native item detail.");
            ScreenCapture.CaptureScreenshot(Path.Combine(ValidationDirectory, "skills-gift-item-detail.png"));
            yield return new WaitForEndOfFrame();
            Require(giftDetail.GetComponentsInChildren<TMPro.TMP_Text>().All(value => !value.text.Contains("{0}")),
                "A visible gift item label retains its original formatting placeholder.");
            SimulatePointerClick(UnityEngine.Object.FindObjectsOfType<Button>().First(value => value.name == "BlurMask"));
            yield return null;
            Button giftReceive = achievementContent.GetComponentsInChildren<Button>(false).First(value => value.name == "ReceiveBtn");
            SimulatePointerClick(giftReceive);
            yield return null;
            Require(progression.Player.Inventory.Where(value => value.ItemId == 41300).Sum(value => value.Count) == giftBefore + 60,
                "The original skill-material gift did not atomically credit its 60-item reward.");
            Require(progression.Player.AppliedCharacterEventIds.Contains("skill-achievement:33:0")
                && !progression.ClaimSkillAchievements(33).IsApplied, "A skill gift reward can be claimed repeatedly.");
            yield return new WaitForSecondsRealtime(.4f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(ValidationDirectory, "zjcs-skills-gifts.png"));
            yield return null;
            Button qualityGift = achievementContent.GetComponentsInChildren<Button>(false)
                .First(value => value.name == "AchievementTabGroupItem_14 (1)");
            SimulatePointerClick(qualityGift);
            yield return new WaitForSecondsRealtime(.4f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(ValidationDirectory, "zjcs-skills-quality-gifts.png"));
            yield return null;
            SimulatePointerClick(skillPageTab);
            Require(skillContent.gameObject.activeInHierarchy && !achievementContent.gameObject.activeInHierarchy, "The official skill tab did not restore its skill content.");
            float animationDeadline = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < animationDeadline)
                yield return null;
            string screenshot = Path.Combine(ValidationDirectory, "zjcs-skills-playmode.png");
            int selectedPlan = progression.Player.Skills.SelectedPlan;
            ScreenCapture.CaptureScreenshot(screenshot);
            Button nextPlanButton = FindButton("NextPlanBtn");
            Require(nextPlanButton != null, "Skill plan switch button is missing.");
            Vector2 buttonPosition = RectTransformUtility.WorldToScreenPoint(null, nextPlanButton.transform.position);
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = buttonPosition
            };
            var raycasts = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, raycasts);
            string topHit = raycasts.Count == 0 ? "<none>" : raycasts[0].gameObject.name;
            Debug.Log("[Skills acceptance] skill panel ready; mouse target=" + buttonPosition + "; screen=" + Screen.width + "x" + Screen.height + "; hit=" + topHit + "; button=" + nextPlanButton.interactable + "; screenshot=" + screenshot);
            yield return null;
            SimulatePointerClick(nextPlanButton);
            Require(progression.Player.Skills.Plans.Length == 7, "The original seven skill plans were not restored.");
            var planBar = nextPlanButton.transform.parent.GetComponentInChildren<Scrollbar>(true);
            Require(planBar != null && planBar.numberOfSteps == 7 && Mathf.Approximately(planBar.size, .2f)
                && !planBar.interactable, "The original plan scrollbar binding is incomplete.");
            for (int i = 0; i < 7; i++)
            {
                int before = progression.Player.Skills.SelectedPlan;
                SimulatePointerClick(nextPlanButton);
                yield return null;
                int after = progression.Player.Skills.SelectedPlan;
                Require(after == (before + 1) % 7 && Mathf.Approximately(planBar.value, after / 6f),
                    "Seven-plan pointer navigation or scrollbar position is incorrect.");
            }
            GameObject recommendationDialog = null;
            Button applyRecommendation = null;
            string recommendationScreenshot = Path.Combine(ValidationDirectory, "zjcs-skills-recommend.png");
            Require(progression.Player.Skills.SelectedPlan != selectedPlan, "A simulated Unity pointer click did not switch the skill plan.");
            Button recommendButton = FindButton("RecommendBtn");
            Require(recommendButton != null, "The skill recommendation button is missing.");
            yield return null;
            SimulatePointerClick(recommendButton);
            yield return null;
            recommendationDialog = UnityEngine.Object.FindObjectsOfType<Transform>(true).FirstOrDefault(value => value.name == "SkillRecommendDialog")?.gameObject;
            Require(recommendationDialog != null && recommendationDialog.activeInHierarchy, "The official skill recommendation dialog did not open.");
            applyRecommendation = FindRaycastableButton("SaveBtn");
            Require(applyRecommendation != null, "The official recommendation item has no active apply button.");
            ScreenCapture.CaptureScreenshot(recommendationScreenshot);
            yield return new WaitForEndOfFrame();
            yield return null;
            SimulatePointerClick(applyRecommendation);
            yield return new WaitForEndOfFrame();
            OfflineSkillPlan recommendedPlan = progression.Player.Skills.Plans[progression.Player.Skills.SelectedPlan];
            Require(recommendedPlan.ActiveSkills.Concat(recommendedPlan.PassiveSkills).Any(expectedRecommendationSkills.Contains), "The official recommendation did not install an available owned skill: " + panel.LastMessage);
            Require(recommendationDialog == null || !recommendationDialog.activeInHierarchy, "Applying an official recommendation did not close its dialog: " + panel.LastMessage);
            Debug.Log("[Skills acceptance] official recommendation rendered and applied by simulated pointer click. Screenshot: " + recommendationScreenshot);
            Button firstActiveSlot = FindFirstSkillSlot("ActiveSkill");
            Require(firstActiveSlot != null, "The official active skill slot selector is missing.");
            yield return null;
            SimulatePointerClick(firstActiveSlot);
            Canvas.ForceUpdateCanvases();
            Require(UnityEngine.Object.FindObjectsOfType<RectTransform>(true).Any(value => value.name == "skill_panel_detail" && value.gameObject.activeInHierarchy), "The equipped slot did not open its official detail.");
            Require(panel.HandleEscape(), "The equipped skill detail did not handle Escape.");
            yield return null;
            Require(UnityEngine.Object.FindObjectsOfType<RectTransform>(true).Any(value => value.name == "skillGrid" && value.gameObject.activeInHierarchy), "Selecting a skill slot did not reveal the official skill list.");
            Button showAll = FindButton("ShowAll");
            Toggle showAllToggle = showAll == null ? null : showAll.GetComponent<Toggle>();
            if (showAllToggle == null)
                showAllToggle = UnityEngine.Object.FindObjectsOfType<Toggle>(true).FirstOrDefault(value => value.name == "ShowAll");
            Require(showAllToggle != null, "The official show-all skill filter is missing.");
            bool wasShowingAll = showAllToggle.isOn;
            yield return null;
            SimulatePointerClick(showAllToggle);
            Require(showAllToggle.isOn != wasShowingAll, "The simulated pointer did not toggle the show-all skill filter.");
            int passiveSkillId = expectedRecommendationSkills.FirstOrDefault(value => progression.SkillDefinition(value)?.ItemType == 101);
            Require(passiveSkillId > 0, "The official profession chain has no passive recommendation skill.");
            OfflineSkillPlan passivePlan = progression.Player.Skills.Plans[progression.Player.Skills.SelectedPlan];
            if (passivePlan.PassiveSkills.Contains(passiveSkillId))
            {
                int equippedPassiveSlot = Array.IndexOf(passivePlan.PassiveSkills, passiveSkillId);
                ProgressionResult unequippedPassive = progression.UnequipSkill(101, equippedPassiveSlot);
                Require(unequippedPassive.IsApplied, "Could not expose an owned passive skill in its category list: " + unequippedPassive.Message);
            }

            Button passiveTab = FindButtonContaining("PassiveSkillTab");
            Require(passiveTab != null, "The official passive skill tab is missing.");
            yield return null;
            SimulatePointerClick(passiveTab);
            bool renderedPassive = UnityEngine.Object.FindObjectsOfType<Button>(true).Where(value => value.name.StartsWith("SkillListItem_", StringComparison.Ordinal) && value.gameObject.activeInHierarchy).Select(value => int.TryParse(value.name.Substring("SkillListItem_".Length), out int id) ? id : 0).Any(id => progression.SkillDefinition(id)?.ItemType == 101);
            Require(renderedPassive, "The simulated passive tab click did not render any official passive skill row.");
            Button activeTab = FindButtonContaining("ActiveSkillTab");
            Require(activeTab != null, "The official active skill tab is missing.");
            yield return null;
            SimulatePointerClick(activeTab);
            bool hasMultiItemOfficialRow = UnityEngine.Object.FindObjectsOfType<RectTransform>(true).Where(value => value.name == "CharacterPanelSkillListRow" && value.gameObject.activeInHierarchy).Any(row => row.GetComponentsInChildren<Transform>(true).Count(child => child.name.StartsWith("SkillListItem_", StringComparison.Ordinal)) > 1);
            Require(hasMultiItemOfficialRow, "The official adaptive skill row did not place multiple skill icons on one row.");
            Button pieceTab = FindButtonContaining("SkillPieceTab");
            Require(pieceTab != null, "The official skill piece tab is missing.");
            yield return null;
            SimulatePointerClick(pieceTab);
            RectTransform pieceList = UnityEngine.Object.FindObjectsOfType<RectTransform>(true).FirstOrDefault(value => value.name == "SkillPieceScrollList");
            RectTransform skillList = UnityEngine.Object.FindObjectsOfType<RectTransform>(true).FirstOrDefault(value => value.name == "SkillScrollList");
            Require(pieceList != null && pieceList.gameObject.activeInHierarchy && skillList != null && !skillList.gameObject.activeInHierarchy, "The skill piece tab did not show its official list.");
            Canvas.ForceUpdateCanvases();
            ScrollRect pieceScroll = UnityEngine.Object.FindObjectsOfType<ScrollRect>(true).FirstOrDefault(value => value.name == "SkillPieceScrollList" && value.isActiveAndEnabled);
            if (pieceScroll != null)
                pieceScroll.verticalNormalizedPosition = 1f;
            Canvas.ForceUpdateCanvases();
            Button pieceRow = UnityEngine.Object.FindObjectsOfType<Button>(true).FirstOrDefault(value => value.name.StartsWith("CharacterPanelSkillDetailPanelSkillPieceItem", StringComparison.Ordinal) && value.gameObject.activeInHierarchy);
            Require(pieceRow != null, "The owned skill piece did not render as an interactive list row.");
            yield return null;
            SimulatePointerClick(pieceRow);
            Require(UnityEngine.Object.FindObjectsOfType<Text>(true).Any(value => value.name == "CountText" && value.isActiveAndEnabled), "The skill piece detail did not open from a simulated pointer click.");
            Require(panel.HandleEscape(), "The skill piece detail did not handle Escape.");
            yield return null;
            SimulatePointerClick(activeTab);
            yield return null;
            Canvas.ForceUpdateCanvases();
            yield return new WaitForSecondsRealtime(0.4f);
            ScreenCapture.CaptureScreenshot(Path.Combine(ValidationDirectory, "zjcs-skills-reference-match.png"));
            Button detailButton = FindVisibleOwnedSkillRowButton("SkillListItem", progression, 0, out _);
            Require(detailButton != null, "The skill list detail button is missing after slot selection.");
            yield return null;
            SimulatePointerClick(detailButton);
            Require(UnityEngine.Object.FindObjectsOfType<TMPro.TMP_Text>(true).Any(value => value.name == "FuncDetailText" && value.isActiveAndEnabled
                && !value.text.Contains("{0}")), "The original skill function description did not render after a simulated pointer click.");
            int favoriteSkillId = int.Parse(detailButton.name.Substring("SkillListItem_".Length));
            Button favoriteButton = FindButton("CollectionBtn");
            Require(favoriteButton != null, "The official skill detail collection button is missing.");
            yield return new WaitForSecondsRealtime(.4f);
            SimulatePointerClick(favoriteButton);
            Require(progression.Player.Skills.FavoriteSkills.Contains(favoriteSkillId), "The official skill detail collection control did not update the favorite state: " + panel.LastMessage);
            yield return new WaitForSecondsRealtime(.3f);
            Require(UnityEngine.Object.FindObjectsOfType<Text>().Any(value => value.text == "已置顶该技能" && value.isActiveAndEnabled),
                "The original favorite system message did not render.");
            ScreenCapture.CaptureScreenshot(Path.Combine(ValidationDirectory, "zjcs-skills-system-message.png"));
            Require(UnityEngine.Object.FindObjectsOfType<RectTransform>(true).Any(value => value.name == "CharacterPanelSkillListSeparator" && value.gameObject.activeInHierarchy), "Favoriting a skill did not restore the official separator between collected and uncollected skills.");
            Require(panel.IsOpen, "Collecting a skill closed the full skill page.");
            Require(favoriteButton != null && favoriteButton.gameObject.activeInHierarchy,
                "Collecting a skill unexpectedly closed its original detail dialog.");
            yield return new WaitForSecondsRealtime(.6f);
            Button outsideMask = UnityEngine.Object.FindObjectsOfType<Button>().First(value => value.name == "BlurMask");
            Canvas maskCanvas = outsideMask.GetComponent<Canvas>();
            Canvas detailCanvas = favoriteButton.GetComponentInParent<Canvas>();
            Require(maskCanvas.overrideSorting && maskCanvas.sortingOrder == detailCanvas.sortingOrder - 1,
                "The original popup mask is not sorted immediately below its window.");
            SimulatePointerClick(outsideMask);
            yield return null;
            Require(!UnityEngine.Object.FindObjectsOfType<TMPro.TMP_Text>().Any(value => value.name == "FuncDetailText" && value.isActiveAndEnabled),
                "An outside pointer click did not close the original skill detail popup.");
            Require(panel.IsOpen, "Closing the skill detail by its original mask also closed the skills page.");
            OfflineSkillPlan dragPlan = progression.Player.Skills.Plans[progression.Player.Skills.SelectedPlan];
            int firstActive = Array.FindIndex(dragPlan.ActiveSkills, value => value > 0);
            int secondActive = Array.FindIndex(dragPlan.ActiveSkills, firstActive + 1, value => value > 0);
            if (firstActive >= 0 && secondActive < 0)
            {
                int extraSkill = expectedRecommendationSkills.FirstOrDefault(value => progression.SkillDefinition(value)?.ItemType == 100 && !dragPlan.ActiveSkills.Contains(value));
                int emptySlot = Array.FindIndex(dragPlan.ActiveSkills, value => value == 0);
                if (extraSkill > 0 && emptySlot >= 0)
                {
                    ProgressionResult seeded = progression.EquipSkill(extraSkill, emptySlot);
                    Require(seeded.IsApplied, "Could not prepare two owned active skills for the drag test.");
                    int selected = progression.Player.Skills.SelectedPlan;
                    yield return null;
                    SimulatePointerClick(FindButton("NextPlanBtn"));
                    yield return null;
                    SimulatePointerClick(FindButton("PrevPlanBtn"));
                    Require(progression.Player.Skills.SelectedPlan == selected, "The skill plan controls did not restore the drag-test plan.");
                    dragPlan = progression.Player.Skills.Plans[selected];
                    firstActive = Array.FindIndex(dragPlan.ActiveSkills, value => value > 0);
                    secondActive = Array.FindIndex(dragPlan.ActiveSkills, firstActive + 1, value => value > 0);
                }
            }

            Require(firstActive >= 0 && secondActive >= 0, "The official recommendation did not provide two active skills for drag validation.");
            RectTransform activeSkillGroup = UnityEngine.Object.FindObjectsOfType<RectTransform>(true).FirstOrDefault(value => value.name == "ActiveSkill");
            RectTransform[] activeSlots = activeSkillGroup.Cast<Transform>().OfType<RectTransform>().Where(value => value.name.StartsWith("SkillContentPanelSkillGroupItem", StringComparison.Ordinal)).Take(4).ToArray();
            Require(activeSlots.Length > secondActive, "The official active skill slots were not rendered for drag validation.");
            int firstSkillBeforeDrag = dragPlan.ActiveSkills[firstActive];
            int secondSkillBeforeDrag = dragPlan.ActiveSkills[secondActive];
            SimulateSkillSlotDrag(activeSlots[firstActive].gameObject, activeSlots[secondActive].gameObject);
            OfflineSkillPlan swappedPlan = progression.Player.Skills.Plans[progression.Player.Skills.SelectedPlan];
            Require(swappedPlan.ActiveSkills[firstActive] == secondSkillBeforeDrag && swappedPlan.ActiveSkills[secondActive] == firstSkillBeforeDrag, "A simulated Unity drag did not swap the two active skill slots: " + panel.LastMessage);
            OfflineSkillLevelCost nextSlotCost = progression.SkillRules.SlotLevelCosts.FirstOrDefault(value => value.Level == 2);
            Require(nextSlotCost != null && nextSlotCost.Items.Length > 0, "The official level-two slot cultivation cost is missing.");
            foreach (OfflineSkillItemCount cost in nextSlotCost.Items)
            {
                OfflineItemStack stack = progression.Player.Inventory.FirstOrDefault(value => value.ItemId == cost.ItemId);
                if (stack == null)
                    progression.Player.Inventory.Add(new OfflineItemStack { ItemId = cost.ItemId, Count = cost.Count });
                else
                    stack.Count = Math.Max(stack.Count, cost.Count);
            }

            int levelBeforeUpgrade = progression.SkillProgression.SlotLevel(progression.Player.Skills, 100, 0, progression.SkillContext().CurrentSeasonId);
            yield return null;
            RectTransform currentActiveGroup = UnityEngine.Object.FindObjectsOfType<RectTransform>(true)
                .First(value => value.name == "ActiveSkill" && value.gameObject.activeInHierarchy);
            RectTransform currentActiveSlot = currentActiveGroup.Cast<Transform>().OfType<RectTransform>()
                .First(value => value.name.StartsWith("SkillContentPanelSkillGroupItem", StringComparison.Ordinal));
            SimulatePointerClick(currentActiveSlot.Find("Slot").GetComponent<Button>());
            yield return new WaitForSecondsRealtime(.4f);
            SimulatePointerClick(FindButton("UpgradeBtn"));
            yield return null;
            SimulatePointerClick(FindButton("SkillPromotePanelLevelUpTabGroupItem_11"));
            Button slotUpgradeButton = UnityEngine.Object.FindObjectsOfType<Button>(true).FirstOrDefault(value => value.name == "UseItemBtn" && value.isActiveAndEnabled && value.GetComponentsInParent<Transform>(true).Any(parent => parent.name == "LevelUpContent"));
            Require(slotUpgradeButton != null, "The official level cultivation button is missing.");
            yield return null;
            SimulatePointerClick(slotUpgradeButton);
            int levelAfterUpgrade = progression.SkillProgression.SlotLevel(progression.Player.Skills, 100, 0, progression.SkillContext().CurrentSeasonId);
            Require(levelAfterUpgrade == levelBeforeUpgrade + 1, "A simulated pointer click did not apply the official slot cultivation cost: level " + levelBeforeUpgrade + " -> " + levelAfterUpgrade + "; " + panel.LastMessage + "; slot=" + currentActiveSlot.name);
            Require(panel.HandleEscape(), "The skill cultivation panel did not close.");
            OfflineSkillPlan promotionPlan = progression.Player.Skills.Plans[progression.Player.Skills.SelectedPlan];
            int promotionSkillId = expectedRecommendationSkills.FirstOrDefault(value =>
            {
                OfflineSkillDefinition candidate = progression.SkillDefinition(value);
                int rank = OfflineSkillProgression.OwnedRank(progression.Player.Skills, value);
                return candidate != null && candidate.ItemType == 100 && candidate.RankCosts?.Any(cost => cost.CurrentRank == rank) == true && !promotionPlan.ActiveSkills.Contains(value) && !promotionPlan.PassiveSkills.Contains(value);
            });
            OfflineSkillDefinition promotionDefinition = progression.SkillDefinition(promotionSkillId);
            int rankBeforePromotion = OfflineSkillProgression.OwnedRank(progression.Player.Skills, promotionSkillId);
            OfflineSkillRankCost rankCost = promotionDefinition?.RankCosts?.FirstOrDefault(value => value.CurrentRank == rankBeforePromotion);
            Require(promotionSkillId > 0 && rankCost != null, "The official profession chain has no un-equipped skill eligible for rank promotion.");
            foreach (OfflineSkillItemCount cost in rankCost.Items)
            {
                OfflineItemStack stack = progression.Player.Inventory.FirstOrDefault(value => value.ItemId == cost.ItemId);
                if (stack == null)
                    progression.Player.Inventory.Add(new OfflineItemStack { ItemId = cost.ItemId, Count = cost.Count });
                else
                    stack.Count = Math.Max(stack.Count, cost.Count);
            }

            System.Collections.Generic.Dictionary<string, object> promotionItem = progression.Catalog.FindItem(promotionSkillId);
            Require(promotionItem != null, "The rank promotion skill has no official item metadata.");
            string promotionSkillName = Convert.ToString(promotionItem["Name"], System.Globalization.CultureInfo.InvariantCulture);
            Button promotionSlot = FindFirstSkillSlot("ActiveSkill");
            Require(promotionSlot != null, "The official skill slot selector is missing before rank promotion.");
            yield return null;
            SimulatePointerClick(promotionSlot);
            Canvas.ForceUpdateCanvases();
            Button promotionRowButton = FindVisibleOwnedSkillRowButton("SkillListItem", progression, promotionSkillId, out _);
            Require(promotionRowButton != null, "The owned skill rank promotion entry is missing: " + promotionSkillName + ".");
            yield return null;
            SimulatePointerClick(promotionRowButton);
            Button skillUpgradeButton = FindButton("UpgradeBtn");
            Require(skillUpgradeButton != null && skillUpgradeButton.isActiveAndEnabled, "The official skill detail rank promotion action is missing.");
            yield return new WaitForSecondsRealtime(.4f);
            ScreenCapture.CaptureScreenshot(Path.Combine(ValidationDirectory, "skills-rank-detail.png"));
            yield return new WaitForEndOfFrame();
            SimulatePointerClick(skillUpgradeButton);
            Button rankUpgradeButton = UnityEngine.Object.FindObjectsOfType<Button>(true).FirstOrDefault(value => value.name == "UseItemBtn" && value.transform.parent != null && value.transform.parent.parent != null && value.transform.parent.parent.name == "RankUpContent");
            Require(rankUpgradeButton != null && rankUpgradeButton.interactable, "The official rank promotion button is missing or disabled.");
            yield return new WaitForSecondsRealtime(.6f);
            SimulatePointerClick(rankUpgradeButton);
            Require(OfflineSkillProgression.OwnedRank(progression.Player.Skills, promotionSkillId) == rankBeforePromotion + 1, "A simulated pointer click did not apply the official skill rank promotion: " + panel.LastMessage);
            Require(UnityEngine.Object.FindObjectsOfType<Text>(true).Any(value => value.name == "SkillNameText" && value.isActiveAndEnabled), "The official rank promotion result dialog did not render.");
            Require(panel.HandleEscape(), "The rank promotion result did not handle Escape.");
            OfflineSkillProfile beforeInsufficientPromotion = progression.Player.Skills.Clone();
            OfflineOwnedSkill rollbackCandidate = progression.Player.Skills.Skills.FirstOrDefault(value => value != null && value.ClassId != promotionSkillId && progression.SkillDefinition(value.ClassId)?.RankCosts?.Any(cost => cost.CurrentRank == value.Rank) == true);
            if (rollbackCandidate != null)
            {
                OfflineSkillPlayerContext rollbackContext = progression.SkillContext();
                int rollbackRank = rollbackCandidate.Rank;
                OfflineSkillProfile emptyInventory = beforeInsufficientPromotion.Clone();
                emptyInventory.Items.Clear();
                OfflineSkillResult rejected = progression.SkillProgression.PromoteSkill(emptyInventory, rollbackContext, rollbackCandidate.ClassId);
                Require(rejected.Code == OfflineSkillResultCode.InsufficientItems, "Promotion with no materials was not rejected as insufficient.");
                Require(emptyInventory.Skills.First(value => value.ClassId == rollbackCandidate.ClassId).Rank == rollbackRank && emptyInventory.Items.Count == 0, "A rejected rank promotion partially changed the skill profile or inventory.");
            }

            var batchCosts = new System.Collections.Generic.Dictionary<int, long>();
            foreach (OfflineOwnedSkill owned in progression.Player.Skills.Skills.Where(value => value != null))
            {
                OfflineSkillDefinition definition = progression.SkillDefinition(owned.ClassId);
                if (definition?.RankCosts == null)
                    continue;
                foreach (OfflineSkillRankCost cost in definition.RankCosts.Where(value => value.CurrentRank >= owned.Rank))
                    foreach (OfflineSkillItemCount itemCost in cost.Items ?? Array.Empty<OfflineSkillItemCount>())
                    {
                        if (!batchCosts.ContainsKey(itemCost.ItemId))
                            batchCosts[itemCost.ItemId] = 0;
                        batchCosts[itemCost.ItemId] += itemCost.Count;
                    }
            }

            foreach (var itemCost in batchCosts)
            {
                OfflineItemStack stack = progression.Player.Inventory.FirstOrDefault(value => value.ItemId == itemCost.Key);
                if (stack == null)
                    progression.Player.Inventory.Add(new OfflineItemStack { ItemId = itemCost.Key, Count = itemCost.Value });
                else
                    stack.Count = Math.Max(stack.Count, itemCost.Value);
            }

            yield return null;
            SimulatePointerClick(FindButton("NextPlanBtn"));
            yield return null;
            SimulatePointerClick(FindButton("PrevPlanBtn"));
            yield return null;
            System.Collections.Generic.Dictionary<int, int> ranksBeforeBatch = progression.Player.Skills.Skills.Where(value => value != null).ToDictionary(value => value.ClassId, value => value.Rank);
            Button fastPromotion = FindButton("FastPromotionBtn");
            Require(fastPromotion != null && fastPromotion.interactable, "The official batch promotion button is missing or disabled.");
            yield return null;
            SimulatePointerClick(fastPromotion);
            var batchDialog = UnityEngine.Object.FindObjectsOfType<Transform>(true).FirstOrDefault(value => value.name == "skill_batch_notify" && value.gameObject.activeInHierarchy);
            Require(batchDialog != null, "Batch promotion did not open its official result dialog.");
            bool batchChanged = progression.Player.Skills.Skills.Any(value => value != null && ranksBeforeBatch.TryGetValue(value.ClassId, out int oldRank) && value.Rank > oldRank);
            Require(batchChanged, "Batch promotion did not change any owned skill rank.");
            yield return new WaitForSecondsRealtime(1f);
            float batchReadyDeadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < batchReadyDeadline
                && !(FindButton("SkipBtn")?.isActiveAndEnabled ?? false)
                && !(FindButton("CloseObj")?.isActiveAndEnabled ?? false)) yield return null;
            Button skipBatch = FindButton("SkipBtn");
            if (skipBatch != null && skipBatch.isActiveAndEnabled)
            {
                SimulatePointerClick(skipBatch);
                Button closeBatch = FindButton("CloseObj");
                Require(closeBatch != null && closeBatch.gameObject.activeInHierarchy, "Skipping the batch result animation did not reveal its close control.");
                yield return new WaitForSecondsRealtime(1.4f);
                SimulatePointerClick(closeBatch);
            }
            else
            {
                Button closeBatch = FindButton("CloseObj");
                Require(closeBatch != null && closeBatch.isActiveAndEnabled, "The batch result did not finish preparing its controls.");
                yield return new WaitForSecondsRealtime(1.4f);
                SimulatePointerClick(closeBatch);
            }
            yield return null;
            Require(batchDialog == null || !batchDialog.gameObject.activeInHierarchy, "The batch result remained open after clicking its close control.");

            // Exercise the original skin dialog through actual pointer hit testing.
            if (!progression.Player.Skills.Skills.Any(value => value.ClassId == 10014))
                progression.Player.Skills.Skills.Add(new OfflineOwnedSkill { ClassId = 10014, Rank = 1 });
            if (!progression.Player.Inventory.Any(value => value.ItemId == 39503 && value.Count > 0))
                progression.Player.Inventory.Add(new OfflineItemStack { ItemId = 39503, Count = 1 });
            Require(progression.EquipSkill(10014, 0).IsApplied, "The skin validation skill could not be equipped.");
            panel.ReloadProfile();
            yield return null;
            SimulatePointerClick(FindFirstSkillSlot("ActiveSkill"));
            yield return new WaitForSecondsRealtime(.5f);
            Button appearance = FindButton("AppearanceBtn");
            Require(appearance != null && appearance.gameObject.activeInHierarchy, "Owned skill skin did not reveal the original appearance entry.");
            SimulatePointerClick(appearance);
            yield return new WaitForSecondsRealtime(.7f);
            Transform skinDialog = UnityEngine.Object.FindObjectsOfType<Transform>().First(value => value.name == "skill_skin_replace");
            Button alternateSkin = skinDialog.GetComponentsInChildren<Button>(true).First(value => value.name == "ItemIconSkill (1)");
            SimulatePointerClick(alternateSkin);
            yield return new WaitForSecondsRealtime(.3f);
            Button wearSkin = skinDialog.GetComponentsInChildren<Button>().First(value => value.name == "ConfirmBtn");
            SimulatePointerClick(wearSkin);
            Require(progression.EquippedSkillSkinId(10014) == 39503, "The original skin confirm button did not save the selected skin.");
            Require(OfflineMainlineProgressSession.OpenCharacter(slots, slot).Save.Player.Wardrobe.SkillSkins.Any(value => value.SkillId == 10014 && value.SkillSkinId == 39503), "The selected skill skin did not survive save reload.");
            float previewDeadline = Time.realtimeSinceStartup + 15f;
            var preview = skinDialog.GetComponentInChildren<UnityEngine.Video.VideoPlayer>();
            while (preview != null && !preview.isPlaying && Time.realtimeSinceStartup < previewDeadline) yield return null;
            Require(preview != null && preview.isPlaying && preview.targetTexture != null, "The original local skill preview did not render video frames.");
            float visibleVideoDeadline = Time.realtimeSinceStartup + 15f;
            while (preview.frame < 30 && Time.realtimeSinceStartup < visibleVideoDeadline) yield return null;
            Require(preview.frame >= 30, "The preview did not advance beyond its original opening fade.");
            var previousVideoTarget = RenderTexture.active;
            RenderTexture.active = preview.targetTexture;
            var videoPixels = new Texture2D(preview.targetTexture.width, preview.targetTexture.height, TextureFormat.RGB24, false);
            videoPixels.ReadPixels(new Rect(0, 0, videoPixels.width, videoPixels.height), 0, 0);
            videoPixels.Apply();
            var colors = videoPixels.GetPixels();
            Require(colors.Average(value => value.r) > .05f && colors.Average(value => value.b) > .05f,
                "The native preview has lost red/blue video channels on Windows.");
            RenderTexture.active = previousVideoTarget;
            UnityEngine.Object.Destroy(videoPixels);
            Require(skinDialog.GetComponentsInChildren<TMPro.TMP_Text>().Any(value => value.name == "Desc" && !string.IsNullOrWhiteSpace(value.text)),
                "The original skin description is blank.");
            ScreenCapture.CaptureScreenshot(Path.Combine(ValidationDirectory, "skills-skin.png"));
            yield return null;
            SimulatePointerClick(skinDialog.GetComponentsInChildren<Button>().First(value => value.name == "CommonBtn_PlayVideo"));
            yield return new WaitForSecondsRealtime(.5f);
            Transform expandedVideo = UnityEngine.Object.FindObjectsOfType<Transform>().First(value => value.name == "skill_video_preview");
            var expandedPlayer = expandedVideo.GetComponentInChildren<UnityEngine.Video.VideoPlayer>();
            float expandedDeadline = Time.realtimeSinceStartup + 15f;
            while ((expandedPlayer == null || !expandedPlayer.isPlaying || expandedPlayer.frame < 30) && Time.realtimeSinceStartup < expandedDeadline) yield return null;
            Require(expandedPlayer != null && expandedPlayer.isPlaying && expandedPlayer.targetTexture != null,
                "The original video magnify button did not play its local movie.");
            var aspect = expandedVideo.GetComponentInChildren<AspectRatioFitter>();
            Require(aspect != null && Math.Abs(aspect.aspectRatio - (float)expandedPlayer.width / expandedPlayer.height) < .001f,
                "The expanded official video did not preserve its native aspect ratio.");
            ScreenCapture.CaptureScreenshot(Path.Combine(ValidationDirectory, "skills-skin-video.png"));
            yield return null;
            SimulatePointerClick(expandedVideo.GetComponentsInChildren<Button>().First(value => value.name == "CloseBtn"));
            yield return null;
            Require(skinDialog != null && skinDialog.gameObject.activeInHierarchy, "Closing the video also closed skill appearance selection.");
            Button defaultSkin = skinDialog.GetComponentsInChildren<Button>().First(value => value.name == "ItemIconSkill");
            SimulatePointerClick(defaultSkin);
            yield return null;
            SimulatePointerClick(wearSkin);
            Require(progression.EquippedSkillSkinId(10014) == 0, "Choosing the default skill skin did not remove the appearance override.");
            Require(panel.HandleEscape(), "The original skin dialog did not close.");
            yield return null;
            Require(panel.HandleEscape(), "The skill detail did not close after skin replacement.");
            yield return null;

            progression.Player.Level = 40;
            progression.Player.Experience = assets.Load().CumulativeLevelExperience[40];
            panel.ReloadProfile();
            yield return null;
            OfficialSkillDamageTestRequest damageRequest = null;
            panel.DamageTestRequested += request => damageRequest = request;
            SimulatePointerClick(FindButton("StageDamageTestBtn"));
            yield return new WaitForSecondsRealtime(.5f);
            Transform damageDialog = UnityEngine.Object.FindObjectsOfType<Transform>().First(value => value.name == "damage_test");
            var damageDropdown = damageDialog.GetComponentInChildren<Zjcs.Official.WorldUI.WorldUIDropdown>();
            Require(damageDropdown.ItemsKey.Count == 5, "The original damage-test monster dropdown omitted stages.");
            SimulatePointerClick(damageDropdown.DropdownButton);
            yield return new WaitForSecondsRealtime(.3f);
            var dropdownEntry = damageDialog.GetComponentsInChildren<Zjcs.Official.WorldUI.WorldDropdownItem>()
                .First(value => value.Texts.Any(text => text.Text == damageDropdown.ItemsKey[1]));
            SimulatePointerClick(dropdownEntry.Button);
            yield return new WaitForSecondsRealtime(.3f);
            Require(damageDropdown.SelectedItemIndex == 1, "The native monster dropdown did not select the simulated click.");
            var difficultyButton = damageDialog.GetComponentsInChildren<Button>()
                .First(value => value.GetComponentsInChildren<TMPro.TMP_Text>().Any(text => text.name == "NumTxt" && text.text == "02"));
            SimulatePointerClick(difficultyButton);
            yield return new WaitForSecondsRealtime(.4f);
            Toggle DamageToggle(string group, string name) => damageDialog.GetComponentsInChildren<Toggle>()
                .First(value => value.transform.parent.name == name && value.GetComponentsInParent<Transform>().Any(parent => parent.name == group));
            SimulatePointerClick(DamageToggle("Round", "ToggleText (2)"));
            SimulatePointerClick(DamageToggle("MonsterHp", "ToggleText (1)"));
            SimulatePointerClick(DamageToggle("MonsterHurt", "ToggleText (1)"));
            yield return null;
            ScreenCapture.CaptureScreenshot(Path.Combine(ValidationDirectory, "skills-damage-settings.png"));
            yield return null;
            SimulatePointerClick(damageDialog.GetComponentsInChildren<Button>().First(value => value.name == "StartTest"));
            Require(damageRequest != null && damageRequest.StageId == 72001 && damageRequest.Difficulty == 1
                && damageRequest.MonsterLevel == 20 && damageRequest.MaxRound == 50 && !damageRequest.EnableMonsterOneDamage
                && !damageRequest.EnableMonsterUnlimitHp, "The original damage-test input did not preserve the selected settings.");
            SimulatePointerClick(damageDialog.GetComponentsInChildren<Button>().First(value => value.name == "CloseBtn"));
            yield return null;

            OfflineSkillDefinition skill = progression.SkillDefinition(10000);
            Require(skill != null && skill.PieceItemId == 12000, "Official skill or piece rule is unavailable.");
            ProgressionResult merged = progression.MergeSkill(skill.ClassId);
            Require(merged.IsApplied, "Skill piece merge failed: " + merged.Message);
            ProgressionResult equipped = progression.EquipSkill(skill.ClassId, 0);
            Require(equipped.IsApplied, "Merged skill could not be equipped: " + equipped.Message);
            OfflineMainlineProgressSession reloaded = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            Require(reloaded.Save.Player.Skills.SelectedPlan == progression.Player.Skills.SelectedPlan && reloaded.Save.Player.Skills.Skills.Any(value => value.ClassId == skill.ClassId) && reloaded.Save.Player.Skills.Plans[reloaded.Save.Player.Skills.SelectedPlan].ActiveSkills[0] == skill.ClassId, "Skill plan, merge or slot assignment did not survive profile reload.");
            yield return null;
            SimulatePointerClick(FindButton("CloseBtn_dark"));
            float restoreDeadline = Time.realtimeSinceStartup + 0.5f;
            while (Time.realtimeSinceStartup < restoreDeadline) yield return null;
            Require(!panel.IsOpen, "The original bottom return button did not close the skill page.");
            Transform restoredProperties = characterView.transform.Find("CharacterPanel/BgPanel1/Content/TopGo/CharacterPanelPropPanel");
            Require(restoredProperties != null && restoredProperties.gameObject.activeInHierarchy,
                "The character property page was not restored after closing skills.");
            Debug.Log("[Skills acceptance] official skill panel rendered; plan switched, recommendation applied, favorite and detail interactions passed, skill merged and equipped, profile reload passed. Screenshot: " + screenshot);
            UnityEngine.Object.Destroy(canvasObject);
            yield return null;
            yield return ValidateHomeDamageTest(slot, root);
            if (ownsEventSystem)
                UnityEngine.Object.Destroy(eventSystem);
            Finish(true, null);
        }

        private static IEnumerator ValidateListAttachments(OfflineMainlineProgressSession session,
            OfflineCharacterProgressionService progression, CharacterSkillPanel panel)
        {
            Transform Row(int id) => UnityEngine.Object.FindObjectsOfType<Transform>()
                .Single(value => value.name == "SkillListItem_" + id);
            bool Attachment(int id, int type) => Row(id).GetComponentsInChildren<Transform>()
                .Any(value => value.name == "OriginalSkillAttachment_" + type);
            Require(!progression.IsNewSessionItem(10015) && !Attachment(10015, 4),
                "An existing login skill incorrectly received a new-item mark.");
            Require(Attachment(10000, 13) && Attachment(10000, 8), "Mergeable pieces lost their original center hint or merge red dot.");
            Require(Row(10000).GetComponentsInChildren<TMPro.TMP_Text>().Any(value => value.text.Contains("合成")),
                "The original merge hint did not use its Chinese localization.");
            ScreenCapture.CaptureScreenshot(Path.Combine(ValidationDirectory, "skills-list-merge-attachment.png"));
            yield return null;
            SimulatePointerClick(Row(10000).GetComponent<Button>());
            yield return new WaitForSecondsRealtime(.6f);
            SimulatePointerClick(FindButton("MergeBtn"));
            yield return new WaitForSecondsRealtime(.3f);
            Require(progression.IsNewSessionItem(10000) && Attachment(10000, 4) && !Attachment(10000, 13),
                "Merging through the original dialog did not replace the hint with a transient new-skill mark.");
            ScreenCapture.CaptureScreenshot(Path.Combine(ValidationDirectory, "skills-list-new-attachment.png"));
            yield return null;
            SimulatePointerClick(Row(10000).GetComponent<Button>());
            yield return new WaitForSecondsRealtime(.6f);
            Require(!progression.IsNewSessionItem(10000) && !Attachment(10000, 4),
                "Opening a newly acquired skill did not clear its mark.");
            SimulatePointerClick(FindButton("BlurMask"));
            yield return new WaitForSecondsRealtime(.3f);
            int mismatch = progression.SkillRules.Skills.First(value => value.ItemType == 100
                && !OfflineSkillProgression.IsAvailableFor(value, progression.SkillContext())).ClassId;
            Require(session.ApplyCharacterMutation(player => {
                player.Skills.Skills.Add(new OfflineOwnedSkill { ClassId = mismatch, Rank = 1 });
                var piece = player.Inventory.SingleOrDefault(value => value.ItemId == 12000);
                if (piece != null) piece.Count = 0;
            }, "skills-attachments-mismatch").IsApplied, "Could not prepare the incompatible skill fixture.");
            panel.ReloadProfile();
            yield return null;
            var all = UnityEngine.Object.FindObjectsOfType<Toggle>().Single(value => value.name == "ShowAll");
            if (!all.isOn) SimulatePointerClick(all);
            yield return null;
            FindVisibleOwnedSkillRowButton("SkillListItem", progression, mismatch, out _);
            Require(Attachment(mismatch, 10) && Attachment(mismatch, 4),
                "The incompatible newly acquired skill lost an original attachment.");
            Require(Row(mismatch).GetComponentsInChildren<TMPro.TMP_Text>().Any(value => value.text.Contains("职业")),
                "The original profession warning was not localized.");
            ScreenCapture.CaptureScreenshot(Path.Combine(ValidationDirectory, "skills-list-profession-attachment.png"));
            yield return null;
            Debug.Log("[Skills attachments] Original merge hint, merge red dot, acquired mark, click-to-clear and profession mismatch passed through simulated input.");
        }

        private static IEnumerator ValidateHomeDamageTest(string slot, string root)
        {
            yield return Zjcs.Presentation.Home.SkillDamageTestValidation.Exercise(slot, root, ValidationDirectory, SimulatePointerClick);
        }

        private static Button FindButton(string name)
        {
            return UnityEngine.Object.FindObjectsOfType<Button>(true).Where(value => value.name == name)
                .OrderByDescending(value => value.isActiveAndEnabled).FirstOrDefault();
        }

        private static Button FindButtonContaining(string name)
        {
            return UnityEngine.Object.FindObjectsOfType<Button>(true).FirstOrDefault(value => value.name.IndexOf(name, StringComparison.Ordinal) >= 0);
        }

        private static Button FindFirstSkillSlot(string groupName)
        {
            RectTransform group = UnityEngine.Object.FindObjectsOfType<RectTransform>(true).FirstOrDefault(value => value.name == groupName);
            if (group == null)
                return null;
            return group.Cast<Transform>().OfType<RectTransform>().FirstOrDefault(value => value.name.StartsWith("SkillContentPanelSkillGroupItem", StringComparison.Ordinal))?.GetComponent<Button>();
        }

        private static Button FindRaycastableButton(string name)
        {
            foreach (Button candidate in UnityEngine.Object.FindObjectsOfType<Button>(true).Where(value => value.name == name && value.isActiveAndEnabled))
            {
                var pointer = new PointerEventData(EventSystem.current)
                {
                    position = RectTransformUtility.WorldToScreenPoint(null, candidate.transform.position)
                };
                var raycasts = new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, raycasts);
                if (raycasts.Any(value => value.gameObject == candidate.gameObject || value.gameObject.transform.IsChildOf(candidate.transform)))
                    return candidate;
            }

            return null;
        }

        private static Button FindSkillRowButton(string buttonName, string skillName)
        {
            return UnityEngine.Object.FindObjectsOfType<Button>(true).FirstOrDefault(value =>
            {
                return value.name == buttonName + "_" + skillName && value.gameObject.activeInHierarchy;
            });
        }

        private static Button FindVisibleOwnedSkillRowButton(string buttonName, OfflineCharacterProgressionService progression, int requiredSkillId, out int skillId)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform grid = UnityEngine.Object.FindObjectsOfType<RectTransform>(true).FirstOrDefault(value => value.name == "skillGrid" && value.gameObject.activeInHierarchy);
            ScrollRect scroll = grid == null ? null : grid.GetComponentInParent<ScrollRect>();
            OfflineSkillPlan currentPlan = progression.Player.Skills.Plans[progression.Player.Skills.SelectedPlan];
            int[] equipped = currentPlan.ActiveSkills.Concat(currentPlan.PassiveSkills).ToArray();
            OfflineOwnedSkill[] candidates = progression.Player.Skills.Skills.Where(value => value != null && !equipped.Contains(value.ClassId)).ToArray();
            for (int scrollStep = 0; scrollStep <= 20; scrollStep++)
            {
                if (scroll != null)
                {
                    float normalized = 1f - scrollStep / 20f;
                    float maximum = Mathf.Max(0f, scroll.content.rect.height - scroll.viewport.rect.height);
                    scroll.content.anchoredPosition = new Vector2(0f, maximum * (1f - normalized));
                    scroll.verticalNormalizedPosition = normalized;
                    Canvas.ForceUpdateCanvases();
                }

                foreach (OfflineOwnedSkill owned in candidates)
                {
                    if (requiredSkillId > 0 && owned.ClassId != requiredSkillId)
                        continue;
                    Button candidate = FindSkillRowButton(buttonName, owned.ClassId.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    if (candidate == null)
                        continue;
                    Vector2 position = RectTransformUtility.WorldToScreenPoint(null, candidate.transform.position);
                    if (position.x < 0 || position.x > Screen.width || position.y < 0 || position.y > Screen.height)
                        continue;
                    if (scroll != null && scroll.viewport != null && !RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport, position))
                        continue;
                    var pointer = new PointerEventData(EventSystem.current)
                    {
                        position = position
                    };
                    var raycasts = new System.Collections.Generic.List<RaycastResult>();
                    EventSystem.current.RaycastAll(pointer, raycasts);
                    if (!raycasts.Any(value => value.gameObject == candidate.gameObject || value.gameObject.transform.IsChildOf(candidate.transform)))
                        continue;
                    skillId = owned.ClassId;
                    return candidate;
                }
            }

            skillId = 0;
            return null;
        }

        private static void SimulateSkillSlotDrag(GameObject source, GameObject target)
        {
            RectTransform icon = source.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(value => value.name == "SkillIcon");
            Require(icon != null, "The source skill slot has no icon to drag.");
            Debug.Log("[Skills acceptance] drag source=" + source.name + " at " + source.transform.position + "; target=" + target.name + " at " + target.transform.position + "; icon=" + icon.rect.size + " at " + icon.position);
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, icon.position)
            };
            ExecuteEvents.Execute(source, pointer, ExecuteEvents.beginDragHandler);
            bool hasDragHandler = source.GetComponents<MonoBehaviour>().Any(value => value != null && value.GetType().Name == "SkillSlotDrag");
            bool targetHasDragHandler = target.GetComponents<MonoBehaviour>().Any(value => value != null && value.GetType().Name == "SkillSlotDrag");
            bool began = UnityEngine.Object.FindObjectsOfType<RectTransform>(true).Any(value => value.name == "SkillDragGhost");
            Require(began, "The skill slot did not begin dragging its icon; source handler=" + hasDragHandler + ", target handler=" + targetHasDragHandler + ".");
            pointer.position = RectTransformUtility.WorldToScreenPoint(null, target.transform.position);
            bool dragged = ExecuteEvents.Execute(source, pointer, ExecuteEvents.dragHandler);
            bool ended = ExecuteEvents.Execute(source, pointer, ExecuteEvents.endDragHandler);
            Debug.Log("[Skills acceptance] drag dispatch: began=" + began + ", dragged=" + dragged + ", ended=" + ended + ", destination screen=" + pointer.position);
        }

        private static void SimulatePointerClick(Selectable control)
        {
            Require(control != null && control.isActiveAndEnabled && control.interactable, "Cannot click an inactive skill UI button.");
            Canvas.ForceUpdateCanvases();
            Transform hitTarget = control.targetGraphic == null ? control.transform : control.targetGraphic.transform;
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, hitTarget.position)
            };
            var raycasts = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, raycasts);
            bool hitsControl = raycasts.Count > 0 && (raycasts[0].gameObject == control.gameObject
                || raycasts[0].gameObject.transform.IsChildOf(control.transform));
            if (!hitsControl)
            {
                Require(TryFindPointerPosition(control.gameObject, out Vector2 visiblePosition), "A simulated mouse click found no UI target for " + control.name + " at " + pointer.position + " on " + Screen.width + "x" + Screen.height + "; canvas=" + control.GetComponentInParent<Canvas>()?.name + ".");
                pointer.position = visiblePosition;
                raycasts.Clear();
                EventSystem.current.RaycastAll(pointer, raycasts);
            }

            GameObject hit = raycasts[0].gameObject;
            pointer.pointerEnter = hit;
            pointer.pointerPress = hit;
            pointer.rawPointerPress = hit;
            ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerEnterHandler);
            ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static void SimulatePointerClickAt(GameObject target, Vector2 position)
        {
            var pointer = new PointerEventData(EventSystem.current) {
                button = PointerEventData.InputButton.Left, position = position
            };
            var hits = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Require(hits.Count > 0 && (hits[0].gameObject == target || hits[0].gameObject.transform.IsChildOf(target.transform)),
                "The original skill text is obstructed at its link: " + position);
            pointer.pointerEnter = hits[0].gameObject;
            pointer.pointerPressRaycast = hits[0];
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.ExecuteHierarchy(hits[0].gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }

        private static bool TryFindPointerPosition(GameObject target, out Vector2 position)
        {
            RectTransform rect = target == null ? null : target.transform as RectTransform;
            if (rect == null || EventSystem.current == null)
            {
                position = default;
                return false;
            }

            float left = 0f;
            float right = Screen.width;
            float bottom = 0f;
            float top = Screen.height;
            var pointer = new PointerEventData(EventSystem.current);
            var raycasts = new System.Collections.Generic.List<RaycastResult>();
            for (float y = bottom + 4f; y <= top; y += 12f)
                for (float x = left + 4f; x <= right; x += 12f)
                {
                    pointer.position = new Vector2(x, y);
                    EventSystem.current.RaycastAll(pointer, raycasts);
                    if (raycasts.Count > 0 && (raycasts[0].gameObject == target || raycasts[0].gameObject.transform.IsChildOf(target.transform)))
                    {
                        position = pointer.position;
                        return true;
                    }

                    raycasts.Clear();
                }

            position = default;
            return false;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
        }

        private static void Finish(bool passed, Exception error)
        {
            Application.logMessageReceived -= CaptureRuntimeFailure;
            if (error == null)
                Debug.Log("[Skills acceptance] " + (passed ? "passed" : "failed"));
            else
                Debug.LogException(error);
            EditorApplication.update -= OnUpdate;
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetBool(ResultKey, passed);
            SessionState.SetBool(ExitKey, Environment.GetEnvironmentVariable("ZJCS_SKILLS_AUTORUN") == "1");
            if (reloadLocked) { reloadLocked = false; EditorApplication.UnlockReloadAssemblies(); }
            EditorApplication.isPlaying = false;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode)
                return;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.update -= OnUpdate;
            string root = SessionState.GetString(RootKey, string.Empty);
            if (Directory.Exists(root))
                Directory.Delete(root, true);
            SessionState.SetBool(ActiveKey, false);
            if (Environment.GetEnvironmentVariable("ZJCS_SKILLS_AUTORUN") == "1")
            {
                EditorApplication.update -= ExitWhenReady;
                EditorApplication.update += ExitWhenReady;
            }
        }

        private static void ExitWhenReady()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= ExitWhenReady;
            string root = SessionState.GetString(RootKey, string.Empty);
            if (Directory.Exists(root)) Directory.Delete(root, true);
            bool passed = SessionState.GetBool(ResultKey, false);
            SessionState.SetBool(ExitKey, false);
            SessionState.SetBool(ResultKey, false);
            if (passed && Environment.GetEnvironmentVariable("ZJCS_SKILLS_BUILD_AFTER_ACCEPTANCE") == "1")
            {
                try
                {
                    ProjectTools.Validate();
                    OfficialSkillAssetTools.BuildWindowsValidation();
                }
                catch (Exception error) { Debug.LogException(error); passed = false; }
            }
            EditorApplication.Exit(passed ? 0 : 1);
        }

        private sealed class Runner : MonoBehaviour
        {
            private void Update()
            {
                try
                {
                    if (!Input.GetMouseButtonDown(0))
                        return;
                    var pointer = new PointerEventData(EventSystem.current)
                    {
                        position = Input.mousePosition
                    };
                    var hits = new System.Collections.Generic.List<RaycastResult>();
                    EventSystem.current.RaycastAll(pointer, hits);
                    string hit = hits.Count == 0 ? "<none>" : hits[0].gameObject.name;
                    Debug.Log("[Skills acceptance] Unity mouse-down at " + Input.mousePosition + "; hit=" + hit);
                }
                catch (Exception exception)
                {
                    Debug.LogError("[Skills acceptance] Unity mouse input unavailable: " + exception.Message);
                }
            }
        }
    }
}
