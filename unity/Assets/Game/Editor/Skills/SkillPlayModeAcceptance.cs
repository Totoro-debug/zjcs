using System;
using System.IO;
using System.Linq;
using System.Collections;
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
        private static bool started;

        [InitializeOnLoadMethod]
        private static void AutoRunInWindowedEditor()
        {
            if (Environment.GetEnvironmentVariable("ZJCS_SKILLS_AUTORUN") != "1"
                || Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            EditorApplication.update -= AutoRunTick;
            EditorApplication.update += AutoRunTick;
        }

        private static void AutoRunTick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= AutoRunTick;
            if (!SessionState.GetBool(ActiveKey, false)) Run();
        }

        [MenuItem("Zjcs/Validate/Skills Play Mode")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Exit Play Mode before running skill acceptance.");
            string root = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "output", "skills-smoke-" + Guid.NewGuid().ToString("N"));
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
            save.Player.Experience = OfficialCharacterAssetCatalog.LoadDefault().Load()
                .CumulativeLevelExperience[save.Player.Level];
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
            if (!SessionState.GetBool(ActiveKey, false)) return;
            EditorApplication.update -= OnUpdate;
            EditorApplication.update += OnUpdate;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private static void OnUpdate()
        {
            if (!SessionState.GetBool(ActiveKey, false) || !EditorApplication.isPlaying || started) return;
            started = true;
            var host = new GameObject("SkillAcceptanceRunner");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<Runner>().StartCoroutine(Scenario());
        }

        private static IEnumerator Scenario()
        {
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
            GameObject canvasObject = null;
            GameObject eventSystem = null;
            bool ownsEventSystem = false;
            Exception error = null;
            var expectedRecommendationSkills = new System.Collections.Generic.HashSet<int>();
            try
            {
                slots = new ProfileSlotCatalog(root);
                OfflineMainlineProgressSession session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
                OfficialCharacterAssetCatalog assets = OfficialCharacterAssetCatalog.LoadDefault();
                progression = new OfflineCharacterProgressionService(session, assets.Load());
                OfficialSkillRecommendationSet recommendations = assets.LoadSkillRecommendations();
                progression.AttachSkillRules(assets.LoadSkillRules(), recommendations.recommendations);
                OfflineSkillPlayerContext skillContext = progression.SkillContext();
                foreach (OfficialSkillRecommendation recommendation in recommendations.recommendations.Where(value =>
                             value != null && skillContext.CurrentProfessionChain.Contains(value.Profession)))
                {
                    foreach (int skillId in (recommendation.ActiveSkillList ?? Array.Empty<int>())
                                 .Concat(recommendation.PassiveSkillList ?? Array.Empty<int>()))
                    {
                        OfflineSkillDefinition definition = progression.SkillDefinition(skillId);
                        if (skillId == 10000 || !OfflineSkillProgression.IsAvailableFor(definition, skillContext)) continue;
                        expectedRecommendationSkills.Add(skillId);
                        if (!progression.Player.Skills.Skills.Any(value => value.ClassId == skillId))
                            progression.Player.Skills.Skills.Add(new OfflineOwnedSkill { ClassId = skillId, Rank = 1 });
                    }
                }
                Require(expectedRecommendationSkills.Count > 0,
                    "The official profession chain has no available recommendation skills.");

                canvasObject = new GameObject("SkillsAcceptanceCanvas",
                    typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                Canvas canvas = canvasObject.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 32000;
                CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1080f, 1920f);
                scaler.matchWidthOrHeight = 1f;
                var backgroundObject = new GameObject("SkillAcceptanceBackground",
                    typeof(RectTransform), typeof(Image));
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
                portrait.anchorMin = new Vector2(0.5f, 0.5f);
                portrait.anchorMax = new Vector2(0.5f, 0.5f);
                portrait.pivot = new Vector2(0.5f, 0.5f);
                portrait.sizeDelta = new Vector2(1080f, 1920f);
                EventSystem existingEventSystem = UnityEngine.Object.FindObjectsOfType<EventSystem>()
                    .FirstOrDefault(value => value.isActiveAndEnabled);
                if (existingEventSystem != null)
                {
                    eventSystem = existingEventSystem.gameObject;
                    BaseInputModule inputModule = existingEventSystem.GetComponent<BaseInputModule>();
                    if (inputModule == null) inputModule = existingEventSystem.gameObject.AddComponent<StandaloneInputModule>();
                    inputModule.enabled = true;
                }
                else
                {
                    eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                    ownsEventSystem = true;
                }
                panel = canvasObject.AddComponent<CharacterSkillPanel>();
                panel.Initialize(assets, progression);
                panel.Open(portrait.transform);
                foreach (GraphicRaycaster raycaster in UnityEngine.Object.FindObjectsOfType<GraphicRaycaster>(true))
                    if (raycaster.transform.root != canvasObject.transform) raycaster.enabled = false;
            }
            catch (Exception exception)
            {
                error = exception;
            }
            if (error != null)
            {
                Finish(false, error);
                yield break;
            }

            float animationDeadline = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < animationDeadline) yield return null;
            string screenshot = Path.Combine(Path.GetTempPath(), "zjcs-skills-playmode.png");
            int selectedPlan = progression.Player.Skills.SelectedPlan;
            try
            {
                ScreenCapture.CaptureScreenshot(screenshot);
                Button nextPlanButton = FindButton("NextPlanBtn");
                Require(nextPlanButton != null, "Skill plan switch button is missing.");
                Vector2 buttonPosition = RectTransformUtility.WorldToScreenPoint(null,
                    nextPlanButton.transform.position);
                var pointer = new PointerEventData(EventSystem.current) { position = buttonPosition };
                var raycasts = new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, raycasts);
                string topHit = raycasts.Count == 0 ? "<none>" : raycasts[0].gameObject.name;
                Debug.Log("[Skills acceptance] skill panel ready; mouse target=" + buttonPosition
                    + "; screen=" + Screen.width + "x" + Screen.height + "; hit=" + topHit
                    + "; button=" + nextPlanButton.interactable + "; screenshot=" + screenshot);
                SimulatePointerClick(nextPlanButton);
            }
            catch (Exception exception)
            {
                error = exception;
            }
            if (error != null)
            {
                UnityEngine.Object.Destroy(canvasObject);
                UnityEngine.Object.Destroy(eventSystem);
                Finish(false, error);
                yield break;
            }

            GameObject recommendationDialog = null;
            Button applyRecommendation = null;
            string recommendationScreenshot = Path.Combine(Path.GetTempPath(), "zjcs-skills-recommend.png");
            try
            {
                Require(progression.Player.Skills.SelectedPlan != selectedPlan,
                    "A simulated Unity pointer click did not switch the skill plan.");

                Button recommendButton = FindButton("RecommendBtn");
                Require(recommendButton != null, "The skill recommendation button is missing.");
                SimulatePointerClick(recommendButton);
            }
            catch (Exception exception)
            {
                error = exception;
            }
            if (error == null) yield return null;
            if (error == null)
            {
                try
                {
                    recommendationDialog = UnityEngine.Object.FindObjectsOfType<Transform>(true)
                        .FirstOrDefault(value => value.name == "SkillRecommendDialog")?.gameObject;
                    Require(recommendationDialog != null && recommendationDialog.activeInHierarchy,
                        "The official skill recommendation dialog did not open.");
                    applyRecommendation = FindRaycastableButton("SaveBtn");
                    Require(applyRecommendation != null,
                        "The official recommendation item has no active apply button.");
                    ScreenCapture.CaptureScreenshot(recommendationScreenshot);
                }
                catch (Exception exception)
                {
                    error = exception;
                }
            }
            if (error == null) yield return new WaitForEndOfFrame();
            if (error == null)
            {
                try
                {
                    SimulatePointerClick(applyRecommendation);
                }
                catch (Exception exception)
                {
                    error = exception;
                }
            }
            if (error == null) yield return new WaitForEndOfFrame();
            if (error == null)
            {
                try
                {
                    OfflineSkillPlan recommendedPlan = progression.Player.Skills.Plans[progression.Player.Skills.SelectedPlan];
                    Require(recommendedPlan.ActiveSkills.Concat(recommendedPlan.PassiveSkills)
                            .Any(expectedRecommendationSkills.Contains),
                        "The official recommendation did not install an available owned skill: " + panel.LastMessage);
                    Require(recommendationDialog == null || !recommendationDialog.activeInHierarchy,
                        "Applying an official recommendation did not close its dialog: " + panel.LastMessage);
                    Debug.Log("[Skills acceptance] official recommendation rendered and applied by simulated pointer click. Screenshot: "
                        + recommendationScreenshot);

                    Button firstActiveSlot = FindFirstSkillSlot("ActiveSkill");
                    Require(firstActiveSlot != null, "The official active skill slot selector is missing.");
                    SimulatePointerClick(firstActiveSlot);
                    Canvas.ForceUpdateCanvases();
                    Require(UnityEngine.Object.FindObjectsOfType<RectTransform>(true)
                            .Any(value => value.name == "skillGrid" && value.gameObject.activeInHierarchy),
                        "Selecting a skill slot did not reveal the official skill list.");

                    Button showAll = FindButton("ShowAll");
                    Toggle showAllToggle = showAll == null ? null : showAll.GetComponent<Toggle>();
                    if (showAllToggle == null)
                        showAllToggle = UnityEngine.Object.FindObjectsOfType<Toggle>(true)
                            .FirstOrDefault(value => value.name == "ShowAll");
                    Require(showAllToggle != null, "The official show-all skill filter is missing.");
                    bool wasShowingAll = showAllToggle.isOn;
                    SimulatePointerClick(showAllToggle);
                    Require(showAllToggle.isOn != wasShowingAll,
                        "The simulated pointer did not toggle the show-all skill filter.");

                    int passiveSkillId = expectedRecommendationSkills.FirstOrDefault(value =>
                        progression.SkillDefinition(value)?.ItemType == 101);
                    Require(passiveSkillId > 0, "The official profession chain has no passive recommendation skill.");
                    OfflineSkillPlan passivePlan = progression.Player.Skills.Plans[progression.Player.Skills.SelectedPlan];
                    if (passivePlan.PassiveSkills.Contains(passiveSkillId))
                    {
                        int equippedPassiveSlot = Array.IndexOf(passivePlan.PassiveSkills, passiveSkillId);
                        ProgressionResult unequippedPassive = progression.UnequipSkill(101, equippedPassiveSlot);
                        Require(unequippedPassive.IsApplied,
                            "Could not expose an owned passive skill in its category list: " + unequippedPassive.Message);
                    }
                    Button passiveTab = FindButtonContaining("PassiveSkillTab");
                    Require(passiveTab != null, "The official passive skill tab is missing.");
                    SimulatePointerClick(passiveTab);
                    Text[] passiveLabels = UnityEngine.Object.FindObjectsOfType<Text>(true)
                        .Where(value => value.name == "SkillLabel" && value.gameObject.activeInHierarchy).ToArray();
                    bool renderedPassive = passiveLabels.Any(label => progression.SkillRules.Skills
                        .Where(value => value != null && value.ItemType == 101)
                        .Select(value => progression.Catalog.FindItem(value.ClassId))
                        .Where(value => value != null && value.ContainsKey("Name"))
                        .Select(value => Convert.ToString(value["Name"], System.Globalization.CultureInfo.InvariantCulture))
                        .Any(name => label.text.StartsWith(name, StringComparison.Ordinal)));
                    Require(renderedPassive,
                        "The simulated passive tab click did not render any official passive skill row.");
                    Button activeTab = FindButtonContaining("ActiveSkillTab");
                    Require(activeTab != null, "The official active skill tab is missing.");
                    SimulatePointerClick(activeTab);

                    Button pieceTab = FindButtonContaining("SkillPieceTab");
                    Require(pieceTab != null, "The official skill piece tab is missing.");
                    SimulatePointerClick(pieceTab);
                    RectTransform pieceList = UnityEngine.Object.FindObjectsOfType<RectTransform>(true)
                        .FirstOrDefault(value => value.name == "SkillPieceScrollList");
                    RectTransform skillList = UnityEngine.Object.FindObjectsOfType<RectTransform>(true)
                        .FirstOrDefault(value => value.name == "SkillScrollList");
                    Require(pieceList != null && pieceList.gameObject.activeInHierarchy
                            && skillList != null && !skillList.gameObject.activeInHierarchy,
                        "The skill piece tab did not show its official list.");
                    Canvas.ForceUpdateCanvases();
                    ScrollRect pieceScroll = UnityEngine.Object.FindObjectsOfType<ScrollRect>(true)
                        .FirstOrDefault(value => value.name == "SkillPieceScrollList" && value.isActiveAndEnabled);
                    if (pieceScroll != null) pieceScroll.verticalNormalizedPosition = 1f;
                    Canvas.ForceUpdateCanvases();
                    Button pieceRow = UnityEngine.Object.FindObjectsOfType<Text>(true)
                        .FirstOrDefault(value => value.name == "SkillPieceLabel" && value.gameObject.activeInHierarchy)
                        ?.transform.parent.GetComponent<Button>();
                    Require(pieceRow != null, "The owned skill piece did not render as an interactive list row.");
                    SimulatePointerClick(pieceRow);
                    Require(UnityEngine.Object.FindObjectsOfType<Text>(true)
                            .Any(value => value.name == "CountText" && value.isActiveAndEnabled),
                        "The skill piece detail did not open from a simulated pointer click.");
                    Require(panel.HandleEscape(), "The skill piece detail did not handle Escape.");
                    SimulatePointerClick(activeTab);
                    SimulatePointerClick(FindFirstSkillSlot("ActiveSkill"));

                    Button detailButton = FindVisibleOwnedSkillRowButton("SkillDetail", progression, 0, out _);
                    Require(detailButton != null, "The skill list detail button is missing after slot selection.");
                    SimulatePointerClick(detailButton);
                    Require(UnityEngine.Object.FindObjectsOfType<Text>(true)
                            .Any(value => value.name == "DetailText" && value.isActiveAndEnabled),
                        "The skill detail dialog did not render after a simulated pointer click.");
                    Require(panel.HandleEscape(), "The skill detail dialog did not handle Escape.");

                    Button favoriteButton = FindVisibleOwnedSkillRowButton("SkillFavorite",
                        progression, 0, out int favoriteSkillId);
                    Require(favoriteButton != null, "The skill list favorite button is missing.");
                    SimulatePointerClick(favoriteButton);
                    Require(progression.Player.Skills.FavoriteSkills.Contains(favoriteSkillId),
                        "A simulated pointer click did not toggle the skill favorite.");

                    OfflineSkillPlan dragPlan = progression.Player.Skills.Plans[progression.Player.Skills.SelectedPlan];
                    int firstActive = Array.FindIndex(dragPlan.ActiveSkills, value => value > 0);
                    int secondActive = Array.FindIndex(dragPlan.ActiveSkills, firstActive + 1, value => value > 0);
                    if (firstActive >= 0 && secondActive < 0)
                    {
                        int extraSkill = expectedRecommendationSkills.FirstOrDefault(value =>
                            progression.SkillDefinition(value)?.ItemType == 100
                            && !dragPlan.ActiveSkills.Contains(value));
                        int emptySlot = Array.FindIndex(dragPlan.ActiveSkills, value => value == 0);
                        if (extraSkill > 0 && emptySlot >= 0)
                        {
                            ProgressionResult seeded = progression.EquipSkill(extraSkill, emptySlot);
                            Require(seeded.IsApplied, "Could not prepare two owned active skills for the drag test.");
                            int selected = progression.Player.Skills.SelectedPlan;
                            SimulatePointerClick(FindButton("NextPlanBtn"));
                            SimulatePointerClick(FindButton("PrevPlanBtn"));
                            Require(progression.Player.Skills.SelectedPlan == selected,
                                "The skill plan controls did not restore the drag-test plan.");
                            dragPlan = progression.Player.Skills.Plans[selected];
                            firstActive = Array.FindIndex(dragPlan.ActiveSkills, value => value > 0);
                            secondActive = Array.FindIndex(dragPlan.ActiveSkills, firstActive + 1, value => value > 0);
                        }
                    }
                    Require(firstActive >= 0 && secondActive >= 0,
                        "The official recommendation did not provide two active skills for drag validation.");
                    RectTransform activeSkillGroup = UnityEngine.Object.FindObjectsOfType<RectTransform>(true)
                        .FirstOrDefault(value => value.name == "ActiveSkill");
                    RectTransform[] activeSlots = activeSkillGroup.Cast<Transform>().OfType<RectTransform>()
                        .Where(value => value.name.StartsWith("SkillContentPanelSkillGroupItem",
                            StringComparison.Ordinal))
                        .Take(4).ToArray();
                    Require(activeSlots.Length > secondActive,
                        "The official active skill slots were not rendered for drag validation.");
                    int firstSkillBeforeDrag = dragPlan.ActiveSkills[firstActive];
                    int secondSkillBeforeDrag = dragPlan.ActiveSkills[secondActive];
                    SimulateSkillSlotDrag(activeSlots[firstActive].gameObject, activeSlots[secondActive].gameObject);
                    OfflineSkillPlan swappedPlan = progression.Player.Skills.Plans[progression.Player.Skills.SelectedPlan];
                    Require(swappedPlan.ActiveSkills[firstActive] == secondSkillBeforeDrag
                        && swappedPlan.ActiveSkills[secondActive] == firstSkillBeforeDrag,
                        "A simulated Unity drag did not swap the two active skill slots: " + panel.LastMessage);

                    OfflineSkillLevelCost nextSlotCost = progression.SkillRules.SlotLevelCosts
                        .FirstOrDefault(value => value.Level == 2);
                    Require(nextSlotCost != null && nextSlotCost.Items.Length > 0,
                        "The official level-two slot cultivation cost is missing.");
                    foreach (OfflineSkillItemCount cost in nextSlotCost.Items)
                    {
                        OfflineItemStack stack = progression.Player.Inventory
                            .FirstOrDefault(value => value.ItemId == cost.ItemId);
                        if (stack == null)
                            progression.Player.Inventory.Add(new OfflineItemStack { ItemId = cost.ItemId, Count = cost.Count });
                        else stack.Count = Math.Max(stack.Count, cost.Count);
                    }
                    int levelBeforeUpgrade = progression.SkillProgression.SlotLevel(
                        progression.Player.Skills, 100, 0, progression.SkillContext().CurrentSeasonId);
                    Button slotUpgradeButton = activeSlots[0].Find("SlotUpgrade")?.GetComponent<Button>();
                    Require(slotUpgradeButton != null, "The skill slot cultivation button is missing.");
                    bool cultivationClickInvoked = false;
                    slotUpgradeButton.onClick.AddListener(() => cultivationClickInvoked = true);
                    SimulatePointerClick(slotUpgradeButton);
                    Require(cultivationClickInvoked, "The simulated pointer did not reach the slot cultivation button.");
                    int levelAfterUpgrade = progression.SkillProgression.SlotLevel(
                        progression.Player.Skills, 100, 0, progression.SkillContext().CurrentSeasonId);
                    Require(levelAfterUpgrade == levelBeforeUpgrade + 1,
                        "A simulated pointer click did not apply the official slot cultivation cost: level "
                        + levelBeforeUpgrade + " -> " + levelAfterUpgrade + "; " + panel.LastMessage
                        + "; slot=" + activeSlots[0].name);

                    OfflineSkillPlan promotionPlan = progression.Player.Skills.Plans[progression.Player.Skills.SelectedPlan];
                    int promotionSkillId = expectedRecommendationSkills.FirstOrDefault(value =>
                    {
                        OfflineSkillDefinition candidate = progression.SkillDefinition(value);
                        int rank = OfflineSkillProgression.OwnedRank(progression.Player.Skills, value);
                        return candidate != null && candidate.ItemType == 100
                            && candidate.RankCosts?.Any(cost => cost.CurrentRank == rank) == true
                            && !promotionPlan.ActiveSkills.Contains(value) && !promotionPlan.PassiveSkills.Contains(value);
                    });
                    OfflineSkillDefinition promotionDefinition = progression.SkillDefinition(promotionSkillId);
                    int rankBeforePromotion = OfflineSkillProgression.OwnedRank(
                        progression.Player.Skills, promotionSkillId);
                    OfflineSkillRankCost rankCost = promotionDefinition?.RankCosts?
                        .FirstOrDefault(value => value.CurrentRank == rankBeforePromotion);
                    Require(promotionSkillId > 0 && rankCost != null,
                        "The official profession chain has no un-equipped skill eligible for rank promotion.");
                    foreach (OfflineSkillItemCount cost in rankCost.Items)
                    {
                        OfflineItemStack stack = progression.Player.Inventory
                            .FirstOrDefault(value => value.ItemId == cost.ItemId);
                        if (stack == null)
                            progression.Player.Inventory.Add(new OfflineItemStack { ItemId = cost.ItemId, Count = cost.Count });
                        else stack.Count = Math.Max(stack.Count, cost.Count);
                    }
                    System.Collections.Generic.Dictionary<string, object> promotionItem =
                        progression.Catalog.FindItem(promotionSkillId);
                    Require(promotionItem != null, "The rank promotion skill has no official item metadata.");
                    string promotionSkillName = Convert.ToString(promotionItem["Name"],
                        System.Globalization.CultureInfo.InvariantCulture);
                    Button promotionSlot = FindFirstSkillSlot("ActiveSkill");
                    Require(promotionSlot != null, "The official skill slot selector is missing before rank promotion.");
                    SimulatePointerClick(promotionSlot);
                    Canvas.ForceUpdateCanvases();
                    Button promotionRowButton = FindVisibleOwnedSkillRowButton("SkillPromote", progression,
                        promotionSkillId, out _);
                    Require(promotionRowButton != null, "The owned skill rank promotion entry is missing: "
                        + promotionSkillName + ".");
                    SimulatePointerClick(promotionRowButton);
                    Button rankUpgradeButton = UnityEngine.Object.FindObjectsOfType<Button>(true).FirstOrDefault(value =>
                        value.name == "UseItemBtn" && value.transform.parent != null
                        && value.transform.parent.parent != null
                        && value.transform.parent.parent.name == "RankUpContent");
                    Require(rankUpgradeButton != null && rankUpgradeButton.interactable,
                        "The official rank promotion button is missing or disabled.");
                    SimulatePointerClick(rankUpgradeButton);
                    Require(OfflineSkillProgression.OwnedRank(progression.Player.Skills, promotionSkillId)
                            == rankBeforePromotion + 1,
                        "A simulated pointer click did not apply the official skill rank promotion: "
                        + panel.LastMessage);
                    Require(UnityEngine.Object.FindObjectsOfType<Text>(true)
                            .Any(value => value.name == "SkillNameText" && value.isActiveAndEnabled),
                        "The official rank promotion result dialog did not render.");
                    Require(panel.HandleEscape(), "The rank promotion result did not handle Escape.");

                    OfflineSkillProfile beforeInsufficientPromotion = progression.Player.Skills.Clone();
                    OfflineOwnedSkill rollbackCandidate = progression.Player.Skills.Skills.FirstOrDefault(value =>
                        value != null && value.ClassId != promotionSkillId
                        && progression.SkillDefinition(value.ClassId)?.RankCosts?.Any(cost =>
                            cost.CurrentRank == value.Rank) == true);
                    if (rollbackCandidate != null)
                    {
                        OfflineSkillPlayerContext rollbackContext = progression.SkillContext();
                        int rollbackRank = rollbackCandidate.Rank;
                        OfflineSkillProfile emptyInventory = beforeInsufficientPromotion.Clone();
                        emptyInventory.Items.Clear();
                        OfflineSkillResult rejected = progression.SkillProgression.PromoteSkill(
                            emptyInventory, rollbackContext, rollbackCandidate.ClassId);
                        Require(rejected.Code == OfflineSkillResultCode.InsufficientItems,
                            "Promotion with no materials was not rejected as insufficient.");
                        Require(emptyInventory.Skills.First(value => value.ClassId == rollbackCandidate.ClassId).Rank
                                == rollbackRank
                            && emptyInventory.Items.Count == 0,
                            "A rejected rank promotion partially changed the skill profile or inventory.");
                    }

                    var batchCosts = new System.Collections.Generic.Dictionary<int, long>();
                    foreach (OfflineOwnedSkill owned in progression.Player.Skills.Skills.Where(value => value != null))
                    {
                        OfflineSkillDefinition definition = progression.SkillDefinition(owned.ClassId);
                        if (definition?.RankCosts == null) continue;
                        foreach (OfflineSkillRankCost cost in definition.RankCosts.Where(value => value.CurrentRank >= owned.Rank))
                            foreach (OfflineSkillItemCount itemCost in cost.Items ?? Array.Empty<OfflineSkillItemCount>())
                            {
                                if (!batchCosts.ContainsKey(itemCost.ItemId)) batchCosts[itemCost.ItemId] = 0;
                                batchCosts[itemCost.ItemId] += itemCost.Count;
                            }
                    }
                    foreach (var itemCost in batchCosts)
                    {
                        OfflineItemStack stack = progression.Player.Inventory.FirstOrDefault(value => value.ItemId == itemCost.Key);
                        if (stack == null) progression.Player.Inventory.Add(new OfflineItemStack
                            { ItemId = itemCost.Key, Count = itemCost.Value });
                        else stack.Count = Math.Max(stack.Count, itemCost.Value);
                    }
                    System.Collections.Generic.Dictionary<int, int> ranksBeforeBatch = progression.Player.Skills.Skills
                        .Where(value => value != null).ToDictionary(value => value.ClassId, value => value.Rank);
                    Button fastPromotion = FindButton("FastPromotionBtn");
                    Require(fastPromotion != null && fastPromotion.interactable,
                        "The official batch promotion button is missing or disabled.");
                    SimulatePointerClick(fastPromotion);
                    var batchDialog = UnityEngine.Object.FindObjectsOfType<Transform>(true)
                        .FirstOrDefault(value => value.name == "skill_batch_notify" && value.gameObject.activeInHierarchy);
                    Require(batchDialog != null, "Batch promotion did not open its official result dialog.");
                    bool batchChanged = progression.Player.Skills.Skills.Any(value => value != null
                        && ranksBeforeBatch.TryGetValue(value.ClassId, out int oldRank) && value.Rank > oldRank);
                    Require(batchChanged, "Batch promotion did not change any owned skill rank.");
                    Button skipBatch = FindButton("SkipBtn");
                    if (skipBatch != null && skipBatch.isActiveAndEnabled)
                    {
                        SimulatePointerClick(skipBatch);
                        Button closeBatch = FindButton("CloseObj");
                        Require(closeBatch != null && closeBatch.gameObject.activeInHierarchy,
                            "Skipping the batch result animation did not reveal its close control.");
                        SimulatePointerClick(closeBatch);
                    }

                    OfflineSkillDefinition skill = progression.SkillDefinition(10000);
                    Require(skill != null && skill.PieceItemId == 12000,
                        "Official skill or piece rule is unavailable.");
                    ProgressionResult merged = progression.MergeSkill(skill.ClassId);
                    Require(merged.IsApplied, "Skill piece merge failed: " + merged.Message);
                    ProgressionResult equipped = progression.EquipSkill(skill.ClassId, 0);
                    Require(equipped.IsApplied, "Merged skill could not be equipped: " + equipped.Message);

                    OfflineMainlineProgressSession reloaded = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
                    Require(reloaded.Save.Player.Skills.SelectedPlan == progression.Player.Skills.SelectedPlan
                        && reloaded.Save.Player.Skills.Skills.Any(value => value.ClassId == skill.ClassId)
                        && reloaded.Save.Player.Skills.Plans[reloaded.Save.Player.Skills.SelectedPlan]
                            .ActiveSkills[0] == skill.ClassId,
                        "Skill plan, merge or slot assignment did not survive profile reload.");
                    Debug.Log("[Skills acceptance] official skill panel rendered; plan switched, recommendation applied, favorite and detail interactions passed, skill merged and equipped, profile reload passed. Screenshot: " + screenshot);
                }
                catch (Exception exception)
                {
                    error = exception;
                }
            }
            UnityEngine.Object.Destroy(canvasObject);
            if (ownsEventSystem) UnityEngine.Object.Destroy(eventSystem);
            Finish(error == null, error);
        }

        private static Button FindButton(string name)
        {
            return UnityEngine.Object.FindObjectsOfType<Button>(true).FirstOrDefault(value => value.name == name);
        }

        private static Button FindButtonContaining(string name)
        {
            return UnityEngine.Object.FindObjectsOfType<Button>(true)
                .FirstOrDefault(value => value.name.IndexOf(name, StringComparison.Ordinal) >= 0);
        }

        private static Button FindFirstSkillSlot(string groupName)
        {
            RectTransform group = UnityEngine.Object.FindObjectsOfType<RectTransform>(true)
                .FirstOrDefault(value => value.name == groupName);
            if (group == null) return null;
            return group.Cast<Transform>().OfType<RectTransform>()
                .FirstOrDefault(value => value.name.StartsWith("SkillContentPanelSkillGroupItem",
                    StringComparison.Ordinal))?.GetComponent<Button>();
        }

        private static Button FindRaycastableButton(string name)
        {
            foreach (Button candidate in UnityEngine.Object.FindObjectsOfType<Button>(true)
                         .Where(value => value.name == name && value.isActiveAndEnabled))
            {
                var pointer = new PointerEventData(EventSystem.current)
                {
                    position = RectTransformUtility.WorldToScreenPoint(null, candidate.transform.position)
                };
                var raycasts = new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, raycasts);
                if (raycasts.Any(value => value.gameObject == candidate.gameObject
                    || value.gameObject.transform.IsChildOf(candidate.transform))) return candidate;
            }
            return null;
        }

        private static Button FindSkillRowButton(string buttonName, string skillName)
        {
            return UnityEngine.Object.FindObjectsOfType<Button>(true).FirstOrDefault(value =>
            {
                if (value.name != buttonName || value.transform.parent == null) return false;
                Text label = value.transform.parent.GetComponentsInChildren<Text>(true)
                    .FirstOrDefault(text => text.name == "SkillLabel");
                return label != null && label.text.StartsWith(skillName, StringComparison.Ordinal);
            });
        }

        private static Button FindVisibleOwnedSkillRowButton(string buttonName,
            OfflineCharacterProgressionService progression, int requiredSkillId, out int skillId)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform grid = UnityEngine.Object.FindObjectsOfType<RectTransform>(true)
                .FirstOrDefault(value => value.name == "skillGrid" && value.gameObject.activeInHierarchy);
            ScrollRect scroll = grid == null ? null : grid.GetComponentInParent<ScrollRect>();
            OfflineSkillPlan currentPlan = progression.Player.Skills.Plans[progression.Player.Skills.SelectedPlan];
            int[] equipped = currentPlan.ActiveSkills.Concat(currentPlan.PassiveSkills).ToArray();
            OfflineOwnedSkill[] candidates = progression.Player.Skills.Skills
                .Where(value => value != null && !equipped.Contains(value.ClassId)).ToArray();
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
                    if (requiredSkillId > 0 && owned.ClassId != requiredSkillId) continue;
                    System.Collections.Generic.Dictionary<string, object> item =
                        progression.Catalog.FindItem(owned.ClassId);
                    if (item == null) continue;
                    string name = Convert.ToString(item["Name"], System.Globalization.CultureInfo.InvariantCulture);
                    Button candidate = FindSkillRowButton(buttonName, name);
                    if (candidate == null) continue;
                    Vector2 position = RectTransformUtility.WorldToScreenPoint(null, candidate.transform.position);
                    if (position.x < 0 || position.x > Screen.width || position.y < 0 || position.y > Screen.height)
                        continue;
                    if (scroll != null && scroll.viewport != null
                        && !RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport, position)) continue;
                    var pointer = new PointerEventData(EventSystem.current) { position = position };
                    var raycasts = new System.Collections.Generic.List<RaycastResult>();
                    EventSystem.current.RaycastAll(pointer, raycasts);
                    if (!raycasts.Any(value => value.gameObject == candidate.gameObject
                        || value.gameObject.transform.IsChildOf(candidate.transform))) continue;
                    skillId = owned.ClassId;
                    return candidate;
                }
            }
            skillId = 0;
            return null;
        }

        private static void SimulateSkillSlotDrag(GameObject source, GameObject target)
        {
            RectTransform icon = source.GetComponentsInChildren<RectTransform>(true)
                .FirstOrDefault(value => value.name == "SkillIcon");
            Require(icon != null, "The source skill slot has no icon to drag.");
            Debug.Log("[Skills acceptance] drag source=" + source.name + " at " + source.transform.position
                + "; target=" + target.name + " at " + target.transform.position
                + "; icon=" + icon.rect.size + " at " + icon.position);
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, icon.position)
            };
            ExecuteEvents.Execute(source, pointer, ExecuteEvents.beginDragHandler);
            bool hasDragHandler = source.GetComponents<MonoBehaviour>().Any(value =>
                value != null && value.GetType().Name == "SkillSlotDrag");
            bool targetHasDragHandler = target.GetComponents<MonoBehaviour>().Any(value =>
                value != null && value.GetType().Name == "SkillSlotDrag");
            bool began = UnityEngine.Object.FindObjectsOfType<RectTransform>(true)
                .Any(value => value.name == "SkillDragGhost");
            Require(began, "The skill slot did not begin dragging its icon; source handler="
                + hasDragHandler + ", target handler=" + targetHasDragHandler + ".");
            pointer.position = RectTransformUtility.WorldToScreenPoint(null, target.transform.position);
            bool dragged = ExecuteEvents.Execute(source, pointer, ExecuteEvents.dragHandler);
            bool ended = ExecuteEvents.Execute(source, pointer, ExecuteEvents.endDragHandler);
            Debug.Log("[Skills acceptance] drag dispatch: began=" + began + ", dragged=" + dragged
                + ", ended=" + ended + ", destination screen=" + pointer.position);
        }

        private static void SimulatePointerClick(Selectable control)
        {
            Require(control != null && control.isActiveAndEnabled && control.interactable,
                "Cannot click an inactive skill UI button.");
            Canvas targetCanvas = control.GetComponentInParent<Canvas>()?.rootCanvas;
            foreach (GraphicRaycaster raycaster in UnityEngine.Object.FindObjectsOfType<GraphicRaycaster>(true))
                if (raycaster.GetComponent<Canvas>()?.rootCanvas != targetCanvas) raycaster.enabled = false;
            Canvas.ForceUpdateCanvases();
            Transform hitTarget = control.targetGraphic == null
                ? control.transform : control.targetGraphic.transform;
            var pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(null, hitTarget.position)
            };
            var raycasts = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, raycasts);
            bool hitsControl = raycasts.Any(value => value.gameObject == control.gameObject
                || value.gameObject.transform.IsChildOf(control.transform));
            if (!hitsControl)
            {
                Require(TryFindPointerPosition(control.gameObject, out Vector2 visiblePosition),
                    "A simulated mouse click found no UI target for " + control.name
                    + " at " + pointer.position + " on " + Screen.width + "x" + Screen.height
                    + "; canvas=" + control.GetComponentInParent<Canvas>()?.name + ".");
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
                    if (raycasts.Any(value => value.gameObject == target
                        || value.gameObject.transform.IsChildOf(target.transform)))
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
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Finish(bool passed, Exception error)
        {
            if (error == null) Debug.Log("[Skills acceptance] " + (passed ? "passed" : "failed"));
            else Debug.LogException(error);
            EditorApplication.update -= OnUpdate;
            SessionState.SetBool(ActiveKey, false);
            SessionState.SetBool(ResultKey, passed);
            EditorApplication.isPlaying = false;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredEditMode) return;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.update -= OnUpdate;
            string root = SessionState.GetString(RootKey, string.Empty);
            if (Directory.Exists(root)) Directory.Delete(root, true);
            SessionState.SetBool(ActiveKey, false);
            if (Environment.GetEnvironmentVariable("ZJCS_SKILLS_AUTORUN") == "1")
            {
                bool passed = SessionState.GetBool(ResultKey, false);
                SessionState.SetBool(ResultKey, false);
                EditorApplication.delayCall += () => EditorApplication.Exit(passed ? 0 : 1);
            }
        }

        private sealed class Runner : MonoBehaviour
        {
            private void Update()
            {
                try
                {
                    if (!Input.GetMouseButtonDown(0)) return;
                    var pointer = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
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
