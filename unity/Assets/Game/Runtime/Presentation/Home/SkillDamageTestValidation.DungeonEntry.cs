using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;

namespace Zjcs.Presentation.Home
{
    public sealed partial class SkillDamageTestValidation
    {
        // Windows Player smoke flow; fixture changes are confined to its temporary save root.
        private static IEnumerator ExerciseDungeonEntry(string slot, string root, string output,
            Action<Selectable> click)
        {
            HomeFeatureRuntime home = HomeFeatureRuntime.Begin(slot, root, null);
            home.TutorialAutoStartEnabled = false;
            try
            {
                var regions = home.DailyDungeons.Catalog.dungeons
                    .Where(value => value != null && value.entryMapClassId > 0 && value.entryRegionId >= 0)
                    .Select(value => (map: value.entryMapClassId, region: value.entryRegionId)).Distinct().ToArray();
                foreach (var group in regions.GroupBy(value => value.map))
                    home.Profile.InitializeMap(group.Key, group.Select(value => value.region).ToArray());
                foreach (var region in regions)
                {
                    var result = home.Profile.SetMapRegionProgress(region.map, region.region, 10000);
                    Require(result.Status == ProgressionResultStatus.Applied
                        || result.Status == ProgressionResultStatus.Unchanged, "Could not prepare the isolated entry prerequisite.");
                }
                yield return new WaitForSecondsRealtime(1);
                click(home.Hud.CanvasTransform.GetComponentsInChildren<Selectable>().First(value =>
                    value.name == "UnlockObj" && value.transform.parent.name == "TravelNote"));
                yield return new WaitForSecondsRealtime(.5f);
                yield return CaptureEntry(output, "travel-entry");
                Transform travel = FindObjectsOfType<Transform>().Single(value => value.name == "TravelNotesPanel");
                click(travel.GetComponentsInChildren<Button>().First(value => value.name == "EntryBtn"
                    && value.GetComponentsInParent<Transform>().Any(parent => parent.name == "DailyDungeonEntry")));
                yield return new WaitForSecondsRealtime(.5f);
                Transform panel = FindObjectsOfType<Transform>().Single(value => value.name == "DailyDungeonEntryPanel");
                WorldAnimationSoundPlayer receiver = panel.GetComponentsInChildren<WorldAnimationSoundPlayer>(true).Single();
                Require(!receiver.gameObject.activeInHierarchy && receiver.mEvents.Count == 1
                    && receiver.mEvents[0].name == "TeamMatch_Waiting_1" && receiver.Post != null,
                    "The original no-match panel initialization or animation sound binding was not restored.");
                yield return CaptureEntry(output, "daily-entry");
                // Controlled presentation probe: checks the restored original event receiver/output,
                // without claiming that an online matchmaking session was started.
                receiver.PostEvent(0);
                yield return new WaitForSecondsRealtime(.1f);
                Require(FindObjectsOfType<AudioSource>().Any(value => value.clip == receiver.mEvents[0]
                    && value.isPlaying && value.spatialBlend == 0), "Original matching animation sound did not reach the shared 2D output.");
                int firstDungeon = home.DailyDungeons.PublicDungeons((map, region) => home.MapRegionProgress(map, region) >= 10000).First().id;
                click(panel.GetComponentsInChildren<Button>().Single(value => value.name == "Dungeon_" + firstDungeon));
                yield return new WaitForSecondsRealtime(.5f);
                Require(FindObjectsOfType<Button>().Any(value => value.name == "GoToBtn" && value.interactable),
                    "The daily entry click did not open its original dungeon dialog.");
                Transform dialog = FindObjectsOfType<Transform>().Single(value => value.name == "DailyDungeonDialog");
                WorldAnimationSoundPlayer dialogSound = dialog.GetComponentsInChildren<WorldAnimationSoundPlayer>(true).Single();
                Require(!dialogSound.gameObject.activeInHierarchy && dialogSound.Post != null
                    && dialogSound.mEvents.Count == 1 && dialogSound.mEvents[0] == receiver.mEvents[0],
                    "The original dungeon dialog did not share the restored no-match/sound bindings.");
                Button go = dialog.GetComponentsInChildren<Button>().Single(value => value.name == "GoToBtn" && value.isActiveAndEnabled);
                RectTransform goRect = go.transform as RectTransform;
                Vector2 center = RectTransformUtility.WorldToScreenPoint(null, goRect.TransformPoint(goRect.rect.center));
                Require(center.x > 0 && center.x < Screen.width && center.y > 0 && center.y < Screen.height,
                    "The original catalogue action was laid out outside the Player viewport.");
                yield return CaptureEntry(output, "daily-dialog");
                click(go);
                yield return new WaitForSecondsRealtime(.5f);
                Require(home.DailyDungeons.ActiveRun != null, "The catalogue action did not create a local dungeon route.");
                yield return CaptureEntry(output, "daily-route");
            }
            finally { if (home != null) home.CloseForNavigation(); }
        }

        private static IEnumerator CaptureEntry(string output, string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "dungeon-" + name + ".png"));
            yield return null;
        }
    }
}
