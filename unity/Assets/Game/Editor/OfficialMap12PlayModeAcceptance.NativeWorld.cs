using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Presentation.World;
using Zjcs.Offline.World;
using Zjcs.Offline.Progression;
using Zjcs.Official.WorldUI;

namespace Zjcs.Editor
{
    public static partial class OfficialMap12PlayModeAcceptance
    {
        public static void RunRuntimeNativeWorld()
        {
            UnityEditor.SessionState.SetBool(Key + ".runtime", false);
            UnityEditor.SessionState.SetBool(Key + ".runtimeTerminals", false);
            UnityEditor.SessionState.SetBool(Key + ".runtimeSpatial", false);
            UnityEditor.SessionState.SetBool(Key + ".runtimeDialogue", false);
            UnityEditor.SessionState.SetBool(Key + ".audio", false);
            UnityEditor.SessionState.SetBool(Key + ".chests", false);
            UnityEditor.SessionState.SetBool(Key + ".buildingUi", false);
            UnityEditor.SessionState.SetBool(Key + ".buildingPrelude", false);
            UnityEditor.SessionState.SetBool(Key + ".buildingBattle", false);
            UnityEditor.SessionState.SetBool(Key + ".privateDungeonEntry", false);
            RunCore(false, false, 12, true, verifyNativeWorld: true);
        }

        private static IEnumerator VerifyNativeWorldSteps(string root, string slot, Transform canvas, string output)
        {
            var world = RuntimeField<OfficialWorldMapSession>("session");
            var helper = Type.GetType("Zjcs.Editor.Map12.OfficialMap12NativeWorldAcceptance, Zjcs.Editor.Map12", true);
            var steps = (IEnumerator)helper.GetMethod("VerifyRuntime").Invoke(null, new object[] {
                map12Runtime, new Func<int, int, bool>((x, y) => world.Movement.IsFit(new MapGridPoint(x, y))),
                new Action<int, int>((x, y) => RuntimeCall("SetAvatarPosition", new MapGridPoint(x, y))), output });
            while (steps.MoveNext()) yield return steps.Current;
            IEnumerator goddess = VerifyNativeGoddessSteps(root, slot, canvas, output, helper);
            while (goddess.MoveNext()) yield return goddess.Current;
        }

        private static IEnumerator VerifyNativeGoddessSteps(string root, string slot, Transform canvas, string output, Type helper)
        {
            var entity = map12Runtime.GetComponentInChildren<OfficialMap10EntityView>().EntityInfos.Single(e => e.PlacementId == 102);
            RuntimeCall("SetAvatarPosition", new MapGridPoint(entity.Grid[0], entity.Grid[1]));
            // Supply the explicitly documented full-region precondition. The
            // map pointer and original popup button perform the activation.
            RuntimeField<System.Collections.Generic.Dictionary<int, int>>("regionPoints")[entity.Region]
                = RuntimeField<System.Collections.Generic.Dictionary<int, int>>("regionPointLimits")[entity.Region];
            RuntimeField<OfflineMainlineProgressSession>("progressionSave").SetMapRegionProgress(12, entity.Region, 10000);
            RuntimeCall("RefreshEntityActivity");
            float deadline = Time.realtimeSinceStartup + 4f;
            var avatar = RuntimeField<GameObject>("avatar");
            while (Vector2.Distance(Camera.main.transform.position, avatar.transform.position) > .05f && Time.realtimeSinceStartup < deadline)
                yield return null;
            // An unrelated timed inventory cleanup must not interrupt this
            // visual activation fixture while the test moves between regions.
            OfficialMainlineMapInspection.Set(map12Runtime, "explorationRefreshAt", long.MaxValue);
            IEnumerator pointer = ClickTerminalMapEntity(entity.PlacementId);
            while (pointer.MoveNext()) yield return pointer.Current;
            var dialog = UnityEngine.Object.FindObjectOfType<GoddessTouchDialog>();
            if (!dialog || !dialog.myTouchBtn.gameObject.activeInHierarchy)
                throw new InvalidDataException("Original full-region goddess popup did not expose the activation button.");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "native-goddess-before.png"));
            SimulateUiClick(dialog.myTouchBtn, dialog.GetComponentInParent<Canvas>().worldCamera);
            yield return new WaitForSecondsRealtime(5.25f);
            helper.GetMethod("VerifyOpenedRegion").Invoke(null, new object[] { map12Runtime, entity.Region });
            if (!RuntimeField<OfflineMainlineProgressSession>("progressionSave").Save.World.ActivatedGoddessRegions.Any(r => r.MapClassId == 12 && r.Region == entity.Region))
                throw new InvalidDataException("Actual goddess button did not save its region activation.");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "native-goddess-after.png"));
            IEnumerator cold = ColdLoadTerminalRuntime(root, slot, canvas);
            while (cold.MoveNext()) yield return cold.Current;
            helper.GetMethod("VerifyOpenedRegion").Invoke(null, new object[] { map12Runtime, entity.Region });
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "native-goddess-cold.png"));
            Debug.Log("Map12 original goddess pointer/popup activation, native private-to-public layers and cold profile restoration passed.");
        }

        private static void VerifyNativeWorldGraphics(Transform canvas)
        {
            var view = map12Runtime.GetComponentsInChildren<MonoBehaviour>(true)
                .Single(value => value.GetType().FullName == "Zjcs.Presentation.World.NativeMap12.OfficialMap12NativeWorldView");
            if (QualitySettings.activeColorSpace != ColorSpace.Linear)
                throw new InvalidDataException("Original Map12 world requires Linear color space.");
            if (map12Runtime.GetComponentsInChildren<Transform>(true).Any(value => value.name == "Map12Terrain"))
                throw new InvalidDataException("Converted legacy terrain is still active beside the original Map12 world.");
            var player = RuntimeField<GameObject>("avatar");
            var group = player.GetComponent<UnityEngine.Rendering.SortingGroup>();
            if (group.sortingLayerName != "Object" || group.sortingOrder != 0)
                throw new InvalidDataException("Original world player layer/order was not restored.");
            Debug.Log("Map12 runtime native world mounted: " + view.name);
        }
    }
}
