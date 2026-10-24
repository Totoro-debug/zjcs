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
            save.Player.Wardrobe.Profession = 1;
            save.Player.Inventory.Add(new OfflineItemStack { ItemId = 12000, Count = 120 });
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
            string root = SessionState.GetString(RootKey, string.Empty);
            string slot = SessionState.GetString(SlotKey, string.Empty);
            ProfileSlotCatalog slots = null;
            OfflineCharacterProgressionService progression = null;
            GameObject canvasObject = null;
            GameObject eventSystem = null;
            bool ownsEventSystem = false;
            Exception error = null;
            try
            {
                slots = new ProfileSlotCatalog(root);
                OfflineMainlineProgressSession session = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
                OfficialCharacterAssetCatalog assets = OfficialCharacterAssetCatalog.LoadDefault();
                progression = new OfflineCharacterProgressionService(session, assets.Load());
                OfficialSkillRecommendationSet recommendations = assets.LoadSkillRecommendations();
                progression.AttachSkillRules(assets.LoadSkillRules(), recommendations.recommendations);

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
                var panel = canvasObject.AddComponent<CharacterSkillPanel>();
                panel.Initialize(assets, progression);
                panel.Open(portrait.transform);
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
                nextPlanButton.onClick.AddListener(() => Debug.Log("[Skills acceptance] NextPlanBtn onClick invoked."));
                Vector2 buttonPosition = RectTransformUtility.WorldToScreenPoint(null,
                    nextPlanButton.transform.position);
                var pointer = new PointerEventData(EventSystem.current) { position = buttonPosition };
                var raycasts = new System.Collections.Generic.List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, raycasts);
                string topHit = raycasts.Count == 0 ? "<none>" : raycasts[0].gameObject.name;
                Debug.Log("[Skills acceptance] skill panel ready; mouse target=" + buttonPosition
                    + "; screen=" + Screen.width + "x" + Screen.height + "; hit=" + topHit
                    + "; button=" + nextPlanButton.interactable + "; screenshot=" + screenshot);
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

            float inputDeadline = Time.realtimeSinceStartup + 300f;
            while (progression.Player.Skills.SelectedPlan == selectedPlan
                && Time.realtimeSinceStartup < inputDeadline) yield return null;
            try
            {
                Require(progression.Player.Skills.SelectedPlan != selectedPlan,
                    "Mouse input did not switch the skill plan before the deadline.");

                OfflineSkillDefinition skill = progression.SkillDefinition(10000);
                Require(skill != null && skill.PieceItemId == 12000, "Official skill or piece rule is unavailable.");
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
                Debug.Log("[Skills acceptance] official skill panel rendered; plan switched, skill merged and equipped, profile reload passed. Screenshot: " + screenshot);
            }
            catch (Exception exception)
            {
                error = exception;
            }
            UnityEngine.Object.Destroy(canvasObject);
            if (ownsEventSystem) UnityEngine.Object.Destroy(eventSystem);
            Finish(error == null, error);
        }

        private static Button FindButton(string name)
        {
            return UnityEngine.Object.FindObjectsOfType<Button>(true).FirstOrDefault(value => value.name == name);
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
