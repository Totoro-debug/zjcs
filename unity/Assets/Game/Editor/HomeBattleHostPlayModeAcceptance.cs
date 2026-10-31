using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Offline.State;
using Zjcs.Presentation.Character;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.Home;

namespace Zjcs.Editor
{
    // Invoked by the existing isolated Play Mode runner. No result callbacks or
    // executor internals are used to manufacture a completed battle.
    public static class HomeBattleHostPlayModeAcceptance
    {
        public static void Run()
        {
            ProjectTools.Validate();
            Skills.SkillPlayModeAcceptance.Run();
        }

        public static IEnumerator Exercise(string slot, string root, string output, Action<Selectable> click)
        {
            var profile = OfflineMainlineProgressSession.OpenCharacter(new ProfileSlotCatalog(root), slot);
            var assets = OfficialCharacterAssetCatalog.LoadDefault();
            var catalog = assets.Load();
            Require(profile.ApplyCharacterMutation(player =>
            {
                player.Level = 40;
                player.Experience = catalog.CumulativeLevelExperience[40];
                player.SubRank = catalog.LevelLimits.Where(value => value.PlayerLevel >= 40
                    && value.PlayerBlessLevel == 0).Min(value => value.SubRank);
                player.InitialWeaponType = 200;
                if (!player.Skills.Skills.Any(value => value.ClassId == 10014))
                    player.Skills.Skills.Add(new OfflineOwnedSkill { ClassId = 10014, Rank = 1 });
            }, "battle-host-acceptance-fixture").IsApplied, "Could not prepare the isolated battle profile.");
            var progression = new OfflineCharacterProgressionService(profile, catalog);
            progression.AttachSkillRules(assets.LoadSkillRules(), assets.LoadSkillRecommendations().recommendations);
            Require(progression.EquipSkill(10014, 0).IsApplied, "Could not equip the battle fixture's skill.");

            HomeFeatureRuntime home = HomeFeatureRuntime.Begin(slot, root, null);
            home.TutorialAutoStartEnabled = false;
            try
            {
                // Publish only configured prerequisite regions in this test save.
                var regions = home.DailyDungeons.Catalog.dungeons
                    .Where(value => value != null && value.entryMapClassId > 0 && value.entryRegionId >= 0)
                    .Select(value => (map: value.entryMapClassId, region: value.entryRegionId))
                    .Concat(home.Arena.Catalog.function_limits.Where(value => value != null
                        && value.Type == 58 && value.Condition != null)
                        .Select(value => (map: value.Condition.MapClassId, region: value.Condition.Region)))
                    .Distinct().ToArray();
                foreach (var group in regions.GroupBy(value => value.map))
                {
                    var result = home.Profile.InitializeMap(group.Key, group.Select(value => value.region).ToArray());
                    Require(result.Status == ProgressionResultStatus.Applied
                        || result.Status == ProgressionResultStatus.Unchanged, "Could not initialize fixture map.");
                }
                foreach (var region in regions)
                {
                    var result = home.Profile.SetMapRegionProgress(region.map, region.region, 10000);
                    Require(result.Status == ProgressionResultStatus.Applied
                        || result.Status == ProgressionResultStatus.Unchanged, "Could not publish fixture region.");
                }
                yield return new WaitForSecondsRealtime(1);
                if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_HOSTS_DIRECT") == "1")
                {
                    yield return Capture(output, "home-entry");
                    yield return OpenTravel(home, click);
                    yield return Capture(output, "travel-entry");
                    for (int i = 0; i < 8 && home.Hud.TryHandleBack(); i++) yield return SettleUi();
                    yield return RunDirectHosts(home, output, click);
                    yield break;
                }
                yield return OpenTravel(home, click);
                yield return Capture(output, "travel-entry");
                var entry = ButtonUnder("DailyDungeonEntry", "EntryBtn");
                File.WriteAllLines(Path.Combine(output, "travel-entry-rects.txt"), entry.GetComponentsInParent<RectTransform>()
                    .Select(value => value.name + " rect=" + value.rect + " local=" + value.localPosition
                        + " anchored=" + value.anchoredPosition + " anchors=" + value.anchorMin + "/" + value.anchorMax
                        + " scale=" + value.localScale + " world=" + value.position));
                click(entry);
                yield return SettleUi();
                click(Active<Button>().First(value => value.name.StartsWith("Dungeon_", StringComparison.Ordinal)));
                yield return SettleUi();
                click(Active<Button>().First(value => value.name == "BattleBtn" && value.interactable));
                yield return SettleUi();
                Require(home.DailyDungeons.ActiveRun != null, "Daily entry did not create a route.");
                for (int attempt = 0; attempt < 20 && UnityEngine.Object.FindObjectOfType<DailyDungeonBattleRunner>() == null; attempt++)
                {
                    var run = home.DailyDungeons.ActiveRun;
                    Require(run != null, "Daily route finished without exercising a combat room.");
                    int roomId = run.currentRoomId;
                    if (run.clearedRoomIds.Contains(roomId))
                    {
                        var current = home.DailyDungeons.FindRoom(roomId);
                        roomId = (current.nextRoomIds ?? Array.Empty<int>()).FirstOrDefault();
                        Require(roomId > 0, "Daily route has no next combat room.");
                    }
                    click(Active<Button>().Single(value => value.name == "Room_" + roomId));
                    yield return SettleUi();
                    var solo = Active<Button>().FirstOrDefault(value => value.name == "SoloChallengeButton");
                    if (solo != null) { click(solo); yield return SettleUi(); }
                }
                int testedRoom = home.DailyDungeons.ActiveRun.currentRoomId;
                yield return RunBattle<DailyDungeonBattleRunner>("daily", output, click);
                Require(home.DailyDungeons.ActiveRun != null, "Daily room unexpectedly removed its route.");
                Require(string.IsNullOrEmpty(home.DailyDungeons.ActiveRun.activeBattleCheckpoint)
                    || FightBattleCheckpointSerializer.Load(home.DailyDungeons.ActiveRun.activeBattleCheckpoint).Finished,
                    "Daily host left an unfinished checkpoint after settlement.");
                Debug.Log("[Battle hosts] daily actual room completed: " + testedRoom);

                yield return OpenTravel(home, click);
                int tickets = home.Arena.Tickets, results = home.Arena.Wins + home.Arena.Losses;
                click(ButtonUnder("ArenaAreaEntry", "EntryBtn"));
                yield return SettleUi();
                click(ButtonUnder("OpponentItem", "FightBtn"));
                yield return SettleUi();
                if (Root("ArenaConfirm") != null)
                {
                    // At direct-win power the left native button selects normal combat.
                    click(ButtonUnder("ArenaConfirm", "CommonBtn_11"));
                    yield return SettleUi();
                }
                yield return RunBattle<ArenaBattleRunner>("arena", output, click);
                Require(!home.Arena.HasActiveChallenge && home.Arena.Tickets == tickets - 1
                    && home.Arena.Wins + home.Arena.Losses == results + 1,
                    "Arena actual result/ticket transaction was not committed exactly once.");
                Debug.Log("[Battle hosts] arena actual battle and settlement passed.");

                yield return OpenTravel(home, click);
                int cleared = home.FantasyTower.GetEntry(1).clearedLayer;
                click(ButtonUnder("DemonBattleEntry", "EntryBtn"));
                yield return SettleUi();
                click(Active<Button>().Single(value => value.name == "FantasyTowerEntry_1"));
                yield return SettleUi();
                var start = Active<Button>().First(value => value.name == "CommonBtn_12"
                    && value.transform.parent.name == "StartBtn");
                click(start);
                yield return SettleUi();
                yield return RunBattle<FantasyTowerBattleRunner>("tower", output, click);
                Require(home.FantasyTower.GetEntry(1).clearedLayer == cleared
                    || home.FantasyTower.GetEntry(1).clearedLayer == cleared + 1,
                    "Tower settled more than one actual layer.");
                Debug.Log("[Battle hosts] tower actual battle and result navigation passed.");
                File.WriteAllText(Path.Combine(output, "battle-hosts-result.txt"),
                    "daily, arena and tower: actual HUD input, Begin/Tick, presentation completion and host settlement passed\n");
            }
            finally { home.CloseForNavigation(); }
        }

        private static IEnumerator RunDirectHosts(HomeFeatureRuntime home, string output, Action<Selectable> click)
        {
            if (Environment.GetEnvironmentVariable("ZJCS_BATTLE_HOSTS_ONLY") == "arena")
            {
                yield return RunArenaHost(home, output, click);
                yield break;
            }
            var dungeon = home.DailyDungeons.PublicDungeons((map, region) => home.MapRegionProgress(map, region) >= 10000).First();
            bool Condition(int id) => home.DailyDungeons.IsConditionMet(id, home.Level,
                home.DailyDungeonCombatRating(), home.SubRank,
                (map, region) => home.MapRegionProgress(map, region) >= 10000,
                home.DailyDungeonHighSubRank, home.DailyDungeonRankForSubRank,
                home.Profile.Save.Player.Wardrobe.Profession, home.DailyDungeonProfessionMatches,
                home.Profile.InventoryCount);
            Require(home.DailyDungeons.TryBegin(dungeon.id, 0,
                id => home.DailyDungeons.PublicDungeons((map, region) => home.MapRegionProgress(map, region) >= 10000)
                    .Any(value => value.id == id), (_, condition) => Condition(condition), home.ConsumeDailyDungeonItem,
                home.DailyDungeons.Catalog.settings.DailyDungeonBuyCostItemId,
                DateTimeOffset.Now.ToUnixTimeSeconds(), out string error), "Daily fixture entry failed: " + error);
            for (int i = 0; i < 30; i++)
            {
                var room = home.DailyDungeons.FindRoom(home.DailyDungeons.ActiveRun.currentRoomId);
                var stage = room == null ? null : home.DailyDungeons.FindStage(room.stageId);
                if (stage != null && (stage.waveArrayIds?.Length > 0
                    || stage.waveMonsterIds?.Any(wave => wave != null && wave.Length > 0) == true)) break;
                Require(room != null && home.DailyDungeons.RecordRoomCleared(room.id, string.Empty),
                    "Could not advance the non-combat fixture room.");
                Require(home.DailyDungeons.ChooseNextRoom((room.nextRoomIds ?? Array.Empty<int>()).First()),
                    "Could not enter the fixture's next room.");
            }
            int testedRoom = home.DailyDungeons.ActiveRun.currentRoomId;
            Action verifyHome = CaptureHomeRestoration(home);
            bool callback = false;
            var daily = new GameObject("DailyDungeonBattleRunner");
            daily.transform.SetParent(home.transform, false);
            daily.AddComponent<DailyDungeonBattleRunner>().Begin(home, testedRoom, "daily-dungeon:" + testedRoom,
                _ => callback = true);
            yield return RunBattle<DailyDungeonBattleRunner>("daily", output, click);
            verifyHome();
            Require(callback, "Daily battle did not hand its actual result back to its caller.");

            yield return RunArenaHost(home, output, click);

            var tower = new GameObject("FantasyTowerBattleRunner");
            verifyHome = CaptureHomeRestoration(home);
            tower.transform.SetParent(home.transform, false);
            bool layerCompleted = false;
            var towerRunner = tower.AddComponent<FantasyTowerBattleRunner>();
            towerRunner.LayerCompleted += (_, __, ___) => layerCompleted = true;
            towerRunner.Begin(home, 1);
            yield return RunBattle<FantasyTowerBattleRunner>("tower", output, click);
            verifyHome();
            Require(layerCompleted, "Tower did not report its actual layer result.");
            Debug.Log("[Battle hosts] public runner integration passed; entrance-window input is a separate pending check.");
        }

        private static IEnumerator RunArenaHost(HomeFeatureRuntime home, string output, Action<Selectable> click)
        {
            int tickets = home.Arena.Tickets, results = home.Arena.Wins + home.Arena.Losses;
            Action verifyHome = CaptureHomeRestoration(home);
            Require(home.Arena.TryBeginChallenge(0, out string error), "Arena fixture entry failed: " + error);
            bool callback = false;
            var arena = new GameObject("ArenaBattleRunner");
            arena.transform.SetParent(home.transform, false);
            arena.AddComponent<ArenaBattleRunner>().Begin(home, _ => callback = true);
            yield return RunBattle<ArenaBattleRunner>("arena", output, click);
            verifyHome();
            Require(callback && !home.Arena.HasActiveChallenge && home.Arena.Tickets == tickets - 1
                && home.Arena.Wins + home.Arena.Losses == results + 1,
                "Arena did not commit exactly one actual battle result.");
        }

        private static IEnumerator RunBattle<T>(string name, string output, Action<Selectable> click) where T : MonoBehaviour
        {
            T runner = UnityEngine.Object.FindObjectOfType<T>();
            Require(runner != null, name + " did not create its real battle runner.");
            var controller = runner.GetComponent<OfficialBattleRuntimeController>();
            Require(controller != null && controller.Checkpoint != null && controller.HasBattleEnvironment,
                name + " did not mount the common battle module/environment.");
            FightBattleCheckpoint checkpoint = controller.Checkpoint;
            int initialCounter = checkpoint.Counter;
            controller.StepCompleted += result => {
                checkpoint = result.Checkpoint;
                if (checkpoint.Counter == initialCounter + 1 || checkpoint.Finished)
                    Debug.Log("[Battle hosts] " + name + " step=" + checkpoint.Counter
                        + " finished=" + checkpoint.Finished);
            };
            File.WriteAllText(Path.Combine(output, name + "-initial.json"), FightBattleCheckpointSerializer.Save(checkpoint));
            string leaf = OfficialBattleHudView.StartButtonPath.Split('/').Last();
            float hudDeadline = Time.realtimeSinceStartup + 15;
            Button start = null;
            while (controller != null && controller.Phase == FightBattlePhase.Preparing && start == null)
            {
                Require(Time.realtimeSinceStartup < hudDeadline, name + " preparation HUD did not appear.");
                start = Active<Button>().FirstOrDefault(value => value.name == leaf
                    && value.transform.parent.name == "StartBtn" && value.interactable);
                if (start == null) yield return null;
            }
            yield return Capture(output, name + "-preparation");
            if (name == "arena")
            {
                Transform guard = Root("ArenaGuardCharacter");
                Require(guard != null && guard.GetComponentsInChildren<MeshRenderer>()
                    .Any(value => value.enabled && !value.forceRenderingOff && value.isVisible
                        && value.transform.parent != null
                        && value.transform.parent.name.StartsWith("__skin__body_", StringComparison.Ordinal)
                        && value.GetComponent<MeshFilter>()?.sharedMesh?.vertexCount > 0),
                    "Arena guard clothing has no visible original attachment geometry.");
            }
            if (start != null) click(start);
            else Require(controller != null && (controller.Phase == FightBattlePhase.Play
                || controller.Phase == FightBattlePhase.PlayStart), name + " did not enter its configured play phase.");
            Button speed = null;
            while (controller != null && speed == null)
            {
                Require(Time.realtimeSinceStartup < hudDeadline, name + " speed HUD did not appear.");
                speed = Active<Button>().FirstOrDefault(value => value.name == "Btn"
                    && value.transform.parent.name == "FightMainPanelSpeedUI" && value.interactable);
                if (speed == null) yield return null;
            }
            Require(speed != null, name + " completed before its HUD could be exercised.");
            int previousSpeed = controller.SpeedIndex;
            click(speed);
            if (controller.UnlockedSpeedIndices.Count > 1)
                Require(controller.SpeedIndex != previousSpeed, name + " speed HUD input was not consumed.");
            float deadline = Time.realtimeSinceStartup + 180;
            bool captured = false;
            float previousTimeScale = Time.timeScale;
            try
            {
                Time.timeScale = 5;
                while (runner != null)
                {
                    Require(Time.realtimeSinceStartup < deadline, name + " battle timed out.");
                    if (!captured && checkpoint.Counter > initialCounter + 2)
                    { yield return Capture(output, name + "-playing"); captured = true; }
                    yield return null;
                }
            }
            finally { Time.timeScale = previousTimeScale; }
            File.WriteAllText(Path.Combine(output, name + "-final.json"), FightBattleCheckpointSerializer.Save(checkpoint));
            Require(checkpoint.Finished && checkpoint.Outcome != null && !checkpoint.Outcome.Invalid
                && checkpoint.Counter > initialCounter,
                name + " disappeared without executing and completing a real battle: counter="
                    + checkpoint.Counter + ", finished=" + checkpoint.Finished + ", initial=" + initialCounter);
            yield return SettleUi();
            yield return Capture(output, name + "-result");
            Debug.Log("[Battle hosts] " + name + " completed counter=" + checkpoint.Counter + " won=" + checkpoint.Outcome.Won);
        }

        private static IEnumerator OpenTravel(HomeFeatureRuntime home, Action<Selectable> click)
        {
            for (int i = 0; i < 8 && home.Hud.TryHandleBack(); i++) yield return SettleUi();
            click(home.Hud.CanvasTransform.GetComponentsInChildren<Selectable>().First(value => value.name == "UnlockObj"
                && value.transform.parent.name == "TravelNote"));
            yield return SettleUi();
        }

        private static Action CaptureHomeRestoration(HomeFeatureRuntime home)
        {
            Camera camera = home.Scene.Camera;
            Vector3 position = camera.transform.position;
            Quaternion rotation = camera.transform.rotation;
            float size = camera.orthographicSize, near = camera.nearClipPlane, far = camera.farClipPlane;
            Transform parent = home.HomePlayerTransform.parent;
            var visible = home.Hud.CanvasTransform.Cast<Transform>().Concat(home.Scene.Root.transform.Cast<Transform>())
                .Where(value => value.name != "HomeToast") // Toast expiry is independent of battle restoration.
                .ToDictionary(value => value.gameObject, value => value.gameObject.activeSelf);
            return () => {
                Require(Vector3.Distance(camera.transform.position, position) < .001f
                    && Quaternion.Angle(camera.transform.rotation, rotation) < .001f
                    && Mathf.Approximately(camera.orthographicSize, size)
                    && Mathf.Approximately(camera.nearClipPlane, near) && Mathf.Approximately(camera.farClipPlane, far),
                    "Battle exit did not restore the host camera projection/pose.");
                Require(home.HomePlayerTransform.parent == parent, "Battle exit did not restore the host avatar parent.");
                foreach (var pair in visible)
                    Require(pair.Key == null || pair.Key.activeSelf == pair.Value,
                        "Battle exit changed the host object's visibility: " + pair.Key.name);
            };
        }

        private static T[] Active<T>() where T : Behaviour => UnityEngine.Object.FindObjectsOfType<T>()
            .Where(value => value.isActiveAndEnabled).ToArray();
        private static Transform Root(string name) => UnityEngine.Object.FindObjectsOfType<Transform>()
            .FirstOrDefault(value => value.name == name);
        private static Button ButtonUnder(string root, string name) => Root(root).GetComponentsInChildren<Button>()
            .First(value => value.name == name && value.isActiveAndEnabled && value.interactable);
        private static IEnumerator SettleUi() { yield return new WaitForSecondsRealtime(.3f); }
        private static IEnumerator Capture(string directory, string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, "battle-host-" + name + ".png"));
            yield return null;
        }
        private static void Require(bool condition, string message)
        { if (!condition) throw new InvalidOperationException(message); }
    }
}
