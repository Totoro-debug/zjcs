using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.World;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static partial class OfficialMap12PlayModeAcceptance
    {
        [Serializable] private sealed class DifficultyLanguageDocument { public DifficultyLanguageStyles routeStyles; }
        [Serializable] private sealed class DifficultyLanguageStyles { public DifficultyLanguageValue[] texts; }
        [Serializable] private sealed class DifficultyLanguageValue { public string key, value; }

        private static IEnumerator VerifyPrivateDifficultyInput(GameObject dialog, string profilePath, string output)
        {
            var controller = dialog.GetComponent<OfficialDungeonPrivateDifficultyController>();
            var group = dialog.GetComponentsInChildren<WorldTabGroup>(true).Single();
            var tabs = group.GetAllTabs().OrderBy(value => value.transform.GetSiblingIndex()).ToArray();
            var session = RuntimeField<OfflineDailyDungeonSession>("map12PrivateDungeonSession");
            byte[] profileBefore = File.ReadAllBytes(profilePath);
            string savePath = Path.Combine(Path.GetDirectoryName(profilePath), "daily-dungeon-save.json");
            if (controller == null || controller.Difficulty != 0 || tabs.Length != 5
                || !tabs[0].isOn || tabs.Skip(1).Any(value => value.isOn)
                || tabs.Count(value => value.gameObject.activeInHierarchy) != 2
                || session.Save.unlocked.Any(value => value.dungeonId == 21) || File.Exists(savePath))
                throw new InvalidDataException("Original private difficulty initial/visible/locked state differs.");
            // Resolve by its original language key, without assuming a duplicate object name.
            WorldMultiLanguageText description = dialog.GetComponentsInChildren<WorldMultiLanguageText>(true)
                .Single(value => value.myKey == "ui_daily_dungeon_dialog_desc_content_21_Normal");
            TMPro.TMP_FontAsset officialFont = Resources.Load<TMPro.TMP_FontAsset>("Combat/FZY4K-Dynamic");
            if (officialFont == null || description.GetComponent<TMPro.TMP_Text>().font != officialFont)
                throw new InvalidDataException("Original private dungeon dialog did not bind its packaged Chinese font.");
            TMPro.TMP_Text descriptionText = description.GetComponent<TMPro.TMP_Text>();
            descriptionText.ForceMeshUpdate(true, true);
            char[] missingCharacters = descriptionText.text.Where(value => !char.IsWhiteSpace(value) && !officialFont.HasCharacter(value))
                .Distinct().ToArray();
            if (missingCharacters.Length > 0)
                throw new InvalidDataException("Original private dungeon description contains characters missing from its packaged font: "
                    + new string(missingCharacters));
            Button locked = tabs[1].transform.GetChild(3).GetComponent<Button>();
            if (locked == null || !locked.gameObject.activeInHierarchy)
                throw new InvalidDataException("Original next-difficulty lock-tip overlay is missing.");
            SimulateUiClick(locked, locked.GetComponentInParent<Canvas>().worldCamera);
            yield return null;
            if (controller.Difficulty != 0 || !tabs[0].isOn || description.myKey != "ui_daily_dungeon_dialog_desc_content_21_Normal")
                throw new InvalidDataException("Locked difficulty pointer changed the selected difficulty.");
            var language = JsonUtility.FromJson<DifficultyLanguageDocument>(File.ReadAllText(Path.Combine(
                Application.streamingAssetsPath, "Dungeons", "UI", "official-bindings.json"))).routeStyles.texts;
            string lockedMessage = string.Format(language.Single(value => value.key == "ui_daily_dungeon_difficulty_lock_tip_text").value,
                language.Single(value => value.key == "ui_daily_dungeon_item_title_21_Normal").value);
            if (!UnityEngine.Object.FindObjectsOfType<TMPro.TMP_Text>().Any(value => value.text == lockedMessage))
                throw new InvalidDataException("Locked difficulty pointer did not display the original preceding-difficulty notice.");

            // Fixture setup only: emulate an already cleared preceding difficulty in the isolated
            // in-memory session; subsequent selection must still pass the real UI raycast handlers.
            var preceding = new DailyDungeonDifficultySave { dungeonId = 21, difficulty = 0 };
            session.Save.unlocked.Add(preceding);
            try
            {
                controller.enabled = false; controller.enabled = true;
                yield return null;
                if (locked.gameObject.activeInHierarchy)
                    throw new InvalidDataException("Cleared preceding difficulty did not release the original lock overlay.");
                SimulatePrivateDifficultyTab(tabs[1]);
                yield return new WaitForSeconds(.25f);
                if (controller.Difficulty != 1 || !tabs[1].isOn || tabs.Where((_, index) => index != 1).Any(value => value.isOn)
                    || description.myKey != "ui_daily_dungeon_dialog_desc_content_21_Hard")
                    throw new InvalidDataException("Original difficulty pointer did not update exclusive selection and detail language key.");
                var animation = tabs[1].GetComponent<WorldTabAnimatorTransition>();
                if (animation == null || animation.Anim == null || !animation.Anim.isActiveAndEnabled
                    || !animation.Anim.GetCurrentAnimatorStateInfo(0).IsName(animation.ToggleOn))
                    throw new InvalidDataException("Selected private difficulty did not enter its original ToggleOn animation.");
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(output, "iron-mount-private-difficulty-hard.png"));
                SimulatePrivateDifficultyTab(tabs[0]);
                yield return null;
                if (controller.Difficulty != 0 || !tabs[0].isOn || tabs[1].isOn
                    || description.myKey != "ui_daily_dungeon_dialog_desc_content_21_Normal")
                    throw new InvalidDataException("Original difficulty pointer could not return to Normal.");
            }
            finally
            {
                session.Save.unlocked.Remove(preceding);
                controller.enabled = false; controller.enabled = true;
            }
            if (!File.ReadAllBytes(profilePath).SequenceEqual(profileBefore) || File.Exists(savePath)
                || session.ActiveRun != null || session.Save.usedEntries != 0 || session.Save.purchasedEntries != 0)
                throw new InvalidDataException("Difficulty preview changed the profile, route or dungeon counters.");
        }

        private static void SimulatePrivateDifficultyTab(WorldTabItem tab)
        {
            Canvas.ForceUpdateCanvases();
            RectTransform rect = (RectTransform)tab.transform;
            Camera camera = tab.GetComponentInParent<Canvas>().worldCamera;
            var pointer = new PointerEventData(EventSystem.current) {
                position = RectTransformUtility.WorldToScreenPoint(camera, rect.TransformPoint(rect.rect.center)),
                button = PointerEventData.InputButton.Left
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            GameObject hit = hits.FirstOrDefault().gameObject;
            if (hit == null || ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit) != tab.gameObject)
                throw new InvalidDataException("Original difficulty tab is occluded at its native input point: " + tab.name);
            GameObject pressed = ExecuteEvents.ExecuteHierarchy(hit, pointer, ExecuteEvents.pointerDownHandler);
            pointer.pointerPress = pressed;
            ExecuteEvents.Execute(pressed, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(pressed, pointer, ExecuteEvents.pointerClickHandler);
        }
    }
}
