using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
using Zjcs.Official.World;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Offline.World;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private OfficialMap12BuildingData map12Buildings;
        private readonly Dictionary<int, Map12BuildingContext> map12BuildingContexts = new Dictionary<int, Map12BuildingContext>();
        private GameObject map12BuildingDialog;
        private bool map12BuildingStagePending;
        private bool map12PrivateDungeonBattleActive;
        private GameObject map12PrivateDungeonDialog;
        private GameObject map12PrivateDungeonRouteDialog;
        private GameObject map12PrivateDungeonRunner;
        private OfflineDailyDungeonSession map12PrivateDungeonSession;
        private const string MountLiftCompletionTag = "map12:building:3501";
        private const string MountLiftMainlineTag = "山壁电梯";

        private void LoadMap12BuildingOperations()
        {
            if (mapClassId != 12 || map12Buildings != null) return;
            var source = Resources.Load<TextAsset>(MapResource("building-operations"));
            map12Buildings = source == null ? null : JsonUtility.FromJson<OfficialMap12BuildingData>(source.text);
            if (map12Buildings?.manifest != "871_156598" || map12Buildings.Buildings?.Length != 2
                || map12Buildings.Conditions?.Length != 5 || map12Buildings.MountLiftAwards?.Length != 2)
                throw new InvalidDataException("Original Map12 building operations are missing.");
        }

        private bool TryMap12BuildingInteraction(Map10RuntimeEntity entity)
        {
            if (mapClassId != 12 || (entity.EntityClassId != 6302 && entity.EntityClassId != 6103)) return false;
            LoadMap12BuildingOperations();
            var source = map12Buildings.Buildings.Single(v => v.PlacementId == entity.PlacementId);
            if (regions.IsPublicRegion(entity.Region)) return false; // Public dungeon/tower feature binding follows separately.
            if (!map12BuildingContexts.TryGetValue(entity.PlacementId, out var context))
                map12BuildingContexts.Add(entity.PlacementId, context = new Map12BuildingContext(this, entity, source));
            if (entity.State < 0 || entity.State >= source.States.Length) return false;
            int effectiveState = entity.State;
            if (entity.EntityClassId == 6103 && entity.State == 0)
            {
                bool active = new[] { 4103, 4104, 4105 }.Any(id => context.Condition(id) == true);
                if (active) effectiveState = 1;
            }
            var operation = context.ResolveOperation(source.States[effectiveState].OperationId);
            if (entity.EntityClassId == 6103)
            {
                // The unactivated original branch only plays its prerequisite
                // story. The private daily-dungeon provider is a separate flow.
                if (effectiveState == 0 && (operation.Type != "ECWorldStageInteractionOperationCondition"
                    || operation.NeedAllMeet || operation.ConditionIds.Any(id => context.Condition(id) == true))) return false;
            }
            operation.DoOper(context);
            return true;
        }

        private sealed class Map12BuildingContext : IWorldStageOperationContext
        {
            private readonly OfficialMainlineMapRuntime host;
            private readonly Map10RuntimeEntity entity;
            private readonly OfficialMap12Building source;
            public Map12BuildingContext(OfficialMainlineMapRuntime host, Map10RuntimeEntity entity, OfficialMap12Building source)
            { this.host = host; this.entity = entity; this.source = source; }
            public bool IsStageActive => !host.closed && host.battleController == null
                && !host.IsEntityLocked(entity) && entity.State >= 0 && entity.State < source.States.Length;
            public void ReportError(string message) => Debug.LogError(message, host);
            public WorldStageInteractionOperation ResolveOperation(long id) => id < 0 ? null : source.Operations.Single(v => v.Id == id);
            public bool? Condition(int id)
            {
                var condition = host.map12Buildings.Conditions.SingleOrDefault(v => v.ClassId == id);
                return condition == null ? (bool?)null : host.IsMapTalkConditionMet(condition.Condition);
            }
            public void StartStage(int subState)
            {
                if (!IsStageActive) return;
                if (entity.EntityClassId != 6302 || entity.State != 0 || subState != 0)
                    throw new InvalidDataException("Unbound original building stage behaviour.");
                if (host.map12BuildingStagePending) return;
                host.map12BuildingStagePending = true;
                host.StartCoroutine(host.BeginMap12BuildingStage(entity));
            }
            public void PlayStory(int scenario, Action completed)
            {
                var talk = host.ResolveMapTalk(entity, scenario);
                if (talk == null) { completed?.Invoke(); return; }
                if (!string.IsNullOrEmpty(talk.TalkId)) host.StartStoryGraph(talk.TalkId, entity, completed);
                else host.PlayMapTalkDialog(talk, entity, completed, false);
            }
            public void OpenWindow(string name, bool report, int panelId, Action<int> click)
            {
                if (report || panelId != 0) throw new InvalidDataException("Original building window reporting changed.");
                if (name == "WorldMap/WorldFantasyLadderDialog") host.OpenMap12MountLiftDialog(entity, source, this, click);
                else if (name == "DailyDungeon/DailyDungeonDialogPrivateMap") host.OpenMap12PrivateDungeonDialog(entity, click);
                else if (name == "WorldMap/StageEndTipUI")
                {
                    var text = host.map12Buildings.Texts.FirstOrDefault(v => v.key == source.EndTipKey)
                        ?? throw new InvalidDataException("Original completed building notice is missing.");
                    host.hud.ShowOfficialNotice(text.value);
                }
                else throw new InvalidDataException("Building feature window is not bound: " + name);
            }
        }

        private void OpenMap12PrivateDungeonDialog(Map10RuntimeEntity entity, Action<int> click)
        {
            if (map12PrivateDungeonDialog != null || map12PrivateDungeonBattleActive) return;
            GameObject prefab = Resources.Load<GameObject>("Dungeons/UI/DailyDungeonDialogPrivateMap");
            if (prefab == null) throw new InvalidDataException("Original private daily dungeon window is missing.");
            map12PrivateDungeonDialog = Instantiate(prefab, hud.ui.OverlayRoot, false);
            try
            {
                OfficialDungeonUiBindingRuntime.Apply(map12PrivateDungeonDialog, "DailyDungeonDialogPrivateMap", PostMap12PopupSound);
                var slots = new ProfileSlotCatalog(persistentDataPath);
                map12PrivateDungeonSession = OfflineDailyDungeonSession.OpenPrivateMap(slots, slotId);
                int displayId = OfficialDungeonUiBindingRuntime.PrivateDisplayDungeonId;
                var display = map12PrivateDungeonSession.FindDungeon(displayId);
                int difficulty = display.difficulties.Where(value => map12PrivateDungeonSession.IsUnlocked(displayId, value.difficulty))
                    .Select(value => value.difficulty).DefaultIfEmpty(display.difficulties.Min(value => value.difficulty)).Max();
                var displayMap = map12PrivateDungeonSession.FindMap(display.difficulties.Single(value => value.difficulty == difficulty).mapGroupId);
                var gradeRows = displayMap.gradeRows.Where(value => value.difficulty == difficulty).ToArray();
                OfficialDungeonUiBindingRuntime.RefreshPrivateDungeonDetails(map12PrivateDungeonDialog, displayId, difficulty,
                    gradeRows.Min(value => value.grade), gradeRows.Max(value => value.grade));
                Button battle = map12PrivateDungeonDialog.transform.Find("BgDialog3/Content/DailyDungeonDetailContent/Content/NormalGo/UnlockGo/BtnContent/BattleBtn")
                    ?.GetComponent<Button>();
                if (battle == null) battle = FindNamedTransform(map12PrivateDungeonDialog.transform, "BattleBtn")?.GetComponent<Button>();
                if (battle == null) throw new InvalidDataException("Original private daily dungeon battle button is missing.");
                battle.onClick.RemoveAllListeners();
                battle.onClick.AddListener(() => StartMap12PrivateDungeonBattle(entity));
                ShowMap12PrivateDungeonDialog();
            }
            catch
            {
                Destroy(map12PrivateDungeonDialog);
                map12PrivateDungeonDialog = null;
                throw;
            }
        }

        private void ShowMap12PrivateDungeonDialog()
        {
            GameObject window = map12PrivateDungeonDialog;
            var popup = window == null ? null : window.GetComponent<OfficialDungeonPopupMask>();
            if (popup == null) throw new InvalidDataException("Original private dungeon mask driver is missing.");
            popup.Show(() =>
            {
                if (map12PrivateDungeonDialog == window) map12PrivateDungeonDialog = null;
                if (window != null) Destroy(window);
            });
        }

        private void CloseMap12PrivateDungeonDialog()
        {
            if (map12PrivateDungeonDialog != null)
                map12PrivateDungeonDialog.GetComponent<OfficialDungeonPopupMask>().Close();
        }

        private static Transform FindNamedTransform(Transform root, string path)
        {
            Transform direct = root.Find(path);
            if (direct != null) return direct;
            string name = path.Contains("/") ? path.Substring(path.LastIndexOf('/') + 1) : path;
            foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                if (candidate.name == name) return candidate;
            return null;
        }

        private void StartMap12PrivateDungeonBattle(Map10RuntimeEntity entity)
        {
            if (map12PrivateDungeonBattleActive || closed || battleController != null) return;
            var slots = new ProfileSlotCatalog(persistentDataPath);
            map12PrivateDungeonSession = OfflineDailyDungeonSession.OpenPrivateMap(slots, slotId);
            DailyDungeonRunSave run = map12PrivateDungeonSession.ActiveRun;
            if (run == null)
            {
                if (!map12PrivateDungeonSession.TryBegin(121, 50, null, null, null, 0,
                        DateTimeOffset.UtcNow.ToUnixTimeSeconds(), out string error))
                    throw new InvalidOperationException("Official private daily dungeon route could not start: " + error);
                run = map12PrivateDungeonSession.ActiveRun;
            }
            if (run.dungeonId != 121 || run.difficulty != 50)
                throw new InvalidOperationException("An incompatible daily dungeon route is already active.");
            OpenMap12PrivateDungeonRoute();
            CloseMap12PrivateDungeonDialog();
        }

        private void OpenMap12PrivateDungeonRoute()
        {
            if (map12PrivateDungeonRouteDialog != null) return;
            GameObject prefab = Resources.Load<GameObject>("Dungeons/UI/DailyDungeonBigMapDialog");
            if (prefab == null) throw new InvalidOperationException("Official daily dungeon route map is missing.");
            map12PrivateDungeonRouteDialog = Instantiate(prefab, hud.ui.OverlayRoot, false);
            map12PrivateDungeonRouteDialog.name = "DailyDungeonBigMapDialog";
            map12PrivateDungeonRouteDialog.transform.SetAsLastSibling();
            PlaceMap12PrivateDungeonRouteOnTop();
            OfficialDungeonUiBindingRuntime.Apply(map12PrivateDungeonRouteDialog, "DailyDungeonBigMapDialog");
            Button close = map12PrivateDungeonRouteDialog.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(value => value.name == "CloseBtn");
            if (close != null) close.onClick.AddListener(CloseMap12PrivateDungeonRoute);
            RenderMap12PrivateDungeonRoute();
            ShowMap12PrivateDungeonRoute();
        }

        private void PlaceMap12PrivateDungeonRouteOnTop()
        {
            Canvas modalCanvas = map12PrivateDungeonRouteDialog == null
                ? null : map12PrivateDungeonRouteDialog.GetComponent<Canvas>();
            Canvas baseCanvas = hud == null || hud.ui == null || hud.ui.OverlayRoot == null
                ? null : hud.ui.OverlayRoot.GetComponentInParent<Canvas>();
            if (modalCanvas == null || baseCanvas == null) return;
            int sortingOrder = baseCanvas.sortingOrder;
            foreach (Canvas existing in baseCanvas.GetComponentsInChildren<Canvas>(true))
                if (existing != modalCanvas && existing.overrideSorting)
                    sortingOrder = Math.Max(sortingOrder, existing.sortingOrder);
            modalCanvas.overrideSorting = true;
            modalCanvas.sortingLayerID = baseCanvas.sortingLayerID;
            modalCanvas.sortingOrder = sortingOrder + 1;
        }

        private void ShowMap12PrivateDungeonRoute()
        {
            WorldPopupWindow popup = map12PrivateDungeonRouteDialog == null
                ? null : map12PrivateDungeonRouteDialog.GetComponent<WorldPopupWindow>();
            if (popup != null) popup.Show();
        }

        private WorldDailyDungeonRoute map12PrivateDungeonRouteModel;

        private void RenderMap12PrivateDungeonRoute()
        {
            if (map12PrivateDungeonRouteDialog == null || map12PrivateDungeonSession?.ActiveRun == null) return;
            DailyDungeonRunSave run = map12PrivateDungeonSession.ActiveRun;
            DailyDungeonRuntimeMap map = map12PrivateDungeonSession.Catalog.maps
                .FirstOrDefault(value => value.id == run.mapGroupId);
            bool choosing = run.clearedRoomIds.Contains(run.currentRoomId) && !run.privateRouteComplete;
            var window = OfficialDungeonUiBindingRuntime.ConfigureRouteWindow(map12PrivateDungeonRouteDialog, choosing);
            GridLayoutGroup grid = window.Grid;
            LoopVerticalScrollRect scroll = window.Scroll;
            if (map == null || grid == null || scroll == null)
                throw new InvalidOperationException("Official private dungeon route bindings are incomplete.");
            if (map12PrivateDungeonRouteModel == null)
            {
                map12PrivateDungeonRouteModel = new WorldDailyDungeonRoute {
                    ColumnCount = map.columnCount, RowCount = map.rowCount, RoomArray = map.roomArray };
                foreach (int roomId in map.roomArray.Where(value => value > 0).Distinct())
                {
                    DailyDungeonRuntimeRoom room = map12PrivateDungeonSession.FindRoom(roomId);
                    if (room == null) throw new InvalidDataException("Official private route room is missing: " + roomId);
                    map12PrivateDungeonRouteModel.Rooms.Add(roomId, new WorldDailyDungeonRoute.Room {
                        Id = roomId, Type = room.type, NextRoomIds = room.nextRoomIds ?? Array.Empty<int>() });
                }
                OfficialDungeonUiBindingRuntime.BindRouteStyles(map12PrivateDungeonRouteModel);
            }
            map12PrivateDungeonRouteModel.CurrentRoomId = run.currentRoomId;
            map12PrivateDungeonRouteModel.FinishedRoomIdList = new HashSet<int>(run.clearedRoomIds);
            grid.constraintCount = map.columnCount;
            scroll.dataSource = new Map12PrivateDungeonRouteDataSource(this, map12PrivateDungeonRouteModel, choosing);
            scroll.totalCount = map.rowCount * map.columnCount;
            var coordinates = map12PrivateDungeonRouteModel.GetCoordinatesByRoomId(run.currentRoomId);
            // DailyDungeonBigMapDialog.GetBigMapShowIndex for normal / choose-area modes.
            int index = map12PrivateDungeonRouteModel.GetIndexByCoordinates(coordinates.x + (choosing ? -1 : -2), coordinates.y);
            scroll.RefillCells(Mathf.Clamp(index, 0, scroll.totalCount));
            OfficialDungeonUiBindingRuntime.RefreshRouteGrades(map12PrivateDungeonRouteDialog, window,
                map.gradeRows.Where(value => value.difficulty == run.difficulty)
                    .Select(value => new KeyValuePair<int, int>(value.grade, value.row)), map.rowCount);
        }

        private sealed class Map12PrivateDungeonRouteDataSource : LoopScrollDataSource
        {
            private readonly OfficialMainlineMapRuntime owner;
            private readonly WorldDailyDungeonRoute route;
            private readonly bool choosing;
            public Map12PrivateDungeonRouteDataSource(OfficialMainlineMapRuntime owner, WorldDailyDungeonRoute route, bool choosing)
            { this.owner = owner; this.route = route; this.choosing = choosing; }
            public void ProvideData(Transform transform, int index)
            {
                int roomId = route.GetRoomIdByIndex(index);
                WorldDailyDungeonRoomItem view = OfficialDungeonUiBindingRuntime.BindRouteRoom(transform.gameObject);
                view.RefreshView(roomId, route, choosing);
                transform.name = "Room_" + roomId;
                DailyDungeonRunSave run = owner.map12PrivateDungeonSession.ActiveRun;
                bool eligible = roomId > 0 && !run.privateRouteComplete && !run.clearedRoomIds.Contains(roomId)
                    && (roomId == run.currentRoomId || (run.clearedRoomIds.Contains(run.currentRoomId)
                        && route.Rooms[run.currentRoomId].NextRoomIds.Contains(roomId)));
                view.ClickBtn.interactable = eligible;
                view.ClickBtn.onClick.RemoveAllListeners();
                view.ClickBtn.onClick.AddListener(() => {
                    if (roomId != route.CurrentRoomId) view.ShowSelectedRoom(roomId, route);
                    owner.SelectMap12PrivateDungeonRoom(roomId);
                });
            }
        }

        private void SelectMap12PrivateDungeonRoom(int roomId)
        {
            DailyDungeonRunSave run = map12PrivateDungeonSession?.ActiveRun;
            if (run == null || run.clearedRoomIds.Contains(roomId)) return;
            DailyDungeonRuntimeRoom room = map12PrivateDungeonSession.FindRoom(roomId);
            if (room == null) return;
            DailyDungeonRuntimeStage stage = room == null
                ? null : map12PrivateDungeonSession.FindStage(room.stageId);
            if (roomId != run.currentRoomId && !map12PrivateDungeonSession.ChooseNextRoom(roomId)) return;
            if (stage == null)
            {
                if (roomId == 12108 && map12PrivateDungeonSession.CompletePrivateRoute(roomId))
                {
                    RenderMap12PrivateDungeonRoute();
                    hud.ShowOfficialNotice("路线完成");
                }
                return;
            }
            if (stage.waveArrayIds?.Length > 0 || stage.waveMonsterIds?.Any(wave => wave?.Length > 0) == true)
            {
                EnterMap12PrivateDungeonBattle(roomId);
                return;
            }
            if (room.type != 1 || !map12PrivateDungeonSession.RecordRoomCleared(roomId, string.Empty))
                throw new InvalidOperationException("Official private dungeon non-battle room could not be entered.");
            RenderMap12PrivateDungeonRoute();
        }

        private void EnterMap12PrivateDungeonBattle(int roomId)
        {
            if (map12PrivateDungeonBattleActive) return;
            Type runnerType = Type.GetType("Zjcs.Presentation.Home.DailyDungeonBattleRunner, Zjcs.Presentation.Home");
            MethodInfo begin = runnerType?.GetMethod("BeginPrivateMapRoom", BindingFlags.Instance | BindingFlags.Public);
            if (runnerType == null || begin == null)
                throw new InvalidOperationException("Official daily dungeon battle runner is unavailable.");
            var runnerObject = new GameObject("Map12PrivateDungeon121Battle");
            map12PrivateDungeonRunner = runnerObject;
            Component runner = runnerObject.AddComponent(runnerType);
            map12PrivateDungeonBattleActive = true;
            try
            {
                begin.Invoke(runner, new object[] { progressionSave, progression, map12PrivateDungeonSession,
                    avatar.transform, hud.ui.OverlayRoot, (Action<bool>)FinishMap12PrivateDungeonBattle });
                SuspendWorldForBattle();
                if (map12PrivateDungeonRouteDialog != null) map12PrivateDungeonRouteDialog.SetActive(false);
            }
            catch (TargetInvocationException error)
            {
                map12PrivateDungeonBattleActive = false;
                RestoreWorldAfterBattle();
                Destroy(runnerObject);
                map12PrivateDungeonRunner = null;
                throw error.InnerException ?? error;
            }
            catch
            {
                map12PrivateDungeonBattleActive = false;
                RestoreWorldAfterBattle();
                Destroy(runnerObject);
                map12PrivateDungeonRunner = null;
                throw;
            }
        }

        private void FinishMap12PrivateDungeonBattle(bool won)
        {
            map12PrivateDungeonBattleActive = false;
            RestoreWorldAfterBattle();
            int roomId = map12PrivateDungeonSession?.ActiveRun?.currentRoomId ?? 0;
            if (roomId <= 0 || !map12PrivateDungeonSession.RecordPrivateBattleResult(roomId, won))
                throw new InvalidOperationException("Private Map12 battle result could not be recorded locally.");
            navigation.SetAvatarPosition(navigation.currentGrid);
            if (!closed && map12PrivateDungeonRouteDialog != null)
            {
                map12PrivateDungeonRouteDialog.SetActive(true);
                RenderMap12PrivateDungeonRoute();
                ShowMap12PrivateDungeonRoute();
            }
            map12PrivateDungeonRunner = null;
            if (!won) hud.ShowOfficialNotice("战斗失败，可在路线图重试");
        }

        private void CloseMap12PrivateDungeonRoute()
        {
            if (map12PrivateDungeonRouteDialog != null)
            {
                Destroy(map12PrivateDungeonRouteDialog);
                map12PrivateDungeonRouteDialog = null;
            }
        }

        private static Transform FindChild(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                Transform found = FindChild(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static void SetActiveNamed(Transform root, string name, bool active)
        {
            Transform node = FindChild(root, name);
            if (node != null) node.gameObject.SetActive(active);
        }

        private IEnumerator BeginMap12BuildingStage(Map10RuntimeEntity entity)
        {
            // The original button sends an asynchronous stage request before
            // its remaining listeners resolve TowerActive's registered object.
            // Complete local transport on the next frame, after those listeners.
            yield return null;
            map12BuildingStagePending = false;
            if (!closed && battleController == null) StartEntityStage(entity, null);
        }

        private (int level, int subRank) Map12BuildingLevel(OfficialMap12Building source)
        {
            int type = regions.IsPublicRegion(mapEntities[source.PlacementId].Region) ? source.PublicLevelType : source.PrivateLevelType;
            if (type == 2) return (progressionSave.Save.Player.Level, progressionSave.Save.Player.SubRank);
            if (type != 1) throw new InvalidDataException("Unbound original Map12 building level type: " + type);
            return (source.Absolute ? source.Level : source.RegionLevel + source.Level,
                source.SubRank == 0 ? source.RegionSubRank : source.SubRank);
        }

        private void OpenMap12MountLiftDialog(Map10RuntimeEntity entity, OfficialMap12Building source,
            Map12BuildingContext context, Action<int> click)
        {
            if (map12BuildingDialog != null) return;
            var root = Instantiate(Resources.Load<GameObject>(NativeUiResource("WorldFantasyLadderDialog")), hud.ui.OverlayRoot, false);
            map12BuildingDialog = root;
            foreach (var text in root.GetComponentsInChildren<WorldMultiLanguageText>(true)) text.Localizer = NativeUi.Text;
            var assets = Resources.Load<OfficialMap11UiAssets>(NativeUiResource("DynamicItems"));
            var level = Map12BuildingLevel(source);
            root.GetComponent<StageDialog>().Init(new WorldStageUiContext
            {
                NameKey = "entity_" + entity.EntityClassId + "_name", DescriptionKey = "entity_" + entity.EntityClassId + "_desc",
                DialogIconPrefab = Resources.Load<GameObject>(NativeUiResource(entity.EntityClassId + "_DialogIcon")),
                Background = assets.mapBackground, Level = level.level,
                IsActive = () => context.IsStageActive, StartStage = index => context.StartStage(index),
                ShowRewardPreview = slot => OfficialMap12BuildingRewardView.Populate(slot, map12Buildings,
                    level.level, level.subRank, assets, MapProfile.NativeUiRoot, key => NativeUi.Text(key)),
                Close = CloseMap12BuildingDialog
            });
            root.GetComponents<WorldStageOperationWindowClickUi>()[0].InitWindowClick(click);
            root.GetComponent<WorldPopupWindow>().Show();
        }

        private void CloseMap12BuildingDialog()
        {
            if (map12BuildingDialog == null) return;
            var root = map12BuildingDialog;
            map12BuildingDialog = null;
            root.GetComponent<WorldPopupWindow>().Hide(() => Destroy(root));
        }

        private void SynchronizeMap12BuildingCompletions()
        {
            if (mapClassId != 12 || puzzles == null || !progressionSave.Save.World.CompletedEntityTags.Contains(MountLiftCompletionTag)) return;
            if (!progressionSave.Save.World.CompletedEntityTags.Contains(MountLiftMainlineTag))
            {
                ProgressionResult migrated = progressionSave.RecordEntityTagCompleted(
                    MountLiftMainlineTag, MountLiftCompletionTag + ":mainline");
                if (migrated.Status == ProgressionResultStatus.InvalidState)
                    throw new InvalidDataException(migrated.Message);
            }
            puzzles.SetState(3501, 1);
            mapEntities[3501].State = 1;
        }

        // Called before any battle counters or checkpoint removal. A failed
        // profile write leaves the finished checkpoint available for retry.
        private bool SettleMap12MountLiftBattle(Map10RuntimeEntity entity, int stageId, bool won)
        {
            if (mapClassId != 12 || entity?.PlacementId != 3501 || stageId != 102 || !won) return true;
            LoadMap12BuildingOperations();
            try
            {
                var source = map12Buildings.Buildings.Single(v => v.PlacementId == entity.PlacementId);
                var level = Map12BuildingLevel(source);
                var awards = map12Buildings.MountLiftRewardPreview(level.level, level.subRank);
                var result = progressionSave.ApplyInventoryTransaction(MountLiftCompletionTag, save =>
                {
                    foreach (var award in awards)
                        if (!OfflineInventoryOperations.Grant(save.Player, award.Key, award.Value, DateTimeOffset.UtcNow.ToUnixTimeSeconds()).IsApplied)
                            throw new InvalidDataException("Original MountLift award could not be applied: " + award.Key);
                    save.World.CompletedEntityTags.Add(MountLiftCompletionTag);
                    if (!save.World.CompletedEntityTags.Contains(MountLiftMainlineTag))
                        save.World.CompletedEntityTags.Add(MountLiftMainlineTag);
                }, entity.EntityClassId, 1);
                if (result.Status == ProgressionResultStatus.InvalidState) throw new InvalidDataException(result.Message);
                SynchronizeMap12BuildingCompletions();
                if (result.IsApplied) stamina.ShowStaminaAward(awards);
                return true;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                battleStepTimer = 2f;
                hud.ShowOfficialNotice("保存失败，请稍后重试");
                return false;
            }
        }
    }
}
