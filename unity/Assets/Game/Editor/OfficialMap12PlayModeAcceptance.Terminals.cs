using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
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
        private static IEnumerator VerifyTerminalSteps(string root, string slot, Transform canvas, string output)
        {
            foreach (int id in new[] { 746, 5220, 1416, 4631, 6326, 8721 })
            {
                var puzzle = RuntimeField<OfflineMapPuzzleSession>("puzzles");
                MapPuzzleRule terminal = puzzle.Rules.Single(value => value.PlacementId == id);
                int[] boxes = terminal.TerminalBoxIds.Concat(terminal.TerminalLockIds.SelectMany(lockId =>
                    puzzle.Rules.Single(value => value.PlacementId == lockId).PushBoxIds)).Distinct().ToArray();
                int[] restored = terminal.TerminalRemoveIds.Concat(boxes).Distinct().ToArray();
                foreach (int reward in terminal.TerminalLockIds.SelectMany(lockId =>
                    puzzle.Rules.Single(value => value.PlacementId == lockId).SpawnIds))
                    if (puzzle.IsSpawned(reward))
                        throw new InvalidDataException("Deferred puzzle reward spawned before its unlock: " + reward);
                var session = RuntimeField<OfficialWorldMapSession>("session");
                bool moved = false;
                foreach (int box in boxes)
                {
                    MapGridPoint position = puzzle.Position(box);
                    foreach (MapGridPoint offset in new[] { new MapGridPoint(0, -1), new MapGridPoint(-1, 0),
                        new MapGridPoint(0, 1), new MapGridPoint(1, 0) })
                    {
                        var stand = new MapGridPoint(position.X + offset.X, position.Y + offset.Y);
                        if (!session.Movement.IsFit(stand)) continue;
                        var copy = new OfflineMapPuzzleSession(puzzle.Rules.ToArray(), puzzle.Save());
                        if (!copy.TryPush(box, stand, session.Definition.Navigation, out _)) continue;
                        RuntimeCall("SetAvatarPosition", stand);
                        IEnumerator input = ClickTerminalMapEntity(box);
                        while (input.MoveNext()) yield return input.Current;
                        puzzle = RuntimeField<OfflineMapPuzzleSession>("puzzles");
                        if (puzzle.Position(box).Equals(position))
                            throw new InvalidDataException("Original map pointer did not move the reset fixture box: " + box);
                        moved = true;
                        break;
                    }
                    if (moved) break;
                }
                if (!moved) throw new InvalidDataException("No legal original box move for terminal " + id);

                IEnumerator open = OpenTerminalOptions(id);
                while (open.MoveNext()) yield return open.Current;
                Button reset = TerminalOption("重置谜题");
                yield return new WaitForEndOfFrame();
                SimulateUiClick(reset, reset.GetComponentInParent<Canvas>().worldCamera);
                yield return new WaitForSecondsRealtime(.3f);
                puzzle = RuntimeField<OfflineMapPuzzleSession>("puzzles");
                if (RuntimeField<bool>("storyOptionsWaiting") || RuntimeField<bool>("storyRunning"))
                    throw new InvalidDataException("Original reset did not finish: " + id);
                foreach (int box in restored)
                {
                    MapPuzzleRule initial = puzzle.Rules.Single(value => value.PlacementId == box);
                    if (!puzzle.Position(box).Equals(new MapGridPoint(initial.GridX, initial.GridY))
                        || puzzle.State(box) != initial.InitialState || puzzle.IsSpawned(box) != initial.InitiallySpawned)
                        throw new InvalidDataException("Reset did not restore the original union member " + box);
                }
                MapGridPoint at = RuntimeField<MapGridPoint>("currentGrid"), location = puzzle.Position(id);
                if (Math.Abs(at.X - location.X) + Math.Abs(at.Y - location.Y) != 1)
                    throw new InvalidDataException("Reset did not return next to the terminal: " + id);
                IEnumerator cold = ColdLoadTerminalRuntime(root, slot, canvas);
                while (cold.MoveNext()) yield return cold.Current;

                open = OpenTerminalOptions(id);
                while (open.MoveNext()) yield return open.Current;
                Button help = TerminalOption("帮我解谜");
                yield return new WaitForEndOfFrame();
                SimulateUiClick(help, help.GetComponentInParent<Canvas>().worldCamera);
                IEnumerator advance = AdvanceTerminalOptions();
                while (advance.MoveNext()) yield return advance.Current;
                Button submit = TerminalOption("提交");
                long before = TerminalTicketCount();
                if (before < 1) throw new InvalidDataException("The isolated terminal fixture has no ticket.");
                yield return new WaitForEndOfFrame();
                SimulateUiClick(submit, submit.GetComponentInParent<Canvas>().worldCamera);
                yield return new WaitForSecondsRealtime(.5f);
                puzzle = RuntimeField<OfflineMapPuzzleSession>("puzzles");
                if (RuntimeField<bool>("storyRunning") || TerminalTicketCount() != before - 1)
                    throw new InvalidDataException("Original auto-solve did not consume exactly one item 325: " + id);
                foreach (int removed in terminal.TerminalRemoveIds)
                    if (puzzle.IsSpawned(removed)) throw new InvalidDataException("Auto-solve retained a required obstacle: " + removed);
                foreach (int retained in terminal.TerminalBoxIds.Except(terminal.TerminalRemoveIds))
                    if (!puzzle.IsSpawned(retained)) throw new InvalidDataException("Auto-solve removed a box outside NeedRemove: " + retained);
                foreach (int lockId in terminal.TerminalLockIds)
                {
                    if (!puzzle.IsUnlocked(lockId)) throw new InvalidDataException("Auto-solve did not unlock the designated lock: " + lockId);
                    foreach (int reward in puzzle.Rules.Single(value => value.PlacementId == lockId).SpawnIds)
                        if (!puzzle.IsSpawned(reward)) throw new InvalidDataException("Original unlock did not spawn its reward: " + reward);
                }
                ScreenCapture.CaptureScreenshot(Path.Combine(output, "terminal-solved-" + id + ".png"));
                yield return new WaitForSecondsRealtime(.3f);
                cold = ColdLoadTerminalRuntime(root, slot, canvas);
                while (cold.MoveNext()) yield return cold.Current;
                if (TerminalTicketCount() != before - 1) throw new InvalidDataException("Cold load charged a second ticket: " + id);
                Debug.Log("Map12 original terminal " + id + " passed moved-box reset, saved cold load and two-menu auto-solve with one ticket.");
            }
        }

        private static long TerminalTicketCount() => RuntimeField<OfflineMainlineProgressSession>("progressionSave")
            .Save.Player.Inventory.FirstOrDefault(value => value.ItemId == 325)?.Count ?? 0;

        private static Button TerminalOption(string text)
        {
            var ui = RuntimeField<OfficialWorldUiRenderer>("ui");
            return ui.Find("OptionTalkDialog", "/OptionTalkDialog/OptionSlots/Content")
                .GetComponentsInChildren<Button>().Single(value => value.GetComponentsInChildren<TMPro.TMP_Text>()
                    .Any(label => label.text.Contains(text)));
        }

        private static IEnumerator ClickTerminalMapEntity(int id)
        {
            Camera camera = RuntimeField<Camera>("worldCamera");
            camera.transform.position = RuntimeField<GameObject>("avatar").transform.position + new Vector3(0, 0, -20);
            yield return new WaitForSecondsRealtime(.2f);
            GameObject target = map12Runtime.GetComponentInChildren<OfficialMap10EntityView>().FindEntityObject(id);
            Vector2? pointer = camera.WorldToScreenPoint(target.transform.position);
            map12Runtime.PointerPressProvider = () => { Vector2? next = pointer; pointer = null; return next; };
            yield return new WaitForSecondsRealtime(.4f);
        }

        private static IEnumerator OpenTerminalOptions(int id)
        {
            // These fixtures verify terminal inputs. They supply preceding region
            // completion and adjacent positioning, rather than a new-player route.
            foreach (var maximum in RuntimeField<System.Collections.Generic.Dictionary<int, int>>("regionPointLimits"))
                RuntimeField<System.Collections.Generic.Dictionary<int, int>>("regionPoints")[maximum.Key] = maximum.Value;
            RuntimeCall("RefreshEntityActivity");
            MapGridPoint location = RuntimeField<OfflineMapPuzzleSession>("puzzles").Position(id);
            MapGridPoint stand = new[] { new MapGridPoint(location.X, location.Y - 1),
                new MapGridPoint(location.X - 1, location.Y), new MapGridPoint(location.X, location.Y + 1),
                new MapGridPoint(location.X + 1, location.Y) }.First(RuntimeField<OfficialWorldMapSession>("session").Movement.IsFit);
            RuntimeCall("SetAvatarPosition", stand);
            IEnumerator input = ClickTerminalMapEntity(id);
            while (input.MoveNext()) yield return input.Current;
            IEnumerator advance = AdvanceTerminalOptions();
            while (advance.MoveNext()) yield return advance.Current;
        }

        private static IEnumerator AdvanceTerminalOptions()
        {
            var ui = RuntimeField<OfficialWorldUiRenderer>("ui");
            for (int count = 0; !RuntimeField<bool>("storyOptionsWaiting") && count < 40; count++)
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
            if (!RuntimeField<bool>("storyOptionsWaiting")) throw new InvalidDataException("Original terminal options were not reached.");
        }

        private static IEnumerator ColdLoadTerminalRuntime(string root, string slot, Transform canvas)
        {
            var puzzle = RuntimeField<OfflineMapPuzzleSession>("puzzles");
            var owned = RuntimeField<System.Collections.Generic.HashSet<int>>("puzzleEntityIds");
            MapPuzzleEntityState[] saved = puzzle.Save().Where(value => owned.Contains(value.PlacementId)).ToArray();
            MapGridPoint position = RuntimeField<MapGridPoint>("currentGrid");
            RuntimeCall("CloseMap");
            yield return null;
            GameObject avatar = CharacterCreationNativeModelFactory.CreateWorldModel(slot, root, out string name);
            map12Runtime = new GameObject("Map12TerminalColdLoad").AddComponent<OfficialMainlineMapRuntime>();
            map12Runtime.Begin(12, slot, root, canvas, avatar, name, null);
            yield return new WaitForSecondsRealtime(.5f);
            puzzle = RuntimeField<OfflineMapPuzzleSession>("puzzles");
            MapPuzzleEntityState mismatch = saved.FirstOrDefault(value =>
                puzzle.State(value.PlacementId) != value.State || puzzle.IsSpawned(value.PlacementId) != value.Spawned
                || puzzle.IsUnlocked(value.PlacementId) != value.IsUnlocked
                || !puzzle.Position(value.PlacementId).Equals(new MapGridPoint(value.GridX, value.GridY)));
            if (!RuntimeField<MapGridPoint>("currentGrid").Equals(position) || mismatch != null)
                throw new InvalidDataException("Terminal cold load mismatch; player=" + position + " -> "
                    + RuntimeField<MapGridPoint>("currentGrid") + "; entity=" + JsonUtility.ToJson(mismatch)
                    + "; loaded=" + (mismatch == null ? "none" : JsonUtility.ToJson(puzzle.Save().Single(value => value.PlacementId == mismatch.PlacementId))));
        }
    }
}
