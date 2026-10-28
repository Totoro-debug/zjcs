using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Offline.State;

namespace Zjcs.Presentation.Character
{
    // Explicit Windows Player diagnostic; its fresh profile stays under a unique temporary directory.
    public sealed class SkillPlayerValidation : MonoBehaviour
    {
        private string output, saveRoot;
        private Exception runtimeFailure;
        private float deadline;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Initialize()
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--zjcs-validate-skills");
            if (index < 0 || index + 1 >= args.Length) return;
            Application.runInBackground = true;
            var host = new GameObject("SkillPlayerValidation");
            DontDestroyOnLoad(host);
            var runner = host.AddComponent<SkillPlayerValidation>();
            runner.output = Path.GetFullPath(args[index + 1]);
            runner.saveRoot = Path.Combine(Path.GetTempPath(), "zjcs-skills-player-" + Guid.NewGuid().ToString("N"));
            runner.deadline = Time.realtimeSinceStartup + 180f;
            Directory.CreateDirectory(runner.output);
            Application.logMessageReceived += runner.CaptureFailure;
            runner.StartCoroutine(runner.Guard(runner.Run()));
        }

        private void CaptureFailure(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert)
                runtimeFailure = new InvalidOperationException(message + "\n" + stackTrace);
        }

        private IEnumerator Guard(IEnumerator routine)
        {
            while (true)
            {
                object next;
                try
                {
                    Require(Time.realtimeSinceStartup < deadline, "Skills Player verification timed out.");
                    if (runtimeFailure != null) throw runtimeFailure;
                    if (!routine.MoveNext()) yield break;
                    next = routine.Current;
                }
                catch (Exception error)
                {
                    Application.logMessageReceived -= CaptureFailure;
                    File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(new Result { error = error.ToString() }, true));
                    Debug.LogException(error);
                    Application.Quit(1);
                    yield break;
                }
                yield return next;
            }
        }

        private IEnumerator Run()
        {
            // Let the real application complete its local startup before exercising the character entry.
            while (GameObject.Find("OfficialSaveSelectionPanel") == null) yield return null;
            var slots = new ProfileSlotCatalog(saveRoot);
            string slot = slots.CreateSlot();
            OfficialCharacterAssetCatalog assets = OfficialCharacterAssetCatalog.LoadDefault();
            var save = new OfflineMainlineSave();
            save.Player.CharacterCreated = true;
            save.Player.CharacterName = "技能验证";
            save.Player.Level = 24;
            save.Player.Rank = 1;
            save.Player.SubRank = 2;
            save.Player.Wardrobe.Profession = 1;
            save.Player.Experience = assets.Load().CumulativeLevelExperience[24];
            save.Player.Inventory.Add(new OfflineItemStack { ItemId = 42200, Count = 57 });
            save.Player.Inventory.Add(new OfflineItemStack { ItemId = 12000, Count = 120 });
            save.Player.Skills.Skills.Add(new OfflineOwnedSkill { ClassId = 10015, Rank = 1 });
            save.Player.Skills.SeniorMaterialSpentOnLevelUp = 500;
            File.WriteAllText(slots.GetProgressPath(slot), JsonUtility.ToJson(save, true));
            foreach (Canvas other in FindObjectsOfType<Canvas>()) other.gameObject.SetActive(false);
            foreach (Camera other in FindObjectsOfType<Camera>()) other.enabled = false;
            var page = new GameObject("SkillsPlayerCharacterEntry", typeof(RectTransform));
            var runtime = page.AddComponent<CharacterFeatureRuntime>();
            runtime.OpenProfile(slot, saveRoot);
            var progression = runtime.Progression;
            var recommendations = assets.LoadSkillRecommendations();
            OfflineSkillPlayerContext context = progression.SkillContext();
            foreach (int id in recommendations.recommendations.Where(value => context.CurrentProfessionChain.Contains(value.Profession))
                .SelectMany(value => (value.ActiveSkillList ?? Array.Empty<int>()).Concat(value.PassiveSkillList ?? Array.Empty<int>())).Distinct())
            {
                if (id != 10000 && OfflineSkillProgression.IsAvailableFor(progression.SkillDefinition(id), context)
                    && !progression.Player.Skills.Skills.Any(value => value.ClassId == id))
                    progression.Player.Skills.Skills.Add(new OfflineOwnedSkill { ClassId = id, Rank = 1 });
            }
            var panel = page.GetComponentInChildren<CharacterSkillPanel>(true);
            yield return null;
            Click(ButtonNamed("CharacterPanelSkillBtn"));
            Require(panel.IsOpen, "The Player skill entry did not open its full original panel.");
            yield return new WaitForSecondsRealtime(.8f);
            yield return new WaitForEndOfFrame(); Capture("skills-opening");
            RawImage head = page.GetComponentsInChildren<RawImage>(true).First(value => value.name == "HeadImg");
            Require(head.texture != null && head.uvRect.width > 0f, "The Player portrait is not bound.");
            Click(ButtonNamed("NextPlanBtn"));
            Require(progression.Player.Skills.SelectedPlan == 1, "The Player plan pointer click did not persist.");
            yield return null;
            Click(ButtonNamed("RecommendBtn"));
            yield return new WaitForSecondsRealtime(.5f);
            Button recommend = ButtonNamed("SaveBtn");
            Click(recommend);
            yield return new WaitForSecondsRealtime(.5f);
            Require(progression.Player.Skills.Plans[1].ActiveSkills.Any(value => value > 0), "The Player recommendation did not equip skills.");
            yield return null;
            Click(ButtonContaining("PassiveSkillTab"));
            yield return null;
            Click(ButtonContaining("SkillPieceTab"));
            yield return null;
            Click(ButtonContaining("ActiveSkillTab"));
            yield return new WaitForSecondsRealtime(.4f);
            yield return new WaitForEndOfFrame(); Capture("skills-equipped");
            Click(ButtonContaining("SkillListItem_"));
            yield return new WaitForSecondsRealtime(.7f);
            yield return new WaitForEndOfFrame(); Capture("skills-detail");
            Click(ButtonNamed("CollectionBtn"));
            yield return new WaitForSecondsRealtime(.3f);
            Require(FindObjectsOfType<Text>().Any(value => value.text == "已置顶该技能"),
                "The original Player skill collection message is absent.");
            yield return new WaitForEndOfFrame(); Capture("skills-collection-message");
            yield return new WaitForSecondsRealtime(.6f);
            Button mask = FindObjectsOfType<Button>().First(value => value.name == "BlurMask" && value.isActiveAndEnabled);
            Click(mask, new Vector2(8f, Screen.height * .5f));
            yield return null;
            Require(panel.IsOpen && !FindObjectsOfType<Button>().Any(value => value.name == "CollectionBtn" && value.isActiveAndEnabled),
                "The original Player popup outside click did not close its detail window.");
            Click(ButtonNamed("AchievementTabGroupItem_11"));
            yield return new WaitForSecondsRealtime(.4f);
            Click(ButtonNamed("ReceiveBtn"));
            yield return null;
            Require(progression.Player.Inventory.Where(value => value.ItemId == 41300).Sum(value => value.Count) == 60,
                "The Windows Player skill gift did not credit the original reward.");
            Require(!progression.ClaimSkillAchievements(33).IsApplied, "The Windows Player skill gift can be claimed twice.");
            yield return new WaitForSecondsRealtime(.4f);
            yield return new WaitForEndOfFrame(); Capture("skills-gifts");
            Click(ButtonNamed("AchievementTabGroupItem_14 (1)"));
            yield return new WaitForSecondsRealtime(.4f);
            yield return new WaitForEndOfFrame(); Capture("skills-quality-gifts");
            long qualityRewardBefore = progression.Player.Inventory.Where(value => value.ItemId == 100).Sum(value => value.Count);
            Click(ButtonNamed("ReceiveBtn"));
            yield return null;
            Require(progression.Player.Inventory.Where(value => value.ItemId == 100).Sum(value => value.Count) == qualityRewardBefore + 1
                && progression.Player.AppliedCharacterEventIds.Contains("skill-achievement:9:0"),
                "The Windows Player quality gift did not credit and record its original reward.");
            Click(ButtonNamed("CharacterPanelSkillDetailPanelSkillTabGroupItem_11"));
            yield return new WaitForSecondsRealtime(.4f);
            var reopened = OfflineMainlineProgressSession.InspectCharacter(new ProfileSlotCatalog(saveRoot), slot);
            Require(reopened.Save.Player.Skills.SelectedPlan == 1
                && reopened.Save.Player.Skills.Plans[1].ActiveSkills.SequenceEqual(progression.Player.Skills.Plans[1].ActiveSkills),
                "The Windows Player skill plan did not survive profile reload.");
            Require(reopened.Save.Player.Skills.SeniorMaterialSpentOnLevelUp == 500
                && reopened.Save.Player.AppliedCharacterEventIds.Contains("skill-achievement:33:0")
                && reopened.Save.Player.Inventory.Where(value => value.ItemId == 41300).Sum(value => value.Count) == 60,
                "The Windows Player skill gift spend counter, reward or claim receipt did not survive reload.");
            Click(ButtonNamed("CloseBtn_dark"));
            Require(!panel.IsOpen, "The Player skill return button did not restore the character page.");
            yield return new WaitForSecondsRealtime(.4f);
            yield return new WaitForEndOfFrame(); Capture("character-return");
            Application.logMessageReceived -= CaptureFailure;
            File.WriteAllText(Path.Combine(output, "result.json"), JsonUtility.ToJson(new Result { passed = true }, true));
            Debug.Log("Skills Windows Player validation passed: native portrait, full panel entry, plans, recommendation, tabs, original return, isolated save reload.");
            Application.Quit(0);
        }

        private static Button ButtonNamed(string name) => FindPointerTarget(value => value.name == name, name);
        private static Button ButtonContaining(string name) => FindPointerTarget(value => value.name.Contains(name), name);
        private static Button FindPointerTarget(Func<Button, bool> matches, string description)
        {
            Canvas.ForceUpdateCanvases();
            var blocked = new List<string>();
            foreach (Button control in FindObjectsOfType<Button>().Where(value => matches(value) && value.isActiveAndEnabled))
            {
                RectTransform rect = (RectTransform)control.transform;
                var pointer = new PointerEventData(EventSystem.current) {
                    position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(pointer, hits);
                if (hits.Count > 0 && (hits[0].gameObject == control.gameObject
                    || hits[0].gameObject.transform.IsChildOf(control.transform))) return control;
                blocked.Add(control.name + " at " + pointer.position + " hit "
                    + (hits.Count == 0 ? "<none>" : hits[0].gameObject.name + " in " + hits[0].module));
            }
            throw new InvalidOperationException("No unobstructed Player button matches " + description + ": " + string.Join("; ", blocked));
        }
        private static void Click(Button control, Vector2? screenPosition = null)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform rect = (RectTransform)control.transform;
            var pointer = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
                position = screenPosition ?? RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center)) };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Require(hits.Count > 0 && (hits[0].gameObject == control.gameObject || hits[0].gameObject.transform.IsChildOf(control.transform)),
                "The Player control is obstructed: " + control.name);
            ExecuteEvents.Execute(control.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(control.gameObject, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(control.gameObject, pointer, ExecuteEvents.pointerClickHandler);
        }
        private void Capture(string name)
        {
            Texture2D texture = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), texture.EncodeToPNG()); Destroy(texture);
        }
        private void OnDestroy()
        {
            Application.logMessageReceived -= CaptureFailure;
            if (!string.IsNullOrEmpty(saveRoot) && Directory.Exists(saveRoot)) Directory.Delete(saveRoot, true);
        }
        private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
        [Serializable] private sealed class Result { public bool passed; public string error; }
    }
}
