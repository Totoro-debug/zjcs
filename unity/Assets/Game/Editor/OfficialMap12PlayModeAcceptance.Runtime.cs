using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Offline.World;
using Zjcs.Presentation.CharacterCreation;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static partial class OfficialMap12PlayModeAcceptance
    {
        private static OfficialMainlineMapRuntime map12Runtime;
        private static T RuntimeField<T>(string key) => (T)OfficialMainlineMapInspection.Get(map12Runtime, key);
        private static object RuntimeCall(string key, params object[] arguments) =>
            OfficialMainlineMapInspection.Call(map12Runtime, key, arguments);

        private static IEnumerator VerifyRuntimeSteps()
        {
            Application.runInBackground = true;
            foreach (MonoBehaviour startup in UnityEngine.Object.FindObjectsOfType<MonoBehaviour>()
                .Where(value => value.GetType().FullName == "Zjcs.Offline.Startup.OfflineStartupFlow"))
            {
                startup.StopAllCoroutines();
                UnityEngine.Object.Destroy(startup.gameObject);
            }
            SetUiGameViewSize(450, 800);
            yield return null;
            if (EventSystem.current == null) new GameObject("Map12SimulatedInput", typeof(EventSystem));
            var canvas = new GameObject("Map12RuntimeFixture", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            string root = Path.GetFullPath("../.tools/map12-runtime-profile-session-" + Guid.NewGuid().ToString("N"));
            if (Directory.Exists(root)) throw new IOException("Runtime fixture profile must be new: " + root);
            var slots = new ProfileSlotCatalog(root);
            string slot = slots.CreateSlot();
            var save = new OfflineMainlineSave { CreatedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
            save.Player.CharacterCreated = true;
            save.Player.CharacterName = "山之国验证";
            save.Player.InitialWeaponType = 200;
            save.Player.Level = 20;
            save.Player.Experience = OfficialCharacterAssetCatalog.LoadDefault().Load().CumulativeLevelExperience[20];
            save.Player.Rank = save.Player.SubRank = save.Player.SeasonRank = 1;
            save.Player.Wardrobe.Profession = 1;
            save.Player.UnlockedMapClassIds.AddRange(new[] { 11, 12 });
            if (UnityEditor.SessionState.GetBool(Key + ".runtimeTerminals", false))
                OfflineInventoryOperations.Grant(save.Player, 325, 6, DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            save.Tasks.ClaimedMainlineTaskIds.AddRange(OfficialMainlineTaskCatalog.LoadDefault().OrderedTasks
                .Where(value => value.classId <= 1040).Select(value => value.classId));
            File.WriteAllText(slots.GetProgressPath(slot), JsonUtility.ToJson(save, true));
            var progression = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            new OfflineCharacterProgressionService(progression, OfficialCharacterAssetCatalog.LoadDefault().Load())
                .InitializeWardrobe("map12-runtime-fixture-wardrobe");
            GameObject avatar = CharacterCreationNativeModelFactory.CreateWorldModel(slot, root, out string name);
            map12Runtime = new GameObject("Map12Runtime").AddComponent<OfficialMainlineMapRuntime>();
            map12Runtime.Begin(12, slot, root, canvas.transform, avatar, name, null);
            yield return new WaitForSecondsRealtime(1);
            if (!RuntimeField<MapGridPoint>("currentGrid").Equals(new MapGridPoint(248, 32)))
                throw new InvalidDataException("Original Map12 entry 101 is not used.");
            string output = Path.GetFullPath("../.tools/map12-runtime-render-session");
            Directory.CreateDirectory(output);
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "entry.png"));
            yield return new WaitForSecondsRealtime(.5f);

            if (UnityEditor.SessionState.GetBool(Key + ".runtimeTerminals", false))
            {
                IEnumerator terminals = VerifyTerminalSteps(root, slot, canvas.transform, output);
                while (terminals.MoveNext()) yield return terminals.Current;
                RuntimeCall("CloseMap");
                yield break;
            }

            if (!UnityEditor.SessionState.GetBool(Key + ".runtimeDialogue", false))
            {
                // Fixture placement is separate from input validation. Actual movement
                // and box selection below use the same map pointer provider as a click.
                var plans = JsonUtility.FromJson<RuntimePlans>(File.ReadAllText("../scripts/testdata/map12-push-plans.json"));
                RuntimePush first = plans.Plans[0].Actions[0];
                var adjacent = new MapGridPoint(first.PlayerX, first.PlayerY);
                RuntimeCall("SetAvatarPosition", adjacent);
                Camera camera = RuntimeField<Camera>("worldCamera");
                camera.transform.position = RuntimeField<GameObject>("avatar").transform.position + new Vector3(0, 0, -20);
                yield return new WaitForSecondsRealtime(.5f);
                OfflineMapPuzzleSession puzzle = RuntimeField<OfflineMapPuzzleSession>("puzzles");
                int box = puzzle.Rules.Single(value => value.HasPushResult
                    && puzzle.Position(value.PlacementId).Equals(new MapGridPoint(first.BoxX, first.BoxY))).PlacementId;
                GameObject target = map12Runtime.GetComponentInChildren<OfficialMap10EntityView>().FindEntityObject(box);
                string mapPath = Path.Combine(slots.GetSlotDirectory(slot), "map-12-save.json");
                using (var denied = new FileStream(mapPath, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    Vector2? failedPointer = camera.WorldToScreenPoint(target.transform.position);
                    map12Runtime.PointerPressProvider = () => { Vector2? next = failedPointer; failedPointer = null; return next; };
                    yield return new WaitForSecondsRealtime(.5f);
                    puzzle = RuntimeField<OfflineMapPuzzleSession>("puzzles");
                    if (!puzzle.Position(box).Equals(new MapGridPoint(first.BoxX, first.BoxY))
                        || !RuntimeField<MapGridPoint>("currentGrid").Equals(adjacent))
                        throw new InvalidDataException("Denied map write did not roll back the actual pointer push.");
                }
                Vector2? pointer = camera.WorldToScreenPoint(target.transform.position);
                map12Runtime.PointerPressProvider = () => { Vector2? next = pointer; pointer = null; return next; };
                yield return new WaitForSecondsRealtime(1);
                var followed = new MapGridPoint(first.BoxX, first.BoxY);
                if (!RuntimeField<MapGridPoint>("currentGrid").Equals(followed)
                    || !puzzle.Position(box).Equals(new MapGridPoint(first.BoxX * 2 - first.PlayerX, first.BoxY * 2 - first.PlayerY)))
                    throw new InvalidDataException("Map pointer did not push in the selected direction/follow the original box.");
                var stored = JsonUtility.FromJson<RuntimeSave>(File.ReadAllText(mapPath));
                MapPuzzleEntityState state = stored.puzzleStates.Single(value => value.PlacementId == box);
                if (stored.gridX != followed.X || stored.gridY != followed.Y
                    || state.GridX != puzzle.Position(box).X || state.GridY != puzzle.Position(box).Y)
                    throw new InvalidDataException("Runtime push did not persist player and box coordinates.");
                ScreenCapture.CaptureScreenshot(Path.Combine(output, "push.png"));
                yield return new WaitForSecondsRealtime(.5f);
                int pushCount = 1;
                foreach (RuntimePlan plan in plans.Plans)
                {
                    foreach (RuntimePush action in plan.Actions.Skip(plan == plans.Plans[0] ? 1 : 0))
                    {
                        RuntimeCall("SetAvatarPosition", new MapGridPoint(action.PlayerX, action.PlayerY));
                        camera.transform.position = RuntimeField<GameObject>("avatar").transform.position + new Vector3(0, 0, -20);
                        box = puzzle.Rules.Single(value => value.HasPushResult && puzzle.IsSpawned(value.PlacementId)
                            && puzzle.Position(value.PlacementId).Equals(new MapGridPoint(action.BoxX, action.BoxY))).PlacementId;
                        target = map12Runtime.GetComponentInChildren<OfficialMap10EntityView>().FindEntityObject(box);
                        if (action == plan.Actions.Last())
                        {
                            byte[] profileBefore = File.ReadAllBytes(slots.GetProgressPath(slot));
                            using (var denied = new FileStream(mapPath, FileMode.Open, FileAccess.Read, FileShare.None))
                            {
                                Vector2? deniedPointer = camera.WorldToScreenPoint(target.transform.position);
                                map12Runtime.PointerPressProvider = () => { Vector2? next = deniedPointer; deniedPointer = null; return next; };
                                yield return new WaitForSecondsRealtime(.2f);
                                puzzle = RuntimeField<OfflineMapPuzzleSession>("puzzles");
                                if (!puzzle.Position(box).Equals(new MapGridPoint(action.BoxX, action.BoxY))
                                    || puzzle.State(plan.LockId) != 0
                                    || !File.ReadAllBytes(slots.GetProgressPath(slot)).SequenceEqual(profileBefore))
                                    throw new InvalidDataException("Denied final push advanced puzzle or global region progress: " + plan.LockId
                                        + "; box=" + puzzle.Position(box) + "; lock=" + puzzle.State(plan.LockId)
                                        + "; profileChanged=" + !File.ReadAllBytes(slots.GetProgressPath(slot)).SequenceEqual(profileBefore));
                            }
                            puzzle = RuntimeField<OfflineMapPuzzleSession>("puzzles");
                        }
                        bool denyProfile = plan == plans.Plans[0] && action == plan.Actions.Last();
                        byte[] profileBeforeCommit = denyProfile ? File.ReadAllBytes(slots.GetProgressPath(slot)) : null;
                        using (var denied = denyProfile
                            ? new FileStream(slots.GetProgressPath(slot), FileMode.Open, FileAccess.Read, FileShare.None) : null)
                        {
                            pointer = camera.WorldToScreenPoint(target.transform.position);
                            map12Runtime.PointerPressProvider = () => { Vector2? next = pointer; pointer = null; return next; };
                            yield return new WaitForSecondsRealtime(.15f);
                            if (!RuntimeField<MapGridPoint>("currentGrid").Equals(new MapGridPoint(action.BoxX, action.BoxY))
                                || !puzzle.Position(box).Equals(new MapGridPoint(action.BoxX * 2 - action.PlayerX, action.BoxY * 2 - action.PlayerY)))
                                throw new InvalidDataException("Actual pointer push failed at step " + pushCount + ", box " + box);
                            if (denyProfile && (!RuntimeField<bool>("puzzleRegionProgressPending")
                                || JsonUtility.FromJson<RuntimeSave>(File.ReadAllText(mapPath)).puzzleStates
                                    .Single(value => value.PlacementId == plan.LockId).State != 1))
                                throw new InvalidDataException("Derived profile write failure rolled back a committed map.");
                        }
                        if (denyProfile)
                        {
                            yield return new WaitForSecondsRealtime(2.2f);
                            if (RuntimeField<bool>("puzzleRegionProgressPending")
                                || File.ReadAllBytes(slots.GetProgressPath(slot)).SequenceEqual(profileBeforeCommit))
                                throw new InvalidDataException("Committed puzzle region progress was not retried after profile write recovery.");
                        }
                        pushCount++;
                    }
                    if (puzzle.State(plan.LockId) != 1) throw new InvalidDataException("Runtime push lock did not open: " + plan.LockId);
                }
                if (pushCount != 58) throw new InvalidDataException("All original push plans were not exercised.");
                Debug.Log("Map12 four push locks passed all 58 simulated map pointer steps, including selected direction and following.");
                MapPuzzleEntityState[] solved = puzzle.Save();
                followed = RuntimeField<MapGridPoint>("currentGrid");
                RuntimeCall("CloseMap");
                yield return null;
                avatar = CharacterCreationNativeModelFactory.CreateWorldModel(slot, root, out name);
                map12Runtime = new GameObject("Map12RuntimeColdLoad").AddComponent<OfficialMainlineMapRuntime>();
                map12Runtime.Begin(12, slot, root, canvas.transform, avatar, name, null);
                yield return new WaitForSecondsRealtime(.5f);
                puzzle = RuntimeField<OfflineMapPuzzleSession>("puzzles");
                if (!RuntimeField<MapGridPoint>("currentGrid").Equals(followed)
                    || solved.Any(value => puzzle.State(value.PlacementId) != value.State
                        || !puzzle.Position(value.PlacementId).Equals(new MapGridPoint(value.GridX, value.GridY))))
                    throw new InvalidDataException("Runtime cold load did not restore pushed coordinates.");
            }
            {
                OfflineMapPuzzleSession puzzle = RuntimeField<OfflineMapPuzzleSession>("puzzles");
                Camera camera;
                GameObject target;
                Vector2? pointer;
                foreach (int terminal in new[] { 746, 5220 })
                {
                    // This terminal fixture supplies prerequisite region completion;
                    // it is separate from verification of a new player's route.
                    foreach (var maximum in RuntimeField<Dictionary<int, int>>("regionPointLimits"))
                        RuntimeField<Dictionary<int, int>>("regionPoints")[maximum.Key] = maximum.Value;
                    RuntimeCall("RefreshEntityActivity");
                    MapGridPoint location = puzzle.Position(terminal);
                    MapGridPoint stand = new[] { new MapGridPoint(location.X, location.Y - 1),
                        new MapGridPoint(location.X - 1, location.Y), new MapGridPoint(location.X, location.Y + 1),
                        new MapGridPoint(location.X + 1, location.Y) }.First(RuntimeField<OfficialWorldMapSession>("session").Movement.IsFit);
                    RuntimeCall("SetAvatarPosition", stand);
                    camera = RuntimeField<Camera>("worldCamera");
                    camera.transform.position = RuntimeField<GameObject>("avatar").transform.position + new Vector3(0, 0, -20);
                    target = map12Runtime.GetComponentInChildren<OfficialMap10EntityView>().FindEntityObject(terminal);
                    pointer = camera.WorldToScreenPoint(target.transform.position);
                    map12Runtime.PointerPressProvider = () => { Vector2? next = pointer; pointer = null; return next; };
                    yield return new WaitForSecondsRealtime(.4f);
                    var ui = RuntimeField<OfficialWorldUiRenderer>("ui");
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "terminal-opening-" + terminal + ".png"));
                    for (int advances = 0; !RuntimeField<bool>("storyOptionsWaiting") && advances < 30; advances++)
                    {
                        Button next = new[] { "OptionTalkDialog", "TalkDialog" }.Select(panel => ui.Find(panel, "/" + panel + "/NextBtn"))
                            .Where(value => value != null && value.activeInHierarchy).Select(value => value.GetComponent<Button>()).FirstOrDefault();
                        if (next != null)
                        {
                            yield return new WaitForEndOfFrame();
                            SimulateUiClick(next, next.GetComponentInParent<Canvas>().worldCamera);
                        }
                        yield return new WaitForSecondsRealtime(.2f);
                    }
                    if (!RuntimeField<bool>("storyOptionsWaiting")) throw new InvalidDataException("Terminal options were not reached: " + terminal
                        + "; active=" + target.activeInHierarchy + "; dialogueWaiting=" + RuntimeField<bool>("dialogueWaiting")
                        + "; grid=" + RuntimeField<MapGridPoint>("currentGrid") + "; clickable=" + puzzle.ContainsClickableBody(terminal, location)
                        + "; buttons=" + string.Join(",", ui.Find("OptionTalkDialog", "/OptionTalkDialog")
                            .GetComponentsInChildren<Button>(true).Select(value => value.name + ":" + value.gameObject.activeInHierarchy)));
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "terminal-" + terminal + ".png"));
                    Button reset = ui.Find("OptionTalkDialog", "/OptionTalkDialog/OptionSlots/Content/OptionItem  (1)").GetComponent<Button>();
                    yield return new WaitForEndOfFrame();
                    SimulateUiClick(reset, reset.GetComponentInParent<Canvas>().worldCamera);
                    yield return new WaitForSecondsRealtime(.3f);
                    if (RuntimeField<bool>("storyOptionsWaiting")) throw new InvalidDataException("Native reset option did not close.");
                    MapGridPoint after = RuntimeField<MapGridPoint>("currentGrid");
                    if (Math.Abs(after.X - location.X) + Math.Abs(after.Y - location.Y) != 1)
                        throw new InvalidDataException("Runtime Huarong reset did not return beside its terminal.");
                }
            }
            RuntimeCall("CloseMap");
            yield return null;
        }

        [Serializable] private sealed class RuntimeSave { public int gridX, gridY; public MapPuzzleEntityState[] puzzleStates; }
        [Serializable] private sealed class RuntimePlans { public RuntimePlan[] Plans; }
        [Serializable] private sealed class RuntimePlan { public int LockId, TerminalId; public RuntimePush[] Actions; }
        [Serializable] private sealed class RuntimePush { public int BoxX, BoxY, PlayerX, PlayerY; }
    }
}
