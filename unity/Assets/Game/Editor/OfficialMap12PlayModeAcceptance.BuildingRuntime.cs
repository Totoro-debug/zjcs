using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.World;
using Zjcs.Presentation.World;
using Zjcs.Presentation.Combat;
using Zjcs.Combat.Official;
using Zjcs.Official.WorldUI;

namespace Zjcs.Editor
{
    public static partial class OfficialMap12PlayModeAcceptance
    {
        public static void RunBuildingPrelude() => RunCore(false, false, 12, true, verifyBuildingPrelude: true);
        public static void RunBuildingBattle() => RunCore(false, false, 12, true, verifyBuildingBattle: true);

        private static IEnumerator VerifyPrivateDungeonEntrySteps(string root, string slot, Transform canvas, string output)
        {
            RuntimeCall("SetStoryProgress", "IronMountBeforeBattle", 1, 2);
            RuntimeCall("SetStoryProgress", "IronMountActive", 1, 1);
            foreach (var maximum in RuntimeField<Dictionary<int, int>>("regionPointLimits"))
                RuntimeField<Dictionary<int, int>>("regionPoints")[maximum.Key] = maximum.Value;
            RuntimeCall("RefreshEntityActivity");
            var puzzle = RuntimeField<OfflineMapPuzzleSession>("puzzles");
            MapGridPoint at = puzzle.Position(503);
            var range = puzzle.Rules.Single(value => value.PlacementId == 503).Stage.InteractiveRange.Single();
            MapGridPoint stand = new MapGridPoint(at.X + range.X, at.Y + range.Y);
            if (!RuntimeField<OfficialWorldMapSession>("session").Movement.IsFit(stand))
                throw new InvalidDataException("IronMount official interaction point is not walkable.");
            RuntimeCall("SetAvatarPosition", stand);
            IEnumerator pointer = ClickTerminalMapEntity(503);
            while (pointer.MoveNext()) yield return pointer.Current;
            float deadline = Time.realtimeSinceStartup + 12f;
            GameObject dialog = null;
            while (dialog == null && Time.realtimeSinceStartup < deadline)
            {
                dialog = RuntimeField<GameObject>("map12PrivateDungeonDialog");
                yield return null;
            }
            if (dialog == null || RuntimeField<OfficialBattleRuntimeController>("battleController") != null)
            {
                var runtime = map12Runtime;
                var entityMap = (System.Collections.IDictionary)typeof(OfficialMainlineMapRuntime)
                    .GetField("mapEntities", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    .GetValue(runtime);
                object entity = entityMap[503];
                string entityState = entity.GetType().GetField("State").GetValue(entity).ToString();
                var stories = RuntimeField<System.Collections.IDictionary>("stories");
                string activeStory = stories.Contains("IronMountActive")
                    ? ((Map10RuntimeStory)stories["IronMountActive"]).key : "missing";
                var progression = RuntimeField<OfflineMainlineProgressSession>("progressionSave");
                string activeProgress = progression.Save.Player.Stories
                    .Where(value => value.Key == "IronMountActive")
                    .Select(value => $"{value.Count}/{value.Status}").FirstOrDefault() ?? "missing";
                throw new InvalidDataException($"IronMount state 1 did not open its original private dungeon window. "
                    + $"state={entityState}, activeStory={activeStory}, activeProgress={activeProgress}, "
                    + $"avatar={RuntimeField<MapGridPoint>("currentGrid").X}/{RuntimeField<MapGridPoint>("currentGrid").Y}, stand={stand.X}/{stand.Y}, "
                    + $"point={at.X}/{at.Y}, puzzleActive={puzzle.InteractionEnabled(503)}, region={entity.GetType().GetField("Region").GetValue(entity)}.");
            }
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "iron-mount-private-dungeon.png"));
            var buttons = dialog.GetComponentsInChildren<Button>(true);
            Button battle = buttons.SingleOrDefault(value => value.name == "BattleBtn");
            if (battle == null || !battle.gameObject.activeInHierarchy)
                throw new InvalidDataException("Original private dungeon BattleBtn is absent or hidden.");
            var slots = new Zjcs.Offline.State.ProfileSlotCatalog(root);
            string dailySave = Path.Combine(slots.GetSlotDirectory(slot), "daily-dungeon-save.json");
            if (File.Exists(dailySave)) throw new InvalidDataException("New isolated profile unexpectedly has daily dungeon state.");
            byte[] profileBefore = File.ReadAllBytes(slots.GetProgressPath(slot));
            yield return new WaitForEndOfFrame();
            SimulateUiClick(battle, battle.GetComponentInParent<Canvas>().worldCamera);
            deadline = Time.realtimeSinceStartup + 12f;
            GameObject route = null;
            while (route == null && Time.realtimeSinceStartup < deadline)
            {
                route = UnityEngine.Object.FindObjectsOfType<Transform>(true)
                    .FirstOrDefault(value => value.name == "DailyDungeonBigMapDialog")?.gameObject;
                yield return null;
            }
            if (route == null || RuntimeField<OfficialBattleRuntimeController>("battleController") != null)
                throw new InvalidDataException("Original private dungeon BattleBtn did not enter the official route map.");
            var privateSession = Zjcs.Offline.World.OfflineDailyDungeonSession.OpenPrivateMap(slots, slot);
            var run = privateSession.ActiveRun;
            if (run == null || run.dungeonId != 121 || run.difficulty != 50 || run.currentRoomId != 12101)
                throw new InvalidDataException("Private entry did not create the official 121/50 start-room route.");
            if (privateSession.Save.usedEntries != 0 || privateSession.Save.purchasedEntries != 0)
                throw new InvalidDataException("Private entry consumed public dungeon counts or purchases.");
            VerifyPrivateDungeonSettlementBoundary(slots);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "iron-mount-private-route.png"));
            Button bonfire = route.GetComponentsInChildren<Button>(true)
                .SingleOrDefault(value => value.name == "Room_12101" && value.gameObject.activeInHierarchy);
            if (bonfire == null) throw new InvalidDataException("Official 12101 bonfire room button is missing.");
            SimulateUiClick(bonfire, bonfire.GetComponentInParent<Canvas>().worldCamera);
            yield return null;
            yield return new WaitForEndOfFrame();
            route = UnityEngine.Object.FindObjectsOfType<Transform>(true)
                .FirstOrDefault(value => value.name == "DailyDungeonBigMapDialog")?.gameObject;
            Button nextRoom = route == null ? null : route.GetComponentsInChildren<Button>(true)
                .SingleOrDefault(value => value.name == "Room_12102" && value.gameObject.activeInHierarchy);
            if (nextRoom == null) throw new InvalidDataException("Official 12102 battle room button is missing after the bonfire.");
            SimulateUiClick(nextRoom, nextRoom.GetComponentInParent<Canvas>().worldCamera);
            deadline = Time.realtimeSinceStartup + 20f;
            OfficialBattleRuntimeController controller = null;
            while (controller == null && Time.realtimeSinceStartup < deadline)
            {
                controller = RuntimeField<OfficialBattleRuntimeController>("battleController");
                if (controller == null)
                {
                    var runner = GameObject.Find("Map12PrivateDungeon121Battle");
                    controller = runner == null ? null : runner.GetComponent<OfficialBattleRuntimeController>();
                }
                yield return null;
            }
            if (controller == null || controller.Checkpoint == null || controller.Checkpoint.StageId != 30152
                || controller.Checkpoint.StageType != 11 || controller.Checkpoint.Units.All(value => value.Side != 2))
                throw new InvalidDataException("Original private dungeon route did not enter the official 12102 / 30152 battle.");
            privateSession = Zjcs.Offline.World.OfflineDailyDungeonSession.OpenPrivateMap(slots, slot);
            run = privateSession.ActiveRun;
            if (run == null || run.currentRoomId != 12102 || !run.clearedRoomIds.Contains(12101)
                || privateSession.Save.usedEntries != 0 || privateSession.Save.purchasedEntries != 0
                || !File.ReadAllBytes(slots.GetProgressPath(slot)).SequenceEqual(profileBefore)
                || RuntimeField<OfflineMainlineProgressSession>("progressionSave").Save.World.CompletedEntityTags.Contains("map12:building:503"))
                throw new InvalidDataException("Private battle entry changed settlement or character profile state.");
            FightBattleOutcome actualOutcome = null;
            controller.StepCompleted += result =>
            {
                if (result?.Checkpoint?.Finished == true) actualOutcome = result.Checkpoint.Outcome;
            };
            yield return new WaitForSecondsRealtime(.5f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "iron-mount-private-battle.png"));
            deadline = Time.realtimeSinceStartup + 300f;
            while (RuntimeField<OfficialBattleRuntimeController>("battleController") != null
                && Time.realtimeSinceStartup < deadline) yield return null;
            if (RuntimeField<OfficialBattleRuntimeController>("battleController") != null || actualOutcome == null
                || actualOutcome.Invalid)
                throw new InvalidDataException("Official private battle did not deliver a valid local victory/loss result.");
            privateSession = Zjcs.Offline.World.OfflineDailyDungeonSession.OpenPrivateMap(slots, slot);
            run = privateSession.ActiveRun;
            int expectedOutcome = actualOutcome.Won ? 1 : 2;
            if (run.lastBattleRoomId != 12102 || run.lastBattleOutcome != expectedOutcome
                || run.clearedRoomIds.Contains(12102) != actualOutcome.Won
                || privateSession.Save.usedEntries != 0 || privateSession.Save.purchasedEntries != 0
                || !File.ReadAllBytes(slots.GetProgressPath(slot)).SequenceEqual(profileBefore))
                throw new InvalidDataException("Actual private battle result did not persist only the local outcome and room state.");
            if (!actualOutcome.Won)
            {
                route = UnityEngine.Object.FindObjectsOfType<Transform>(true)
                    .FirstOrDefault(value => value.name == "DailyDungeonBigMapDialog")?.gameObject;
                Button retry = route == null ? null : route.GetComponentsInChildren<Button>(true)
                    .SingleOrDefault(value => value.name == "Room_12102" && value.gameObject.activeInHierarchy);
                if (retry == null || !retry.gameObject.activeInHierarchy || !retry.IsInteractable())
                    throw new InvalidDataException("Actual private battle loss did not expose the current room for retry.");
            }
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "iron-mount-private-battle-result.png"));
            Debug.Log("Map12 IronMount private entry passed original simulated map and route input; official 121/50 battle 30152 completed with a valid "
                + (actualOutcome.Won ? "victory" : "loss") + ". Result survived cold read without changing character profile, rewards, public attempts or purchases.");
        }

        private static void VerifyPrivateDungeonSettlementBoundary(Zjcs.Offline.State.ProfileSlotCatalog slots)
        {
            string settlementSlot = slots.CreateSlot();
            string profilePath = slots.GetProgressPath(settlementSlot);
            File.WriteAllText(profilePath, JsonUtility.ToJson(new OfflineMainlineSave(), true));
            byte[] profileBefore = File.ReadAllBytes(profilePath);
            string previousDay = System.DateTime.Now.AddDays(-1).ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
            string dungeonPath = Path.Combine(slots.GetSlotDirectory(settlementSlot), "daily-dungeon-save.json");
            File.WriteAllText(dungeonPath, JsonUtility.ToJson(new Zjcs.Offline.World.DailyDungeonSave {
                localDate = previousDay, usedEntries = 2, purchasedEntries = 1 }, true));
            byte[] dungeonBefore = File.ReadAllBytes(dungeonPath);
            var session = Zjcs.Offline.World.OfflineDailyDungeonSession.OpenPrivateMap(slots, settlementSlot);
            if (!File.ReadAllBytes(dungeonPath).SequenceEqual(dungeonBefore) || session.Save.localDate != previousDay
                || session.Save.usedEntries != 2 || session.Save.purchasedEntries != 1)
                throw new InvalidDataException("Opening a private route ran the public dungeon daily reset.");
            if (!session.TryBegin(121, 50, null, null, null, 0,
                    System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(), out string error)
                || session.ActiveRun?.currentRoomId != 12101)
                throw new InvalidDataException("Private settlement fixture could not begin the configured 121/50 route: " + error);
            if (session.RecordPrivateBattleResult(12101, true))
                throw new InvalidDataException("Official 12101 bonfire accepted a battle settlement without enemy waves.");
            if (!session.RecordRoomCleared(12101, string.Empty) || !session.ChooseNextRoom(12102))
                throw new InvalidDataException("Private settlement fixture could not advance through official bonfire 12101.");
            if (!session.RecordPrivateBattleResult(12102, false))
                throw new InvalidDataException("Private battle failure result could not be stored in the local route save.");
            session = Zjcs.Offline.World.OfflineDailyDungeonSession.OpenPrivateMap(slots, settlementSlot);
            var run = session.ActiveRun;
            if (run.lastBattleRoomId != 12102 || run.lastBattleOutcome != 2 || run.clearedRoomIds.Contains(12102)
                || session.Save.usedEntries != 2 || session.Save.purchasedEntries != 1
                || session.Save.localDate != previousDay || !File.ReadAllBytes(profilePath).SequenceEqual(profileBefore))
                throw new InvalidDataException("Private battle failure changed character, public counts or room completion after cold read.");
            if (!session.RecordPrivateBattleResult(12102, true))
                throw new InvalidDataException("Private battle victory result could not be stored after retry.");
            session = Zjcs.Offline.World.OfflineDailyDungeonSession.OpenPrivateMap(slots, settlementSlot);
            run = session.ActiveRun;
            if (run.lastBattleRoomId != 12102 || run.lastBattleOutcome != 1 || !run.clearedRoomIds.Contains(12102)
                || session.Save.usedEntries != 2 || session.Save.purchasedEntries != 1
                || session.Save.localDate != previousDay || !File.ReadAllBytes(profilePath).SequenceEqual(profileBefore))
                throw new InvalidDataException("Private battle victory changed rewards/counts/profile or failed cold read.");
            if (!session.ChooseNextRoom(12104) || !session.RecordPrivateBattleResult(12104, true)
                || !session.ChooseNextRoom(12106) || !session.RecordRoomCleared(12106, string.Empty)
                || !session.ChooseNextRoom(12107) || !session.RecordPrivateBattleResult(12107, true)
                || !session.ChooseNextRoom(12108) || !session.CompletePrivateRoute(12108))
                throw new InvalidDataException("Private Map12 official 12102 branch did not complete through terminal room 12108.");
            session = Zjcs.Offline.World.OfflineDailyDungeonSession.OpenPrivateMap(slots, settlementSlot);
            run = session.ActiveRun;
            if (!run.privateRouteComplete || run.currentRoomId != 12108 || session.Save.usedEntries != 2
                || session.Save.purchasedEntries != 1 || session.Save.localDate != previousDay || !File.ReadAllBytes(profilePath).SequenceEqual(profileBefore))
                throw new InvalidDataException("Private route completion changed character, rewards or public counts after cold read.");

            string alternateSlot = slots.CreateSlot();
            string alternatePath = slots.GetProgressPath(alternateSlot);
            File.WriteAllText(alternatePath, JsonUtility.ToJson(new OfflineMainlineSave(), true));
            var alternate = Zjcs.Offline.World.OfflineDailyDungeonSession.OpenPrivateMap(slots, alternateSlot);
            byte[] alternateProfile = File.ReadAllBytes(alternatePath);
            if (!alternate.TryBegin(121, 50, null, null, null, 0,
                    System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(), out error)
                || !alternate.RecordRoomCleared(12101, string.Empty) || !alternate.ChooseNextRoom(12103)
                || !alternate.RecordPrivateBattleResult(12103, true) || !alternate.ChooseNextRoom(12105)
                || !alternate.RecordPrivateBattleResult(12105, true) || !alternate.ChooseNextRoom(12106)
                || !alternate.RecordRoomCleared(12106, string.Empty) || !alternate.ChooseNextRoom(12107)
                || !alternate.RecordPrivateBattleResult(12107, true) || !alternate.ChooseNextRoom(12108)
                || !alternate.CompletePrivateRoute(12108))
                throw new InvalidDataException("Private Map12 official 12103 branch did not complete through terminal room 12108.");
            alternate = Zjcs.Offline.World.OfflineDailyDungeonSession.OpenPrivateMap(slots, alternateSlot);
            if (alternate.ActiveRun?.privateRouteComplete != true || alternate.Save.usedEntries != 0
                || alternate.Save.purchasedEntries != 0 || !File.ReadAllBytes(alternatePath).SequenceEqual(alternateProfile))
                throw new InvalidDataException("Alternate private route completion changed character, rewards or public counts.");
        }

        private static IEnumerator VerifyBuildingBattleSteps(string root, string slot, Transform canvas, string output)
        {
            // Qualify only preceding story/region progress. Entry and battle
            // readiness use simulated pointers on the original map and buttons.
            RuntimeCall("SetStoryProgress", "MountLiftBeforeBattle", 1, 2);
            foreach (var maximum in RuntimeField<Dictionary<int, int>>("regionPointLimits"))
                RuntimeField<Dictionary<int, int>>("regionPoints")[maximum.Key] = maximum.Value;
            RuntimeCall("RefreshEntityActivity");
            var puzzle = RuntimeField<OfflineMapPuzzleSession>("puzzles");
            var rule = puzzle.Rules.Single(v => v.PlacementId == 3501);
            MapGridPoint at = puzzle.Position(3501);
            var range = rule.Stage.InteractiveRange.Single();
            RuntimeCall("SetAvatarPosition", new MapGridPoint(at.X + range.X, at.Y + range.Y));
            var progress = RuntimeField<OfflineMainlineProgressSession>("progressionSave");
            var tasks = OfficialMainlineTaskCatalog.LoadDefault();
            if (!tasks.TryGetTask(1215, out OfficialMainlineTaskDefinition liftTask)
                || progress.GetTaskProgress(liftTask.finishCondition, out long liftProgress, out long liftTarget).Status
                    != ProgressionResultStatus.Ready
                || liftProgress != 0 || liftTarget != 1)
                throw new InvalidDataException("Official Map12 task 1215 must be incomplete before the MountLift win.");
            long coins = progress.Save.Player.Inventory.FirstOrDefault(v => v.ItemId == 1)?.Count ?? 0;
            long item = progress.Save.Player.Inventory.FirstOrDefault(v => v.ItemId == 110)?.Count ?? 0;
            IEnumerator click = ClickTerminalMapEntity(3501);
            while (click.MoveNext()) yield return click.Current;
            yield return new WaitForSecondsRealtime(.8f);
            var dialog = RuntimeField<GameObject>("map12BuildingDialog");
            if (dialog == null || RuntimeField<OfficialBattleRuntimeController>("battleController") != null)
                throw new InvalidDataException("Ready MountLift did not open its original native preview.");
            var unlocalized = dialog.GetComponentsInChildren<WorldMultiLanguageText>().Where(v =>
                !string.IsNullOrEmpty(v.myKey) && v.Text == v.myKey).Select(v => v.myKey).ToArray();
            if (unlocalized.Length > 0)
                throw new InvalidDataException("Original building window text keys were not localized: " + string.Join(",", unlocalized));
            if (Resources.Load<OfficialBattleMonsterPortraitCatalog>("Map12/monster-portraits")?.Count != 44)
                throw new InvalidDataException("Map12 original monster portraits are incomplete.");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "mount-lift-native-preview.png"));
            var battle = dialog.GetComponent<WorldCommonStageUI>().myBattleBtn;
            SimulateUiClick(battle, battle.GetComponentInParent<Canvas>().worldCamera);
            yield return null;
            yield return null;
            var controller = RuntimeField<OfficialBattleRuntimeController>("battleController");
            if (controller == null || controller.Checkpoint == null || controller.Checkpoint.StageId != 102)
                throw new InvalidDataException("Original MountLift battle button did not enter stage 102.");
            var enemies = controller.Checkpoint.Units.Where(v => v.Side == 2).ToArray();
            var roster = RuntimeField<Dictionary<int, Map10RuntimeRoster>>("rosters")[102];
            var originalUnits = roster.Waves.SelectMany(v => v.Units).ToArray();
            if (enemies.Length != originalUnits.Length || enemies.Any(v => v.Level != 50 || v.SubRank != 6))
                throw new InvalidDataException("MountLift monsters did not inherit the confirmed building level 50/rank 6.");
            var assets = OfficialCombatAssetCatalog.LoadDefault();
            var catalog = assets.Load();
            foreach (var enemy in enemies)
            {
                var original = originalUnits.First(v => v.MonsterId == enemy.MonsterId);
                var props = new Dictionary<int, long>(catalog.CalculateMonsterProps(original.MonsterId,
                    original.MonsterGroupNo, 50, 6));
                RuntimeCall("ApplyMonsterPassiveSkills", catalog, assets.LoadSkillItems(), props, original, 50, 6);
                if (props.Any(v => enemy.Properties.All(p => p.Id != v.Key || p.Value != v.Value)))
                    throw new InvalidDataException("MountLift original monster property pipeline changed: " + enemy.MonsterId);
            }
            // The source stage uses a preparation phase. Await the native HUD,
            // including TowerActive's original six-second camera story.
            float readyDeadline = Time.realtimeSinceStartup + 15f;
            Button ready = null;
            while (ready == null && Time.realtimeSinceStartup < readyDeadline)
            {
                ready = UnityEngine.Object.FindObjectsOfType<Button>().FirstOrDefault(v =>
                    v.name == "Btn" && v.transform.parent != null && v.transform.parent.name == "StartBtn"
                    && v.gameObject.activeInHierarchy);
                yield return null;
            }
            if (ready == null) throw new InvalidDataException("Original stage 102 readiness button is missing.");
            float storyDeadline = Time.realtimeSinceStartup + 12f;
            while (RuntimeField<bool>("storyRunning") && Time.realtimeSinceStartup < storyDeadline) yield return null;
            if (RuntimeField<bool>("storyRunning")) throw new InvalidDataException("Original TowerActive preview did not finish.");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "mount-lift-battle-preparing.png"));
            SimulateUiClick(ready, ready.GetComponentInParent<Canvas>().worldCamera);
            // Block atomic replacement of this isolated character profile. A
            // finished fight must retain its checkpoint and grant nothing until
            // saving becomes available; releasing the lock permits normal retry.
            var slots = new Zjcs.Offline.State.ProfileSlotCatalog(root);
            var locked = new FileStream(slots.GetProgressPath(slot), FileMode.Open, FileAccess.Read, FileShare.Read);
            float deadline = Time.realtimeSinceStartup + 300f;
            try
            {
                while (!controller.CanCompleteBattle && Time.realtimeSinceStartup < deadline) yield return null;
                if (!controller.CanCompleteBattle || !controller.Checkpoint.Outcome.Won)
                    throw new InvalidDataException("Stage 102 qualified character did not win its actual battle.");
                yield return new WaitForSecondsRealtime(2.5f);
                if (RuntimeField<OfficialBattleRuntimeController>("battleController") != controller
                    || progress.Save.World.CompletedEntityTags.Contains("map12:building:3501")
                    || (progress.Save.Player.Inventory.FirstOrDefault(v => v.ItemId == 1)?.Count ?? 0) != coins
                    || (progress.Save.Player.Inventory.FirstOrDefault(v => v.ItemId == 110)?.Count ?? 0) != item)
                    throw new InvalidDataException("Failed MountLift save granted rewards or removed its finished checkpoint.");
            }
            finally { locked.Dispose(); }
            deadline = Time.realtimeSinceStartup + 8f;
            while (RuntimeField<OfficialBattleRuntimeController>("battleController") != null && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (RuntimeField<OfficialBattleRuntimeController>("battleController") != null)
                throw new InvalidDataException("Stage 102 did not finish its actual battle within the fixture deadline.");
            progress = RuntimeField<OfflineMainlineProgressSession>("progressionSave");
            if (!progress.Save.World.CompletedEntityTags.Contains("map12:building:3501")
                || !progress.Save.World.CompletedEntityTags.Contains("山壁电梯") || puzzle.State(3501) != 1
                || (progress.Save.Player.Inventory.FirstOrDefault(v => v.ItemId == 1)?.Count ?? 0) != coins + 2490
                || (progress.Save.Player.Inventory.FirstOrDefault(v => v.ItemId == 110)?.Count ?? 0) != item + 1)
                throw new InvalidDataException("Stage 102 victory did not settle its original rewards and latched completion once.");
            ProgressionResult liftTaskProgress = progress.GetTaskProgress(liftTask.finishCondition,
                out liftProgress, out liftTarget);
            if (liftTaskProgress.Status != ProgressionResultStatus.Ready || liftProgress != 1 || liftTarget != 1)
                throw new InvalidDataException("Official Map12 task 1215 did not unlock from the MountLift victory.");
            yield return new WaitForSecondsRealtime(1f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "mount-lift-battle-awards.png"));
            IEnumerator cold = ColdLoadTerminalRuntime(root, slot, canvas);
            while (cold.MoveNext()) yield return cold.Current;
            progress = RuntimeField<OfflineMainlineProgressSession>("progressionSave");
            liftTaskProgress = progress.GetTaskProgress(liftTask.finishCondition, out liftProgress, out liftTarget);
            if (RuntimeField<OfflineMapPuzzleSession>("puzzles").State(3501) != 1
                || !progress.Save.World.CompletedEntityTags.Contains("山壁电梯")
                || liftTaskProgress.Status != ProgressionResultStatus.Ready || liftProgress != 1 || liftTarget != 1
                || (progress.Save.Player.Inventory.FirstOrDefault(v => v.ItemId == 1)?.Count ?? 0) != coins + 2490
                || (progress.Save.Player.Inventory.FirstOrDefault(v => v.ItemId == 110)?.Count ?? 0) != item + 1)
                throw new InvalidDataException("MountLift cold load duplicated rewards or lost completion.");
            Debug.Log("Map12 MountLift stage 102 passed actual map pointer, native preview/readiness button input, real battle with source monster props at 50/6, failed-save rollback/retry, exact-once rewards 2490+110x1, official task 1215 unlock and cold completion restoration. Qualified isolated character 90/12 with original starter skill 10011; directional-skill AI and full route remain separate.");
        }

        private static IEnumerator VerifyBuildingPreludeSteps(string root, string slot, Transform canvas, string output)
        {
            RuntimeCall("LoadMap12BuildingOperations");
            var source = RuntimeField<OfficialMap12BuildingData>("map12Buildings");
            foreach (var row in source.Conditions.Where(v => v.Condition._type == "StoryStatusCondition"))
            {
                for (int status = 0; status <= 2; status++)
                {
                    RuntimeCall("SetStoryStatus", row.Condition.StoryKey, status);
                    bool actual = (bool)RuntimeCall("IsMapTalkConditionMet", row.Condition);
                    if (actual != (status == row.Condition.Status))
                        throw new InvalidDataException("Original story status equality changed: " + row.ClassId + "/" + status);
                }
                RuntimeCall("SetStoryStatus", row.Condition.StoryKey, 1);
            }
            IEnumerator cold = ColdLoadTerminalRuntime(root, slot, canvas);
            while (cold.MoveNext()) yield return cold.Current;
            foreach (var row in source.Conditions.Where(v => v.Condition._type == "StoryStatusCondition"))
                if (!(bool)RuntimeCall("IsMapTalkConditionMet", row.Condition))
                    throw new InvalidDataException("Playing story status was not restored: " + row.ClassId);

            foreach (var maximum in RuntimeField<Dictionary<int, int>>("regionPointLimits"))
                RuntimeField<Dictionary<int, int>>("regionPoints")[maximum.Key] = maximum.Value;
            RuntimeCall("RefreshEntityActivity");
            var puzzle = RuntimeField<OfflineMapPuzzleSession>("puzzles");
            var rule = puzzle.Rules.Single(v => v.PlacementId == 3501);
            MapGridPoint at = puzzle.Position(3501);
            var range = rule.Stage.InteractiveRange.Single();
            var stand = new MapGridPoint(at.X + range.X, at.Y + range.Y);
            if (!RuntimeField<OfficialWorldMapSession>("session").Movement.IsFit(stand))
                throw new InvalidDataException("Original MountLift interaction cell is not standable.");
            RuntimeCall("SetAvatarPosition", stand);
            IEnumerator click = ClickTerminalMapEntity(3501);
            while (click.MoveNext()) yield return click.Current;
            if (!RuntimeField<bool>("storyRunning") || RuntimeField<string>("activeStoryRootKey") != "MountLiftGuideToNPC"
                || RuntimeField<object>("selectedStageEntity") != null)
                throw new InvalidDataException("Original map click did not select the prerequisite guide story.");
            yield return new WaitForSecondsRealtime(.8f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "mount-lift-prerequisite.png"));
            var ui = RuntimeField<OfficialWorldUiRenderer>("ui");
            for (int count = 0; RuntimeField<bool>("storyRunning") && count < 60; count++)
            {
                var next = new[] { "TalkDialog", "OptionTalkDialog" }.Select(panel => ui.Find(panel, "/" + panel + "/NextBtn"))
                    .Where(v => v != null && v.activeInHierarchy).Select(v => v.GetComponent<Button>()).FirstOrDefault();
                if (next != null)
                {
                    yield return new WaitForEndOfFrame();
                    SimulateUiClick(next, next.GetComponentInParent<Canvas>().worldCamera);
                }
                yield return new WaitForSecondsRealtime(.2f);
            }
            var progress = RuntimeField<OfflineMainlineProgressSession>("progressionSave");
            if (RuntimeField<bool>("storyRunning") || progress.StoryCount("MountLiftGuideToNPC") != 1
                || progress.StoryCount("MountLiftBeforeBattle") != 0 || puzzle.State(3501) != 0)
                throw new InvalidDataException("Prerequisite guide changed battle eligibility or building state.");
            cold = ColdLoadTerminalRuntime(root, slot, canvas);
            while (cold.MoveNext()) yield return cold.Current;
            progress = RuntimeField<OfflineMainlineProgressSession>("progressionSave");
            if (progress.StoryCount("MountLiftGuideToNPC") != 1 || progress.StoryStatus("MountLiftGuideToNPC") != 2)
                throw new InvalidDataException("Original prerequisite guide did not survive cold load.");
            Debug.Log("Map12 original MountLift prerequisite passed actual map pointer, source condition branch, simulated dialogue clicks and cold restoration; 3 playing-status conditions passed all 0/1/2 values and cold load.");

            // Supply only preceding region progress and placement. Both source
            // dialogue branches are selected through their original UI buttons.
            for (int option = 0; option < 2; option++)
            {
                RuntimeCall("SetStoryProgress", "IronMountBeforeBattle", 0, 0);
                foreach (var maximum in RuntimeField<Dictionary<int, int>>("regionPointLimits"))
                    RuntimeField<Dictionary<int, int>>("regionPoints")[maximum.Key] = maximum.Value;
                RuntimeCall("RefreshEntityActivity");
                puzzle = RuntimeField<OfflineMapPuzzleSession>("puzzles");
                rule = puzzle.Rules.Single(v => v.PlacementId == 503);
                at = puzzle.Position(503);
                range = rule.Stage.InteractiveRange.Single();
                stand = new MapGridPoint(at.X + range.X, at.Y + range.Y);
                if (!RuntimeField<OfficialWorldMapSession>("session").Movement.IsFit(stand))
                    throw new InvalidDataException("Original IronMount interaction cell is not standable.");
                RuntimeCall("SetAvatarPosition", stand);
                click = ClickTerminalMapEntity(503);
                while (click.MoveNext()) yield return click.Current;
                if (!RuntimeField<bool>("storyRunning") || RuntimeField<string>("activeStoryRootKey") != "IronMountBeforeBattle")
                    throw new InvalidDataException("IronMount map pointer did not select original unmet-condition story.");
                bool selected = false;
                for (int step = 0; RuntimeField<bool>("storyRunning") && step < 120; step++)
                {
                    ui = RuntimeField<OfficialWorldUiRenderer>("ui");
                    if (RuntimeField<bool>("storyOptionsWaiting"))
                    {
                        var buttons = ui.Find("OptionTalkDialog", "/OptionTalkDialog/OptionSlots/Content")
                            .GetComponentsInChildren<Button>().Where(v => v.gameObject.activeInHierarchy).ToArray();
                        if (buttons.Length != 2) throw new InvalidDataException("IronMount original two choices are missing.");
                        yield return new WaitForEndOfFrame();
                        ScreenCapture.CaptureScreenshot(Path.Combine(output, "iron-mount-choices-" + option + ".png"));
                        SimulateUiClick(buttons[option], buttons[option].GetComponentInParent<Canvas>().worldCamera);
                        selected = true;
                    }
                    else
                    {
                        var next = new[] { "TalkDialog", "OptionTalkDialog" }.Select(panel => ui.Find(panel, "/" + panel + "/NextBtn"))
                            .Where(v => v != null && v.activeInHierarchy).Select(v => v.GetComponent<Button>()).FirstOrDefault();
                        if (next != null)
                        {
                            yield return new WaitForEndOfFrame();
                            SimulateUiClick(next, next.GetComponentInParent<Canvas>().worldCamera);
                        }
                    }
                    yield return new WaitForSecondsRealtime(.2f);
                }
                progress = RuntimeField<OfflineMainlineProgressSession>("progressionSave");
                if (!selected || RuntimeField<bool>("storyRunning") || progress.StoryCount("IronMountBeforeBattle") != 1
                    || progress.StoryStatus("IronMountBeforeBattle") != 2 || puzzle.State(503) != 0)
                    throw new InvalidDataException("IronMount prerequisite choice did not preserve source completion and stage state: " + option);
                cold = ColdLoadTerminalRuntime(root, slot, canvas);
                while (cold.MoveNext()) yield return cold.Current;
                progress = RuntimeField<OfflineMainlineProgressSession>("progressionSave");
                if (progress.StoryCount("IronMountBeforeBattle") != 1 || progress.StoryStatus("IronMountBeforeBattle") != 2)
                    throw new InvalidDataException("IronMount prerequisite choice did not survive cold load: " + option);
            }
            Debug.Log("Map12 original IronMount prerequisite passed actual map pointers, both original option buttons, source story completion and cold restoration; private dungeon entry remains separate.");
        }
    }
}
