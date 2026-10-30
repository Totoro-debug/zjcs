using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
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
            GameObject mapAvatar = RuntimeField<GameObject>("avatar");
            Animator mapAnimator = mapAvatar.GetComponentInChildren<Animator>(true);
            Transform mapCharacter = mapAnimator == null ? null : mapAnimator.transform;
            if (mapCharacter == null || mapCharacter.parent != mapAvatar.transform)
                throw new InvalidDataException("The Map12 native character model hierarchy is missing before battle.");
            Transform originalMapCharacterParent = mapCharacter.parent;
            Vector3 originalMapCharacterPosition = mapCharacter.localPosition;
            Quaternion originalMapCharacterRotation = mapCharacter.localRotation;
            Vector3 originalMapCharacterScale = mapCharacter.localScale;
            int soundRegistrations = 0, soundUnregistrations = 0;
            int closeSoundRegistrations = 0, closeSoundUnregistrations = 0;
            map12Runtime.MapSoundRegistration = (source, registered) =>
            {
                if (registered)
                {
                    source.volume = 0f;
                    soundRegistrations++;
                    if (source.gameObject.name == "OfficialDungeonUiSound:Main_Cursor_Back") closeSoundRegistrations++;
                }
                else
                {
                    soundUnregistrations++;
                    if (source.gameObject.name == "OfficialDungeonUiSound:Main_Cursor_Back") closeSoundUnregistrations++;
                }
            };
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
            Vector3 placedPosition = RuntimeField<GameObject>("avatar").transform.position;
            if (!RuntimeField<MapGridPoint>("currentGrid").Equals(stand))
                throw new InvalidDataException("IronMount fixture placement did not update navigation state.");
            yield return null;
            if (!RuntimeField<MapGridPoint>("currentGrid").Equals(stand)
                || (RuntimeField<GameObject>("avatar").transform.position - placedPosition).sqrMagnitude > 0.001f)
                throw new InvalidDataException("IronMount fixture position was reset before the pointer click: current="
                    + RuntimeField<MapGridPoint>("currentGrid").X + "/" + RuntimeField<MapGridPoint>("currentGrid").Y
                    + ", stand=" + stand.X + "/" + stand.Y + ".");
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
            var maskDriver = dialog.GetComponent<OfficialDungeonPopupMask>();
            deadline = Time.realtimeSinceStartup + 12f;
            while (maskDriver != null && !maskDriver.IsReady && Time.realtimeSinceStartup < deadline) yield return null;
            if (maskDriver == null || !maskDriver.IsReady) throw new InvalidDataException("Original private blur mask did not become ready.");
            GameObject mask = (GameObject)typeof(OfficialDungeonPopupMask).GetField("mask",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(maskDriver);
            var maskImage = mask == null ? null : mask.GetComponent<RawImage>();
            var maskButton = mask == null ? null : mask.GetComponent<Button>();
            var maskCanvas = mask == null ? null : mask.GetComponent<Canvas>();
            var dialogCanvas = dialog.GetComponent<Canvas>();
            if (maskImage == null || maskImage.texture == null || maskButton == null
                || maskCanvas == null || dialogCanvas == null || maskCanvas.sortingOrder != dialogCanvas.sortingOrder - 1)
                throw new InvalidDataException("Original private blur image/button/sorting binding is incomplete: "
                    + "mask=" + (mask != null) + ", image=" + (maskImage != null)
                    + ", texture=" + (maskImage != null && maskImage.texture != null)
                    + ", button=" + (maskButton != null) + ", maskCanvas="
                    + (maskCanvas == null ? "null" : maskCanvas.sortingOrder.ToString())
                    + ", dialogCanvas=" + (dialogCanvas == null ? "null" : dialogCanvas.sortingOrder.ToString()) + ".");
            var popupSound = dialog.GetComponentsInChildren<WorldPopupWindowSound>(true).Single();
            if (popupSound.ShowSound != null || popupSound.HideSound == null || popupSound.HideSound.name != "Main_Cursor_Back"
                || popupSound.Post == null || !popupSound.enabled)
                throw new InvalidDataException("Original private popup sound/null-open reference was not restored.");
            var tagSounds = dialog.GetComponentsInChildren<WorldUiClickSound>(true);
            var buttonSounds = dialog.GetComponentsInChildren<WorldButtonSound>(true);
            if (tagSounds.Length != 5 || tagSounds.Any(sound => sound.AkEvent == null || sound.AkEvent.name != "Main_Tag" || sound.Post == null)
                || buttonSounds.Length != 14 || buttonSounds.Count(sound => sound.ClickEvent != null && sound.ClickEvent.name == "Main_Cursor_Normal") != 12
                || buttonSounds.Count(sound => sound.ClickEvent != null && sound.ClickEvent.name == "Main_Cursor_Fail") != 2
                || buttonSounds.Any(sound => sound.Post == null))
                throw new InvalidDataException("Original private tag/normal/failure sound bindings were not restored.");
            int closeSoundPosts = 0;
            Action<AudioClip, GameObject> postSound = popupSound.Post;
            popupSound.Post = (clip, emitter) => { closeSoundPosts++; postSound(clip, emitter); };
            float shown = (float)typeof(WorldPopupWindow).GetField("mShowStartTime",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(dialog.GetComponent<WorldPopupWindow>());
            if (Time.time - shown > .5f) throw new InvalidDataException("The initial half-second mask gate was not observed in time.");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "iron-mount-private-mask-preclick.png"));
            Vector2 maskInputPoint = PrivateMaskInputPoint(maskButton);
            SimulateUiClick(maskButton, maskCanvas.worldCamera, maskInputPoint);
            yield return null;
            if (closeSoundPosts != 0) throw new InvalidDataException("Early rejected mask click emitted a close sound.");
            if (RuntimeField<GameObject>("map12PrivateDungeonDialog") != dialog)
                throw new InvalidDataException("Private mask accepted a click before the original half-second gate.");
            yield return new WaitForSeconds(.6f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "iron-mount-private-mask.png"));
            var maskSlots = new Zjcs.Offline.State.ProfileSlotCatalog(root);
            byte[] beforeMaskClose = File.ReadAllBytes(maskSlots.GetProgressPath(slot));
            SimulateUiClick(maskButton, maskCanvas.worldCamera, maskInputPoint);
            var closeSoundSources = UnityEngine.Object.FindObjectsOfType<AudioSource>()
                .Where(source => source.gameObject.name == "OfficialDungeonUiSound:Main_Cursor_Back").ToArray();
            var closeSoundSource = closeSoundSources.SingleOrDefault();
            if (closeSoundPosts != 1 || closeSoundRegistrations != 1 || closeSoundSource == null || closeSoundSource.clip != popupSound.HideSound
                || closeSoundSource.loop || closeSoundSource.spatialBlend != 0f || closeSoundSource.volume != 0f
                || closeSoundSource.transform.IsChildOf(dialog.transform))
                throw new InvalidDataException("Original close event did not post once to an independent muted UI source: "
                    + $"posts={closeSoundPosts}, registrations={soundRegistrations}, sources={closeSoundSources.Length}, "
                    + $"clip={(closeSoundSource == null || closeSoundSource.clip == null ? "null" : closeSoundSource.clip.name)}, "
                    + $"volume={(closeSoundSource == null ? -1 : closeSoundSource.volume)}, "
                    + $"spatial={(closeSoundSource == null ? -1 : closeSoundSource.spatialBlend)}, "
                    + $"parented={(closeSoundSource != null && closeSoundSource.transform.IsChildOf(dialog.transform))}, "
                    + $"dialogOpen={RuntimeField<GameObject>("map12PrivateDungeonDialog") == dialog}.");
            deadline = Time.realtimeSinceStartup + 5f;
            while (RuntimeField<GameObject>("map12PrivateDungeonDialog") != null && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            if (RuntimeField<GameObject>("map12PrivateDungeonDialog") != null || mask != null
                || !File.ReadAllBytes(maskSlots.GetProgressPath(slot)).SequenceEqual(beforeMaskClose)
                || File.Exists(Path.Combine(maskSlots.GetSlotDirectory(slot), "daily-dungeon-save.json")))
                throw new InvalidDataException("Mask close did not release the original window/mask without changing the profile or dungeon state.");
            deadline = Time.realtimeSinceStartup + 5f;
            while (closeSoundSource != null && Time.realtimeSinceStartup < deadline) yield return null;
            if (closeSoundSource != null || closeSoundUnregistrations != 1)
                throw new InvalidDataException("Private close sound did not release and unregister its independent source.");
            pointer = ClickTerminalMapEntity(503);
            while (pointer.MoveNext()) yield return pointer.Current;
            deadline = Time.realtimeSinceStartup + 12f;
            dialog = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                dialog = RuntimeField<GameObject>("map12PrivateDungeonDialog");
                maskDriver = dialog == null ? null : dialog.GetComponent<OfficialDungeonPopupMask>();
                if (maskDriver != null && maskDriver.IsReady) break;
                yield return null;
            }
            if (dialog == null || maskDriver == null || !maskDriver.IsReady)
                throw new InvalidDataException("Private window could not be reopened by its original map input.");
            yield return new WaitForSeconds(dialog.GetComponent<WorldPopupWindow>().mShowClip.length + .05f);
            IEnumerator matchingAnimation = VerifyPrivateMatchingAnimation(dialog, output, maskSlots.GetProgressPath(slot));
            while (matchingAnimation.MoveNext()) yield return matchingAnimation.Current;
            IEnumerator difficultyInput = VerifyPrivateDifficultyInput(dialog, maskSlots.GetProgressPath(slot), output);
            while (difficultyInput.MoveNext()) yield return difficultyInput.Current;
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
            yield return null;
            yield return new WaitForEndOfFrame();
            var battleSound = battle.GetComponent<WorldButtonSound>();
            int battleSoundPosts = 0;
            Action<AudioClip, GameObject> postBattleSound = battleSound == null ? null : battleSound.Post;
            if (postBattleSound == null) throw new InvalidDataException("Original private BattleBtn sound is not bound.");
            battleSound.Post = (clip, emitter) => { battleSoundPosts++; postBattleSound(clip, emitter); };
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
                throw new InvalidDataException("Original private dungeon BattleBtn did not enter the official route map: "
                    + $"soundPosts={battleSoundPosts}, soundEnabled={(battleSound != null && battleSound.enabled)}, "
                    + $"clip={(battleSound == null || battleSound.ClickEvent == null ? "null" : battleSound.ClickEvent.name)}, "
                    + $"dialogOpen={RuntimeField<GameObject>("map12PrivateDungeonDialog") != null}.");
            if (battleSoundPosts != 1)
                throw new InvalidDataException("Original private BattleBtn pointer did not emit exactly one normal sound: "
                    + $"posts={battleSoundPosts}, enabled={battleSound.enabled}, clip={battleSound.ClickEvent.name}, "
                    + $"active={(battleSound.isActiveAndEnabled)}, route={route.activeInHierarchy}.");
            deadline = Time.realtimeSinceStartup + 5f;
            while (RuntimeField<GameObject>("map12PrivateDungeonDialog") != null && Time.realtimeSinceStartup < deadline) yield return null;
            if (RuntimeField<GameObject>("map12PrivateDungeonDialog") != null)
                throw new InvalidDataException("Successful private stage entry did not close its original detail window.");
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
            Animator battlePlayer = UnityEngine.Object.FindObjectsOfType<Animator>(true)
                .SingleOrDefault(value => value.name.StartsWith("PlayerCharacter_", StringComparison.Ordinal));
            Transform battleWrapper = battlePlayer == null ? null : battlePlayer.transform.parent;
            Transform officialBattleModel = battleWrapper == null ? null : battleWrapper.parent;
            if (battlePlayer == null || battlePlayer.transform.localScale != Vector3.one
                || battleWrapper == null || battleWrapper.localScale != Vector3.one * 1.25f
                || officialBattleModel == null || officialBattleModel.name != "PlayerModel_Game"
                || !Mathf.Approximately(Mathf.Abs(officialBattleModel.localScale.x), 0.3f))
                throw new InvalidDataException("The player model did not use the official battle model scale without retaining the map-view scale.");
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
            while ((actualOutcome == null || controller != null)
                && Time.realtimeSinceStartup < deadline) yield return null;
            if (controller != null || actualOutcome == null || actualOutcome.Invalid)
                throw new InvalidDataException("Official private battle did not deliver a valid local victory/loss result.");
            if (mapCharacter.parent != originalMapCharacterParent
                || mapCharacter.localPosition != originalMapCharacterPosition
                || mapCharacter.localRotation != originalMapCharacterRotation
                || mapCharacter.localScale != originalMapCharacterScale)
                throw new InvalidDataException("The private battle did not restore the original Map12 character transform.");
            privateSession = Zjcs.Offline.World.OfflineDailyDungeonSession.OpenPrivateMap(slots, slot);
            run = privateSession.ActiveRun;
            if (RuntimeField<GameObject>("map12PrivateDungeonDialog") != null)
                throw new InvalidDataException("Private battle return reopened a detail window that the original stage entry closed.");
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
            else
            {
                yield return VerifyPrivateVictoryRoute(slots, slot, profileBefore, output);
            }
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "iron-mount-private-battle-result.png"));
            Debug.Log("Map12 IronMount private entry passed original simulated map and route input; official 121/50 battle 30152 completed with a valid "
                + (actualOutcome.Won ? "victory" : "loss") + ". Result survived cold read without changing character profile, rewards, public attempts or purchases.");
        }

        private static IEnumerator VerifyPrivateVictoryRoute(ProfileSlotCatalog slots, string slot,
            byte[] profileBefore, string output)
        {
            foreach (int roomId in new[] { 12104, 12106, 12107, 12108 })
            {
                Debug.Log("Map12 private victory route entering room " + roomId + ".");
                GameObject route = UnityEngine.Object.FindObjectsOfType<Transform>(true)
                    .FirstOrDefault(value => value.name == "DailyDungeonBigMapDialog")?.gameObject;
                Button roomButton = route == null ? null : route.GetComponentsInChildren<Button>(true)
                    .SingleOrDefault(value => value.name == "Room_" + roomId && value.gameObject.activeInHierarchy);
                if (roomButton == null || !roomButton.IsInteractable())
                    throw new InvalidDataException("Official private route did not expose next room " + roomId + ".");
                SimulateUiClick(roomButton, roomButton.GetComponentInParent<Canvas>().worldCamera);

                OfflineDailyDungeonSession session = OfflineDailyDungeonSession.OpenPrivateMap(slots, slot);
                DailyDungeonRunSave run = session.ActiveRun;
                DailyDungeonRuntimeRoom room = session.FindRoom(roomId);
                DailyDungeonRuntimeStage stage = room == null ? null : session.FindStage(room.stageId);
                bool hasBattle = stage != null && (stage.waveArrayIds?.Length > 0
                    || stage.waveMonsterIds?.Any(wave => wave?.Length > 0) == true);
                FightBattleOutcome outcome = null;
                OfficialBattleRuntimeController controller = null;
                if (hasBattle)
                {
                    float deadline = Time.realtimeSinceStartup + 20f;
                    while (controller == null && Time.realtimeSinceStartup < deadline)
                    {
                        controller = UnityEngine.Object.FindObjectsOfType<OfficialBattleRuntimeController>(true)
                            .FirstOrDefault(value => value != null && value.Checkpoint != null);
                        yield return null;
                    }
                    if (controller == null || controller.Checkpoint?.StageId != stage.id)
                        throw new InvalidDataException("Private room " + roomId + " did not enter its official battle.");
                    // Use the highest speed already unlocked by the official local speed table;
                    // this keeps the long route acceptance bounded while exercising the same
                    // controller callback used by the native speed button.
                    int fastest = controller.UnlockedSpeedIndices.Count == 0
                        ? controller.SpeedIndex : controller.UnlockedSpeedIndices.Max();
                    if (fastest != controller.SpeedIndex) controller.SetSpeedIndex(fastest);
                    controller.StepCompleted += result =>
                    {
                        if (result?.Checkpoint?.Finished == true) outcome = result.Checkpoint.Outcome;
                    };
                    yield return new WaitForSecondsRealtime(.5f);
                    yield return new WaitForEndOfFrame();
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, "iron-mount-private-room-" + roomId + "-battle.png"));
                    deadline = Time.realtimeSinceStartup + 300f;
                    while ((outcome == null || controller != null) && Time.realtimeSinceStartup < deadline)
                        yield return null;
                    if (controller != null || outcome == null || outcome.Invalid || !outcome.Won)
                    {
                        FightBattleCheckpoint state = controller == null ? null : controller.Checkpoint;
                        string progress = state == null ? "controller destroyed"
                            : $"phase={controller.Phase}, sequence={state.Sequence}, counter={state.Counter}, "
                                + $"wave={state.WaveIndex}, finished={state.Finished}, units={state.Units.Count}, "
                                + $"aliveEnemies={state.Units.Count(unit => unit.Side != 1 && unit.Hp > 0)}";
                        throw new InvalidDataException("Private room " + roomId
                            + $" did not complete with a valid local victory (controllerAlive={controller != null}, "
                            + $"outcomeNull={outcome == null}, invalid={outcome?.Invalid}, won={outcome?.Won}; {progress}).");
                    }
                }
                else yield return null;

                session = OfflineDailyDungeonSession.OpenPrivateMap(slots, slot);
                run = session.ActiveRun;
                if (run == null || run.currentRoomId != roomId
                    || (hasBattle && !run.clearedRoomIds.Contains(roomId))
                    || session.Save.usedEntries != 0 || session.Save.purchasedEntries != 0
                    || !File.ReadAllBytes(slots.GetProgressPath(slot)).SequenceEqual(profileBefore))
                    throw new InvalidDataException("Private route room " + roomId
                        + " failed to persist its local state without changing public attempts or the character profile.");
                Debug.Log("Map12 private victory route persisted room " + roomId + ".");
            }

            OfflineDailyDungeonSession completed = OfflineDailyDungeonSession.OpenPrivateMap(slots, slot);
            if (completed.ActiveRun == null || !completed.ActiveRun.privateRouteComplete
                || !completed.ActiveRun.clearedRoomIds.Contains(12104)
                || !completed.ActiveRun.clearedRoomIds.Contains(12107))
                throw new InvalidDataException("The official 121/50 victory route did not persist its 12108 endpoint.");
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "iron-mount-private-route-complete.png"));
        }

        private static Vector2 PrivateMaskInputPoint(Button expected)
        {
            var rect = expected.GetComponent<RawImage>().rectTransform;
            Camera camera = expected.GetComponent<Canvas>().worldCamera;
            Canvas.ForceUpdateCanvases();
            string observations = string.Empty;
            foreach (float x in new[] { .01f, .99f, .1f, .9f, .5f })
                foreach (float y in new[] { .01f, .99f, .1f, .9f, .5f })
                {
                    Vector2 point = RectTransformUtility.WorldToScreenPoint(camera,
                        rect.TransformPoint(new Vector3(Mathf.Lerp(rect.rect.xMin, rect.rect.xMax, x),
                            Mathf.Lerp(rect.rect.yMin, rect.rect.yMax, y), 0)));
                    var pointer = new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current) { position = point };
                    var hits = new List<UnityEngine.EventSystems.RaycastResult>();
                    UnityEngine.EventSystems.EventSystem.current.RaycastAll(pointer, hits);
                    GameObject first = hits.FirstOrDefault().gameObject;
                    if (first != null && UnityEngine.EventSystems.ExecuteEvents.GetEventHandler<UnityEngine.EventSystems.IPointerClickHandler>(first) == expected.gameObject)
                        return point;
                    if (observations.Length < 1200)
                        observations += $" [{point.x:0},{point.y:0}:{(first == null ? "none" : first.name)}]";
                }
            throw new InvalidDataException("Original private blur mask has no reachable area outside its window: "
                + $"rect={rect.rect}, world={rect.position}, screen={Screen.width}x{Screen.height}, camera={(camera == null ? "null" : camera.name)}"
                + observations + ".");
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
