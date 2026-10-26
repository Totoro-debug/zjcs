using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Offline.Progression;
using Zjcs.Offline.World;
using Zjcs.Presentation.World;

namespace Zjcs.Editor
{
    public static partial class OfficialMap12PlayModeAcceptance
    {
        private static IEnumerator VerifySpatialSteps(string root, string slot, Transform canvas, string output)
        {
            foreach (var item in new[] { new { Id = 3501, Story = "TowerActive", Wait = 6.5f, Offset = 1.5f },
                new { Id = 503, Story = "DungeonActive", Wait = 9.5f, Offset = 0f } })
            {
                foreach (var maximum in RuntimeField<Dictionary<int, int>>("regionPointLimits"))
                    RuntimeField<Dictionary<int, int>>("regionPoints")[maximum.Key] = maximum.Value;
                RuntimeCall("RefreshEntityActivity");
                var puzzle = RuntimeField<OfflineMapPuzzleSession>("puzzles");
                MapGridPoint target = puzzle.Position(item.Id);
                MapGridPoint stand = puzzle.Rules.Single(value => value.PlacementId == item.Id).Stage.InteractiveRange
                    .Select(value => new MapGridPoint(target.X + value.X, target.Y + value.Y))
                    .First(RuntimeField<OfficialWorldMapSession>("session").Movement.IsFit);
                RuntimeCall("SetAvatarPosition", stand);
                // Graph fixture only: the original tower starts its preview from a
                // ladder-window operation, not directly from its map click. That
                // window and the dungeon operation chain remain unverified.
                RuntimeCall("StartStoryGraph", item.Story, null, null);
                if (!RuntimeField<bool>("storyRunning") || !RuntimeField<bool>("storyCameraActive"))
                    throw new InvalidDataException("Original graph did not start its registered camera story: " + item.Story);
                var session = RuntimeField<OfficialWorldMapSession>("session");
                Vector3 camera = RuntimeField<Vector3>("storyCameraTarget");
                if (Mathf.Abs(camera.x - session.Definition.Navigation.Origin.X - target.X) > .001f
                    || Mathf.Abs(camera.y - session.Definition.Navigation.Origin.Y - target.Y - item.Offset) > .001f)
                    throw new InvalidDataException("Story camera did not use the original prefab register and fractional offset: " + item.Story);
                yield return new WaitForSecondsRealtime(1.5f);
                ScreenCapture.CaptureScreenshot(Path.Combine(output, "story-camera-" + item.Id + ".png"));
                yield return new WaitForSecondsRealtime(item.Wait - 1.5f);
                if (RuntimeField<bool>("storyRunning") || RuntimeField<bool>("storyCameraActive"))
                    throw new InvalidDataException("Original camera story did not finish/restore its camera: " + item.Story);
                var progress = RuntimeField<OfflineMainlineProgressSession>("progressionSave");
                if (progress.StoryStatus(item.Story) != 2 || progress.StoryCount(item.Story) != 1)
                    throw new InvalidDataException("Original camera story completion was not saved once: " + item.Story);
                if (!RuntimeField<MapGridPoint>("currentGrid").Equals(stand))
                    throw new InvalidDataException("Camera preview moved the character: " + item.Story);
                IEnumerator cold = ColdLoadTerminalRuntime(root, slot, canvas);
                while (cold.MoveNext()) yield return cold.Current;
                progress = RuntimeField<OfflineMainlineProgressSession>("progressionSave");
                if (progress.StoryStatus(item.Story) != 2 || progress.StoryCount(item.Story) != 1)
                    throw new InvalidDataException("Original camera story progress did not survive cold load: " + item.Story);
            }
            Debug.Log("Map12 native story spatial graph fixture passed: MountLift fractional offset, IronMount local selector branch, original waits, camera restoration and saved progress. This does not verify building/window input operations.");
        }
    }
}
