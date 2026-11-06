using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Editor.Combat;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Offline.World;
using Zjcs.Offline.World.NativeMap14SharedEntities;
using Zjcs.Presentation.CharacterCreation;
using Zjcs.Presentation.World;

namespace Zjcs.Editor.Map13Mainline
{
    public static partial class OfficialMap13MainlinePlayAcceptance
    {
        public static void RunSharedWorlds()
        {
            RunSharedWorldMap(10);
            SessionState.SetString(Key + ".queue", "11,12,13,14,15");
        }

        private static void RunSharedWorldMap(int map)
        {
            if (map == 11) RunMap11();
            else if (map == 12) RunMap12();
            else if (map == 14) RunMap14();
            else if (map == 15) RunMap15();
            else { Run(); SessionState.SetInt(Key + ".map", map); }
            SessionState.SetBool(Key + ".shared-worlds", true);
        }

        private static IEnumerator WaitForFormalSharedWorld(OfficialMainlineMapRuntime runtime)
        {
            float deadline = Time.realtimeSinceStartup + 120;
            while (Field<MainlineSharedWorld>(runtime, "mainlineSharedWorld")?.IsReady != true)
            {
                Require(!failed && Time.realtimeSinceStartup < deadline,
                    "Formal shared world failed to start: map=" + MapId);
                yield return null;
            }
            if (MapId == 12 || MapId == 13 || MapId == 14)
            {
                string field = "nativeMap" + MapId + "World";
                while (true)
                {
                    var terrain = Field<object>(runtime, field);
                    if (terrain != null && (bool)terrain.GetType().GetProperty("IsReady").GetValue(terrain)) break;
                    Require(!failed && Time.realtimeSinceStartup < deadline,
                        "Formal native terrain failed to become ready: map=" + MapId);
                    yield return null;
                }
            }
        }

        private static void VerifyFormalRegionStatuses(OfficialMainlineMapRuntime runtime)
        {
            var world = Field<MainlineSharedWorld>(runtime, "mainlineSharedWorld");
            var saved = Field<OfflineMainlineProgressSession>(runtime, "progressionSave").Save;
            var type = world.Assemblies.CommonType("MapInfoManager");
            var info = type.GetMethod("GetMapPartInfo").Invoke(type.GetProperty("Instance").GetValue(null), new object[] { MapId });
            bool mixed = (bool)NativeMap14SharedPrefabs.Get(info, "SupportPrivateRegion")
                && (bool)NativeMap14SharedPrefabs.Get(info, "SupportPublicRegion");
            var regions = (IDictionary)NativeMap14SharedPrefabs.Get(world.Data.Placement, "MaxRegionProgressDict");
            foreach (int region in regions.Keys)
            {
                int expected = mixed ? saved.World.ActivatedGoddessRegions.Any(value => value.MapClassId == MapId && value.Region == region) ? 2 : 1
                    : Convert.ToInt32(NativeMap14SharedPrefabs.Get(info, "MapDefaultRegionStatus"));
                Require((int)Call(runtime, "OriginalRegionStatus", region) == expected,
                    "Formal region status differs from original supported/default status: map=" + MapId + " region=" + region);
            }
        }

        private static IEnumerator SharedWorldSteps()
        {
            Application.runInBackground = true; Application.targetFrameRate = 60;
            BattleGameViewInput.Focus(); yield return new WaitForSecondsRealtime(.5f);
            if (EventSystem.current == null) new GameObject("Shared world simulated pointer", typeof(EventSystem));
            var canvas = new GameObject("Shared world formal HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            string saves = Path.Combine(Output, "formal-shared-map" + MapId + "-profile-" + Guid.NewGuid().ToString("N"));
            var slots = new ProfileSlotCatalog(saves); string slot = slots.CreateSlot();
            var save = new OfflineMainlineSave { CreatedAtUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds() };
            save.Player.CharacterCreated = true; save.Player.CharacterName = "公共地图" + MapId + "验证";
            save.Player.InitialWeaponType = 201; save.Player.Wardrobe.Profession = 1;
            var catalog = OfficialCharacterAssetCatalog.LoadDefault().Load();
            save.Player.Level = 90; save.Player.Experience = catalog.CumulativeLevelExperience[90];
            save.Player.SubRank = catalog.LevelLimits.Where(row => row.PlayerBlessLevel == 0 && row.PlayerLevel >= 90).Min(row => row.SubRank);
            save.Player.Rank = catalog.RankOfSubRank(save.Player.SubRank);
            save.Player.UnlockedMapClassIds.AddRange(new[] { 10, 11, 12, 13, 14, 15 });
            save.Player.Stories.Add(new OfflineStoryProgress { Key = "FirstEnterMap", Count = 1, Status = 2 });
            save.Player.Stories.Add(new OfflineStoryProgress { Key = "FirstBattle", Count = 1, Status = 2 });
            save.Tasks.ClaimedMainlineTaskIds.AddRange(OfficialMainlineTaskCatalog.LoadDefault().OrderedTasks.Where(row => row.classId <= 1040).Select(row => row.classId));
            File.WriteAllText(slots.GetProgressPath(slot), JsonUtility.ToJson(save, true));
            var progression = OfflineMainlineProgressSession.OpenCharacter(slots, slot);
            Require(new OfflineCharacterProgressionService(progression, catalog).InitializeWardrobe("shared-map" + MapId + "-wardrobe").IsApplied,
                "Formal shared world wardrobe fixture failed.");

            var runtime = Open(slot, saves, canvas.transform);
            yield return WaitForFormalSharedWorld(runtime);
            VerifyFormalOriginalClient(runtime); VerifyOriginalEntityStates(runtime); VerifyFormalRegionStatuses(runtime);
            var firstWorld = Field<MainlineSharedWorld>(runtime, "mainlineSharedWorld");
            var firstRoot = firstWorld.Root.Root;
            var manager = firstWorld.Assemblies.Shared.GetType("MeManager", true).GetProperty("Instance").GetValue(null);
            var navigation = Field<object>(runtime, "navigation");
            var initial = Field<MapGridPoint>(navigation, "currentGrid");
            yield return new WaitForSecondsRealtime(2);
            Require(Field<GameObject>(runtime, "avatar").GetComponentsInChildren<Renderer>().Any(renderer => renderer.isVisible),
                "Formal shared map player is not rendered: " + MapId);

            var session = Field<OfficialWorldMapSession>(runtime, "session");
            var camera = Field<Camera>(runtime, "worldCamera");
            var target = initial; Vector2 screen = default; bool found = false;
            foreach (var offset in new[] { new MapGridPoint(1, 0), new MapGridPoint(-1, 0), new MapGridPoint(0, 1), new MapGridPoint(0, -1), new MapGridPoint(2, 0), new MapGridPoint(0, 2) })
            {
                var candidate = new MapGridPoint(initial.X + offset.X, initial.Y + offset.Y);
                var path = session.FindPath(new OfficialMapPathRequest { Start = initial, End = candidate, PreciseTarget = true, BodyOffsets = new[] { new MapGridPoint(0, 0) } });
                if (path == null || path.Count == 0) continue;
                var position = (Vector3)Call(navigation, "GridToWorld", candidate);
                screen = camera.WorldToScreenPoint(position);
                var hits = new List<RaycastResult>();
                EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current) { position = screen }, hits);
                if (hits.Count != 0 || Physics2D.OverlapPoint(position) != null) continue;
                target = candidate; found = true; break;
            }
            Require(found, "No unobstructed simulated map-pointer target: " + MapId);
            bool sent = false;
            runtime.PointerPressProvider = () => { if (sent) return null; sent = true; return screen; };
            float deadline = Time.realtimeSinceStartup + 15;
            while (!Field<MapGridPoint>(navigation, "currentGrid").Equals(target) && Time.realtimeSinceStartup < deadline) yield return null;
            runtime.PointerPressProvider = () => null;
            Require(sent && Field<MapGridPoint>(navigation, "currentGrid").Equals(target), "Formal simulated movement failed: " + MapId);
            VerifyOriginalEntityStates(runtime);
            Require(ReferenceEquals(firstWorld, Field<MainlineSharedWorld>(runtime, "mainlineSharedWorld")), "Movement replaced the owned shared world.");
            ScreenCapture.CaptureScreenshot(Path.Combine(Output, "map" + MapId + "-shared-world-moved.png"));
            yield return new WaitForSecondsRealtime(.5f);
            Call(runtime, "CloseMap"); yield return null; yield return null;
            Require(!firstWorld.IsReady && Field<bool>(firstRoot, "Ended") && NativeMap14SharedPrefabs.Get(manager, "Player") == null,
                "Closing the formal shared world retained its original root/player: " + MapId);

            runtime = Open(slot, saves, canvas.transform);
            yield return WaitForFormalSharedWorld(runtime);
            VerifyFormalOriginalClient(runtime); VerifyOriginalEntityStates(runtime); VerifyFormalRegionStatuses(runtime);
            Require(!ReferenceEquals(firstWorld, Field<MainlineSharedWorld>(runtime, "mainlineSharedWorld"))
                && Field<MapGridPoint>(Field<object>(runtime, "navigation"), "currentGrid").Equals(target),
                "Formal cold reopen reused its world or lost the saved grid: " + MapId);
            Call(runtime, "CloseMap"); yield return null; yield return null;
            runtime = Open(slot, saves, canvas.transform);
            var cancelled = Field<MainlineSharedWorld>(runtime, "mainlineSharedWorld");
            Require(cancelled != null && !cancelled.IsLoaded, "Formal cancellation missed the loading world.");
            Call(runtime, "CloseMap"); yield return null; yield return null;
            Require(!cancelled.IsLoaded && !cancelled.IsReady && NativeMap14SharedPrefabs.Get(manager, "Player") == null,
                "Cancelled formal shared world retained its original client/config scope: " + MapId);
            Require(!failed, "Formal shared world emitted runtime errors: " + MapId);
            Debug.Log("MAINLINE_FORMAL_SHARED_WORLD_PASS map=" + MapId + " original_client=True entity_states=True original_status=True simulated_pointer=True save_reopen=True cancelled=True");
        }
    }
}
