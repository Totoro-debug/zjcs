using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
using Zjcs.Combat.Official;
using Zjcs.Official.World;
using Zjcs.Official.WorldUI;
using Zjcs.Offline.Progression;
using Zjcs.Offline.State;
using Zjcs.Offline.World;
using Zjcs.Presentation.Combat;

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
        private DailyDungeonModule map12PrivateDungeonSession;
        private OfflineNpcAssistanceProgress map12PrivateDungeonAssistProgress;
        private FightOfficialNpcAssistanceCatalog map12PrivateDungeonAssistCatalog;
        private OfficialBattleModelCatalog map12PrivateDungeonBattleModels;
        private OfficialNpcTravelCatalog map12PrivateDungeonTravelCatalog;
        private object map12PrivateDungeonHomeAssets;
        private MethodInfo map12PrivateDungeonHomeLocalizedText;
        private MethodInfo map12PrivateDungeonHomeFindDynamicSprite;
        private MethodInfo map12PrivateDungeonHomeRelease;
        private OfficialBattleAssistSelectionView map12PrivateDungeonAssistView;
        private int map12PrivateDungeonAssistRoomId;
        private int map12PrivateDungeonAssistMaxPlayer;
        private string map12PrivateDungeonAssistEntryId;
        private const int Map12PrivateDungeonAssistPlayType = 2;
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
            GameObject prefab = Resources.Load<GameObject>("Dungeons/UI/884_156692/DailyDungeonDialogPrivateMap");
            if (prefab == null) throw new InvalidDataException("Original private daily dungeon window is missing.");
            var staging = new GameObject("Map12PrivateDungeonStaging", typeof(RectTransform));
            staging.SetActive(false);
            staging.transform.SetParent(hud.ui.OverlayRoot, false);
            map12PrivateDungeonDialog = Instantiate(prefab, staging.transform, false);
            try
            {
                BindStoryUiWindow(map12PrivateDungeonDialog, "DailyDungeonDialogPrivateMap");
                OfficialDungeonUiBindingRuntime.Apply(map12PrivateDungeonDialog, "DailyDungeonDialogPrivateMap", PostMap12PopupSound);
                var slots = new ProfileSlotCatalog(persistentDataPath);
                map12PrivateDungeonSession = DailyDungeonModule.OpenPrivateMap(slots, slotId);
                int displayId = OfficialDungeonUiBindingRuntime.PrivateDisplayDungeonId;
                var display = map12PrivateDungeonSession.FindDungeon(displayId);
                int difficulty = display.difficulties.Where(value => map12PrivateDungeonSession.IsUnlocked(displayId, value.difficulty))
                    .Select(value => value.difficulty).DefaultIfEmpty(display.difficulties.Min(value => value.difficulty)).Max();
                GameObject window = map12PrivateDungeonDialog;
                OfficialDungeonUiBindingRuntime.InitializePrivateDifficulty(window, map12PrivateDungeonSession, difficulty,
                    selected =>
                    {
                        var displayMap = map12PrivateDungeonSession.FindMap(display.difficulties.Single(value => value.difficulty == selected).mapGroupId);
                        var gradeRows = displayMap.gradeRows.Where(value => value.difficulty == selected).ToArray();
                        OfficialDungeonUiBindingRuntime.RefreshPrivateDungeonDetails(window, displayId, selected,
                            gradeRows.Min(value => value.grade), gradeRows.Max(value => value.grade));
                    }, hud.ShowOfficialNotice);
                Button battle = map12PrivateDungeonDialog.transform.Find("BgDialog3/Content/DailyDungeonDetailContent/Content/NormalGo/UnlockGo/BtnContent/BattleBtn")
                    ?.GetComponent<Button>();
                if (battle == null) battle = FindNamedTransform(map12PrivateDungeonDialog.transform, "BattleBtn")?.GetComponent<Button>();
                if (battle == null) throw new InvalidDataException("Original private daily dungeon battle button is missing.");
                battle.onClick.RemoveAllListeners();
                battle.onClick.AddListener(() => StartMap12PrivateDungeonBattle(entity));
                Transform matching = FindNamedTransform(map12PrivateDungeonDialog.transform, "GameMatch");
                if (matching == null) throw new InvalidDataException("Original private matching presentation is missing.");
                matching.gameObject.SetActive(false);
                map12PrivateDungeonDialog.transform.SetParent(hud.ui.OverlayRoot, false);
                Destroy(staging);
                ShowMap12PrivateDungeonDialog();
            }
            catch
            {
                Destroy(map12PrivateDungeonDialog);
                Destroy(staging);
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
            map12PrivateDungeonSession = DailyDungeonModule.OpenPrivateMap(slots, slotId);
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
            GameObject prefab = Resources.Load<GameObject>("Dungeons/UI/884_156692/DailyDungeonBigMapDialog");
            if (prefab == null) throw new InvalidOperationException("Official daily dungeon route map is missing.");
            map12PrivateDungeonRouteDialog = Instantiate(prefab, hud.ui.OverlayRoot, false);
            map12PrivateDungeonRouteDialog.name = "DailyDungeonBigMapDialog";
            map12PrivateDungeonRouteDialog.transform.SetAsLastSibling();
            PlaceMap12PrivateDungeonRouteOnTop();
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

        private void RenderMap12PrivateDungeonRoute()
        {
            if (map12PrivateDungeonRouteDialog == null || map12PrivateDungeonSession?.ActiveRun == null) return;
            OfficialDungeonRoutePresenter.Refresh(map12PrivateDungeonRouteDialog, map12PrivateDungeonSession,
                SelectMap12PrivateDungeonRoom, CloseMap12PrivateDungeonRoute);
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
            if (map12PrivateDungeonAssistView != null) return;
            if (map12PrivateDungeonBattleActive) return;
            map12PrivateDungeonAssistProgress ??= OfflineNpcAssistanceProgress.Open(
                new ProfileSlotCatalog(persistentDataPath), slotId);
            map12PrivateDungeonAssistCatalog ??= OfficialCombatAssetCatalog.LoadDefault().LoadNpcAssistance();
            DailyDungeonRunSave run = map12PrivateDungeonSession?.ActiveRun;
            DailyDungeonRuntimeRoom room = run == null ? null : map12PrivateDungeonSession.FindRoom(roomId);
            DailyDungeonRuntimeStage stage = room == null ? null : map12PrivateDungeonSession.FindStage(room.stageId);
            if (run == null || room == null || stage == null || run.currentRoomId != roomId)
                throw new InvalidOperationException("Official private daily dungeon route room is incomplete.");
            map12PrivateDungeonAssistRoomId = roomId;
            map12PrivateDungeonAssistEntryId = Map12PrivateDungeonAssistEntry(run, roomId);
            OfflineNpcAssistBattle activeAssist = map12PrivateDungeonAssistProgress.ActiveBattle;
            bool matchingActiveBattle = activeAssist != null
                && activeAssist.playType == Map12PrivateDungeonAssistPlayType
                && activeAssist.entryId == map12PrivateDungeonAssistEntryId
                && activeAssist.stageId == stage.id;
            if (!string.IsNullOrEmpty(run.activeBattleCheckpoint) || matchingActiveBattle)
            {
                StartMap12PrivateDungeonBattleRunner(roomId);
                return;
            }
            if (activeAssist != null)
                throw new InvalidOperationException("另一场 NPC 助战战斗尚未结束。");

            List<OfficialBattleAssistSelectionView.Candidate> candidates =
                Map12PrivateDungeonAssistCandidates(map12PrivateDungeonAssistEntryId,
                    out map12PrivateDungeonAssistMaxPlayer);
            map12PrivateDungeonAssistView?.Dispose();
            map12PrivateDungeonAssistView = new OfficialBattleAssistSelectionView(hud.ui.OverlayRoot,
                candidates, map12PrivateDungeonAssistMaxPlayer,
                SelectMap12PrivateDungeonAssistCandidate,
                ConfirmMap12PrivateDungeonAssistSelection,
                CloseMap12PrivateDungeonAssistPicker);
        }

        private void StartMap12PrivateDungeonBattleRunner(int roomId)
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
                    avatar.transform, hud.ui.OverlayRoot,
                    new OfficialBattleHostPresentation(worldCamera, mapRoot, hud.ui.OverlayRoot,
                        additionalHudRoots: new[] { hud.ui.Find("WorldCanvas", "/WorldCanvas").transform }),
                    slotId, persistentDataPath,
                    (Action<bool>)FinishMap12PrivateDungeonBattle,
                    map12PrivateDungeonAssistProgress,
                    map12PrivateDungeonAssistCatalog,
                    new OfficialBattleAssistOptions
                    {
                        PlayType = Map12PrivateDungeonAssistPlayType,
                        EntryId = map12PrivateDungeonAssistEntryId
                    } });
                if (map12PrivateDungeonRouteDialog != null) map12PrivateDungeonRouteDialog.SetActive(false);
            }
            catch (TargetInvocationException error)
            {
                map12PrivateDungeonBattleActive = false;
                Destroy(runnerObject);
                map12PrivateDungeonRunner = null;
                throw error.InnerException ?? error;
            }
            catch
            {
                map12PrivateDungeonBattleActive = false;
                Destroy(runnerObject);
                map12PrivateDungeonRunner = null;
                throw;
            }
        }

        private List<OfficialBattleAssistSelectionView.Candidate> Map12PrivateDungeonAssistCandidates(
            string entryId, out int maxPlayer)
        {
            map12PrivateDungeonBattleModels ??= OfficialBattleModelCatalog.LoadDefault();
            map12PrivateDungeonTravelCatalog ??= OfficialNpcTravelCatalog.LoadDefault();
            EnsureMap12PrivateDungeonHomeAssets();
            IReadOnlyList<int> candidates = map12PrivateDungeonAssistCatalog.FightAssistNpcClassIds();
            var friendships = (progressionSave.Save.Player.CharacterProperties.NpcFriendships
                    ?? new List<OfflineNpcFriendship>())
                .Where(value => value != null && value.NpcClassId > 0)
                .ToDictionary(value => value.NpcClassId, value => value.Level);
            FightOfficialNpcInviteRule invite =
                map12PrivateDungeonAssistCatalog.InviteRule(Map12PrivateDungeonAssistPlayType);
            NpcAssistRefreshCycle cycle = (NpcAssistRefreshCycle)invite.RefreshCycle;
            long now = DateTimeOffset.Now.ToUnixTimeSeconds();
            maxPlayer = Math.Max(0, Math.Min(3, invite.MaxPlayer));
            int[] existing = map12PrivateDungeonAssistProgress
                .Selected(Map12PrivateDungeonAssistPlayType, entryId).ToArray();
            var retained = new List<int>();
            var rows = new List<OfficialBattleAssistSelectionView.Candidate>(candidates.Count);
            int selectedCount = 0;
            foreach (int npcId in candidates)
            {
                bool meetsCondition = map12PrivateDungeonAssistCatalog.AssistConditionMet(npcId,
                    progressionSave.Save.Player.Level, friendships, progressionSave.StoryCount);
                bool encountered = friendships.ContainsKey(npcId);
                bool modelAvailable = map12PrivateDungeonBattleModels.HasNpcBattleAnimations(npcId);
                bool available = modelAvailable
                    && map12PrivateDungeonAssistCatalog.CanInviteNpc(npcId, encountered, meetsCondition)
                    && (invite.OneNpcMaxInviteCount <= 0
                        || map12PrivateDungeonAssistProgress.AssistCount(Map12PrivateDungeonAssistPlayType,
                            npcId, cycle, now) < invite.OneNpcMaxInviteCount);
                bool selected = existing.Contains(npcId) && available && selectedCount < maxPlayer;
                if (selected)
                {
                    selectedCount++;
                    retained.Add(npcId);
                }
                NpcTravelNpc travelNpc = map12PrivateDungeonTravelCatalog.Npc(npcId);
                rows.Add(new OfficialBattleAssistSelectionView.Candidate
                {
                    Id = npcId,
                    Name = Map12PrivateDungeonLocalizedText(
                        map12PrivateDungeonAssistCatalog.NpcNameKey(npcId), "伙伴 " + npcId),
                    Portrait = Map12PrivateDungeonFindDynamicSprite(
                        Map12PrivateDungeonNpcIconKey(travelNpc)),
                    Available = available,
                    Selected = selected,
                    FriendshipLevel = friendships.TryGetValue(npcId, out int friendshipLevel)
                        ? friendshipLevel : 1,
                    FriendshipBlocked = !encountered
                        && map12PrivateDungeonAssistCatalog.RequiresEncounter(npcId),
                    Reason = !modelAvailable ? "本地战斗动画未安装"
                        : !encountered && map12PrivateDungeonAssistCatalog.RequiresEncounter(npcId)
                            ? "尚未相识" : !meetsCondition ? "未满足助战条件"
                            : !available ? "今日助战次数已用完" : string.Empty
                });
            }
            if (!existing.SequenceEqual(retained)
                && !map12PrivateDungeonAssistProgress.SetSelection(Map12PrivateDungeonAssistPlayType,
                    entryId, retained, maxPlayer))
                throw new InvalidOperationException("素材秘境助战选择无法保存。");
            return rows;
        }

        private void SelectMap12PrivateDungeonAssistCandidate(int npcId, bool selected)
        {
            List<OfficialBattleAssistSelectionView.Candidate> current =
                Map12PrivateDungeonAssistCandidates(map12PrivateDungeonAssistEntryId, out int maxPlayer);
            OfficialBattleAssistSelectionView.Candidate candidate = current.FirstOrDefault(value => value.Id == npcId);
            if (candidate == null || !candidate.Available) return;
            var selection = map12PrivateDungeonAssistProgress
                .Selected(Map12PrivateDungeonAssistPlayType, map12PrivateDungeonAssistEntryId).ToList();
            if (selected)
            {
                if (!selection.Contains(npcId)) selection.Add(npcId);
            }
            else selection.Remove(npcId);
            if (!map12PrivateDungeonAssistProgress.SetSelection(Map12PrivateDungeonAssistPlayType,
                    map12PrivateDungeonAssistEntryId, selection, maxPlayer))
                throw new InvalidOperationException("素材秘境助战选择无法保存。");
            current = Map12PrivateDungeonAssistCandidates(map12PrivateDungeonAssistEntryId, out maxPlayer);
            map12PrivateDungeonAssistMaxPlayer = maxPlayer;
            map12PrivateDungeonAssistView?.Refresh(current, maxPlayer);
        }

        private void ConfirmMap12PrivateDungeonAssistSelection()
        {
            int roomId = map12PrivateDungeonAssistRoomId;
            CloseMap12PrivateDungeonAssistPicker();
            StartMap12PrivateDungeonBattleRunner(roomId);
        }

        private void CloseMap12PrivateDungeonAssistPicker()
        {
            OfficialBattleAssistSelectionView view = map12PrivateDungeonAssistView;
            object assets = map12PrivateDungeonHomeAssets;
            map12PrivateDungeonAssistView = null;
            map12PrivateDungeonHomeAssets = null;
            view?.Dispose();
            if (assets != null) map12PrivateDungeonHomeRelease.Invoke(assets, null);
        }

        private void EnsureMap12PrivateDungeonHomeAssets()
        {
            if (map12PrivateDungeonHomeAssets != null) return;
            Type catalogType = Type.GetType(
                "Zjcs.Presentation.Home.HomeAssetCatalog, Zjcs.Presentation.Home", true);
            MethodInfo loadDefault = catalogType.GetMethod("LoadDefault", BindingFlags.Public | BindingFlags.Static);
            map12PrivateDungeonHomeLocalizedText ??= catalogType.GetMethod("LocalizedText",
                new[] { typeof(string), typeof(string) });
            map12PrivateDungeonHomeFindDynamicSprite ??= catalogType.GetMethod("FindDynamicSprite",
                new[] { typeof(string) });
            map12PrivateDungeonHomeRelease ??= catalogType.GetMethod("Release", Type.EmptyTypes);
            if (loadDefault == null || map12PrivateDungeonHomeLocalizedText == null
                || map12PrivateDungeonHomeFindDynamicSprite == null || map12PrivateDungeonHomeRelease == null)
                throw new MissingMethodException("Official home asset catalog API is unavailable.");
            map12PrivateDungeonHomeAssets = loadDefault.Invoke(null, null);
        }

        private string Map12PrivateDungeonLocalizedText(string key, string fallback)
        {
            EnsureMap12PrivateDungeonHomeAssets();
            return (string)map12PrivateDungeonHomeLocalizedText.Invoke(
                map12PrivateDungeonHomeAssets, new object[] { key, fallback });
        }

        private Sprite Map12PrivateDungeonFindDynamicSprite(string key)
        {
            EnsureMap12PrivateDungeonHomeAssets();
            return (Sprite)map12PrivateDungeonHomeFindDynamicSprite.Invoke(
                map12PrivateDungeonHomeAssets, new object[] { key });
        }

        private static string Map12PrivateDungeonAssistEntry(DailyDungeonRunSave run, int roomId) =>
            "map12-private-dungeon:" + run.id + ":" + roomId;

        private static string Map12PrivateDungeonNpcIconKey(NpcTravelNpc npc)
        {
            string configuredPath = npc == null ? string.Empty : npc.figure_image;
            if (string.IsNullOrWhiteSpace(configuredPath)) return string.Empty;
            int separator = configuredPath.LastIndexOf('/');
            return separator >= 0 ? configuredPath.Substring(separator + 1) : configuredPath;
        }

        private void FinishMap12PrivateDungeonBattle(bool won)
        {
            map12PrivateDungeonBattleActive = false;
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
