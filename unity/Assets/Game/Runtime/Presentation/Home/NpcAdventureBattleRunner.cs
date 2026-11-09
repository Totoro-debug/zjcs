using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.World;
using Zjcs.Presentation.Character;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.World;

namespace Zjcs.Presentation.Home
{
    /// <summary>Offline adapter for the official NPC Adventure Map10 battle flow.</summary>
    public sealed class NpcAdventureBattleRunner : MonoBehaviour, IOfficialBattleModeAdapter
    {
        private HomeFeatureRuntime host;
        private OfficialNpcAdventureBattleCatalog catalog;
        private OfficialBattleRuntimeController controller;
        private Transform battleRoot;
        private NpcTravelNode node;
        private Map10RuntimeStage stage;
        private Map10RuntimeBattleScene scene;
        private bool complete;
        private bool disposed;
        private bool startedHere;
        private bool assistCommitted;
        private string receipt;

        public OfficialBattleRuntimeController Controller => controller;
        public FightBattleReport FinalReport { get; private set; }

        public void Begin(HomeFeatureRuntime runtime, NpcTravelNode selectedNode,
            IReadOnlyList<int> selectedNpcClassIds)
        {
            host = runtime ?? throw new ArgumentNullException(nameof(runtime));
            node = selectedNode ?? throw new ArgumentNullException(nameof(selectedNode));
            if (selectedNpcClassIds == null || selectedNpcClassIds.Count != OfflineNpcAdventureSession.MaximumCompanions
                || selectedNpcClassIds.Distinct().Count() != selectedNpcClassIds.Count)
                throw new InvalidOperationException("羁绊冒险必须选择 3 名不同的伙伴。");

            OfflineNpcAdventureProgress state = host.NpcAdventure.State;
            bool resume = state.ActiveStageId > 0;
            if (resume && state.ActiveStageId != node.stage_id)
                throw new InvalidOperationException("当前已有另一场羁绊冒险战斗。");
            if (!resume)
            {
                ProgressionResult selection = host.NpcAdventure.SetSelectedNpcClassIds(selectedNpcClassIds);
                if (!selection.IsApplied && selection.Status != ProgressionResultStatus.Unchanged)
                    throw new InvalidOperationException(selection.Message ?? "伙伴选择保存失败。");
                ProgressionResult started = host.NpcAdventure.BeginBattle(node, DateTime.Now);
                if (!started.IsApplied && started.Status != ProgressionResultStatus.Unchanged)
                    throw new InvalidOperationException(started.Message ?? "羁绊冒险无法开始。");
                startedHere = true;
                state = host.NpcAdventure.State;
            }
            receipt = state.ActiveBattleReceipt;
            try
            {
                catalog = host.NpcAdventureBattleCatalog;
                if (!catalog.TryGetBattle(node.stage_id, out stage, out Map10RuntimeRoster roster, out scene))
                    throw new InvalidOperationException("官方羁绊冒险关卡数据不可用：" + node.stage_id);
                CommitNpcAdventureAssistance(selectedNpcClassIds, resume);

                FightBattleCheckpoint checkpoint;
                if (resume && !string.IsNullOrEmpty(state.ActiveBattleCheckpoint))
                {
                    checkpoint = FightBattleCheckpointSerializer.Load(state.ActiveBattleCheckpoint);
                    if (checkpoint.StageId != stage.Id)
                        throw new InvalidOperationException("羁绊冒险 checkpoint 关卡不匹配。");
                }
                else
                {
                    checkpoint = OfficialMapBattleCheckpointBuilder.Create(new OfficialMapBattleCheckpointRequest
                    {
                        Stage = stage,
                        Roster = roster,
                        Scene = scene,
                        Player = host.Profile.Save.Player,
                        World = host.Profile.Save.World,
                        Progression = host.DailyDungeonProgression,
                        RandomSeed = StableSeed(receipt, stage.Id),
                        UseMonsterProperties = true
                    });
                    AddAssistNpcs(checkpoint, scene, state.SelectedNpcClassIds);
                    SaveCheckpoint(checkpoint);
                }
                MountBattle(checkpoint);
            }
            catch (Exception startError)
            {
                if (startedHere)
                {
                    Exception rollbackError = null;
                    if (assistCommitted)
                    {
                        try
                        {
                            host.NpcAssistanceProgress.CancelBattle((int)NpcAssistPlayType.BondAdventure,
                                AssistanceEntryId, stage.Id);
                        }
                        catch (Exception error) { rollbackError = error; }
                        assistCommitted = false;
                    }
                    try
                    {
                        ProgressionResult rollback = host.NpcAdventure.CancelBattleStart(node.stage_id);
                        if (!rollback.IsApplied && rollback.Status != ProgressionResultStatus.Unchanged)
                            throw new InvalidOperationException(rollback.Message ?? "羁绊冒险次数回滚失败。");
                    }
                    catch (Exception error)
                    {
                        rollbackError = rollbackError == null ? error
                            : new AggregateException(rollbackError, error);
                    }
                    if (rollbackError != null)
                        throw new AggregateException("羁绊冒险启动失败，且至少一项本地回滚未完成。",
                            startError, rollbackError);
                }
                throw;
            }
        }

        private string AssistanceEntryId => "npc-adventure-stage:" + stage.Id;

        private void CommitNpcAdventureAssistance(IReadOnlyList<int> selectedNpcClassIds, bool resume)
        {
            int playType = (int)NpcAssistPlayType.BondAdventure;
            FightOfficialNpcInviteRule rule = host.NpcAssistanceCatalog.InviteRule(playType);
            OfflineNpcAssistBattle active = host.NpcAssistanceProgress.ActiveBattle;
            if (active != null && active.playType == playType
                && (!resume || active.stageId != stage.Id
                    || !string.Equals(active.entryId, AssistanceEntryId, StringComparison.Ordinal)))
            {
                host.NpcAssistanceProgress.FinishBattle(active.playType, active.entryId, active.stageId);
                active = null;
            }
            if (active != null)
            {
                if (active.playType != playType || active.stageId != stage.Id
                    || !string.Equals(active.entryId, AssistanceEntryId, StringComparison.Ordinal))
                    throw new InvalidOperationException("另一场伙伴助战战斗仍处于进行状态。");
                assistCommitted = true;
                return;
            }
            if (!host.NpcAssistanceProgress.SetSelection(playType, AssistanceEntryId,
                    selectedNpcClassIds, rule.MaxPlayer))
                throw new InvalidOperationException("羁绊冒险伙伴选择不符合官方助战配置。");
            host.NpcAssistanceProgress.CommitBattle(playType, AssistanceEntryId, stage.Id,
                rule.MaxPlayer, rule.OneNpcMaxInviteCount, rule.MaxInviteCount,
                (NpcAssistRefreshCycle)rule.RefreshCycle, DateTimeOffset.Now.ToUnixTimeSeconds());
            assistCommitted = true;
        }

        private void FinishNpcAdventureAssistance()
        {
            if (stage == null) return;
            OfflineNpcAssistBattle active = host.NpcAssistanceProgress.ActiveBattle;
            if (active == null || active.playType != (int)NpcAssistPlayType.BondAdventure
                || active.stageId != stage.Id
                || !string.Equals(active.entryId, AssistanceEntryId, StringComparison.Ordinal)) return;
            host.NpcAssistanceProgress.FinishBattle(active.playType, active.entryId, active.stageId);
            assistCommitted = false;
        }

        private void SaveCheckpoint(FightBattleCheckpoint checkpoint)
        {
            ProgressionResult result = host.NpcAdventure.SaveBattleCheckpoint(stage.Id,
                FightBattleCheckpointSerializer.Save(checkpoint));
            if (!result.IsApplied && result.Status != ProgressionResultStatus.Unchanged)
                throw new InvalidOperationException(result.Message ?? "羁绊冒险 checkpoint 保存失败。");
        }

        private void MountBattle(FightBattleCheckpoint checkpoint)
        {
            Transform playerTransform = host.HomePlayerTransform;
            if (playerTransform == null) throw new InvalidOperationException("主城角色模型不可用。");
            FightBattleUnitState player = checkpoint.Units.First(value => value.Id == 1);
            Map10RuntimeVector3 origin = scene.FightGridGroundComponent.OriginPos;
            battleRoot = new GameObject("NpcAdventureBattleWorld").transform;
            battleRoot.position = playerTransform.position - new Vector3(origin.X + player.X,
                origin.Y + player.Y, origin.Z);
            OfficialCombatAssetCatalog assets = OfficialCombatAssetCatalog.LoadDefault();
            OfficialCombatSkillIconCatalog skillIcons = OfficialCombatSkillIconCatalog.TryLoadDefault();
            OfficialBattleHudCatalog hudCatalog = OfficialBattleHudCatalog.LoadDefault();
            Sprite fallback = hudCatalog.FindSprite("C_none-fa3e084e40.png");
            if (fallback == null) throw new InvalidOperationException("官方战斗默认头像缺失。");
            controller = gameObject.AddComponent<OfficialBattleRuntimeController>();
            controller.Begin(new OfficialBattleRequest
            {
                Checkpoint = checkpoint,
                WorldRoot = battleRoot,
                PlayerAppearance = new OfficialBattlePlayerAppearanceSource(1, 1,
                    () => host.Profile.Save.Player, id => host.DailyDungeonProgression.Catalog.ItemType(id)),
                HudParent = host.Hud.CanvasTransform,
                ModeAdapter = this,
                SpeedConditionMet = conditionId =>
                {
                    var context = new OfficialConditionContext { Level = host.Level };
                    foreach (int map in host.Profile.Save.Player.UnlockedMapClassIds ?? new List<int>())
                        context.UnlockedMapClassIds.Add(map);
                    return host.DailyDungeonProgression.Catalog.EvaluateCondition(conditionId, context);
                },
                TimelinePortrait = OfficialBattleCharacterPortraits.ForProfile(battleRoot.gameObject,
                    host.DailyDungeonProgression, host.ActiveProfileSlotId, host.SavesRoot),
                HostPresentation = new OfficialBattleHostPresentation(host.Scene.Camera,
                    host.Scene.Root.transform, host.Hud.CanvasTransform, host.SetBattlePresentationActive),
                AnimationGroupResolver = unit => OfficialCharacterAnimationProfile.GroupForProfession(unit.Profession),
                CombatAssets = assets,
                SkillIcon = itemId => skillIcons == null ? fallback : skillIcons.Find(itemId) ?? fallback,
                EnvironmentGameId = stage.GameId,
                StatueProgress = host.Profile,
                Options = OfficialMapBattleSessionOptionsBuilder.Create(stage, true, 3, 3, player.X, player.Y),
                RestoreCheckpoint = checkpoint.Finished || checkpoint.Counter > 0
                    || checkpoint.Timeline.Entries.Count > 0,
                InitialStepDelay = 0f
            });
        }

        private void AddAssistNpcs(FightBattleCheckpoint checkpoint, Map10RuntimeBattleScene battleScene,
            IReadOnlyList<int> npcClassIds)
        {
            if (npcClassIds == null || npcClassIds.Count == 0) return;
            Map10RuntimeFactionInfo faction = battleScene.FightComponent?.FactionInfoList?
                .FirstOrDefault(value => value != null && value.Faction == 0);
            if (faction?.EntryPosList == null || faction.EntryPosList.Length == 0)
                throw new InvalidOperationException("官方羁绊冒险场景没有玩家出生点。");
            FightBattleUnitState player = checkpoint.Units.First(value => value.Id == 1);
            int moveMask = player.MoveMask;
            var occupied = new HashSet<FightGridPoint>(checkpoint.Units.Select(value =>
                new FightGridPoint(value.X, value.Y)));
            var entryPoints = faction.EntryPosList.Where(value => value != null)
                .Select(value => new FightGridPoint(value.X, value.Y)).Distinct().ToList();
            var candidates = new List<FightGridPoint>(entryPoints.Skip(1));
            candidates.AddRange((battleScene.FightGridGroundComponent.PosInfos ?? Array.Empty<Map10RuntimeBattleCell>())
                .Where(value => value?.Pos != null)
                .Select(value => new FightGridPoint(value.Pos.X, value.Pos.Y))
                .Distinct()
                .OrderBy(value => Math.Abs(value.X - player.X) + Math.Abs(value.Y - player.Y)));
            var positions = new List<FightGridPoint>();
            foreach (FightGridPoint candidate in candidates)
            {
                if (!checkpoint.Grid.CanStand(candidate, moveMask, occupied)) continue;
                positions.Add(candidate);
                occupied.Add(candidate);
                if (positions.Count == npcClassIds.Count) break;
            }
            if (positions.Count != npcClassIds.Count)
                throw new InvalidOperationException("官方羁绊冒险场景没有足够的助战位置。");
            OfflinePlayerProgress save = host.Profile.Save.Player;
            for (int index = 0; index < npcClassIds.Count; index++)
            {
                FightGridPoint position = positions[index];
                checkpoint.Units.Add(host.NpcAssistanceCatalog.CreateUnit(checkpoint.Units.Count + 1,
                    1, Math.Max(1, save.Level), Math.Max(0, save.Rank), Math.Max(0, save.SubRank),
                    position.X, position.Y, moveMask, npcClassIds[index]));
            }
        }

        private void Update()
        {
            if (controller != null && !complete) controller.Tick(Time.deltaTime);
        }

        public void OnBattleCompleted(FightBattleCheckpoint checkpoint)
        {
            if (complete || checkpoint == null) return;
            complete = true;
            bool won = checkpoint.Outcome != null && checkpoint.Outcome.Won;
            ulong randomState = unchecked((ulong)StableSeed(receipt, stage.Id));
            List<NpcAdventureRewardItem> rewards = won
                ? catalog.RollRewards(stage.Id, host.Profile.Save.Player.Level,
                    host.Profile.Save.Player.SubRank, ref randomState)
                : new List<NpcAdventureRewardItem>();
            var pairs = rewards.Select(value => new KeyValuePair<int, long>(value.ItemId, value.Count)).ToList();
            ProgressionResult settled = host.NpcAdventure.CompleteBattle(stage.Id, won, pairs, receipt);
            if (!settled.IsApplied && settled.Status != ProgressionResultStatus.Unchanged)
                throw new InvalidOperationException(settled.Message ?? "羁绊冒险结算失败。");
            FinishNpcAdventureAssistance();
            DisposeBattle();
            host.Hud.RefreshAll();
            host.Hud.ShowNpcAdventureBattleResult(node, won, rewards,
                () => host.Hud.OpenNpcAdventureBattleSelection());
            Destroy(this);
        }

        public void OnBattleStepCompleted(FightBattleExecutionResult result)
        {
            if (result?.Checkpoint != null) SaveCheckpoint(result.Checkpoint);
        }

        public void OnBattleReportReady(FightBattleReport report) => FinalReport = report;

        public void OnBattleHudControlClicked(string path)
        {
            if (path != OfficialBattleHudView.CloseButtonPath || complete || stage == null) return;
            complete = true;
            ProgressionResult settled = host.NpcAdventure.CompleteBattle(stage.Id, false,
                Array.Empty<KeyValuePair<int, long>>(), receipt);
            if (!settled.IsApplied && settled.Status != ProgressionResultStatus.Unchanged)
                host.Hud.ShowNotice(settled.Message ?? "羁绊冒险退出失败。");
            FinishNpcAdventureAssistance();
            DisposeBattle();
            Destroy(this);
        }

        internal static int StableSeed(string value, int stageId)
        {
            unchecked
            {
                uint hash = 2166136261u;
                foreach (char character in (value ?? string.Empty) + ":" + stageId)
                    hash = (hash ^ character) * 16777619u;
                return (int)(hash % (FightRandomStream.Modulus - 1)) + 1;
            }
        }

        private void OnDestroy() => DisposeBattle();

        private void DisposeBattle()
        {
            if (disposed) return;
            disposed = true;
            if (controller != null)
            {
                controller.DisposeSession();
                Destroy(controller);
                controller = null;
            }
            if (battleRoot != null)
            {
                Destroy(battleRoot.gameObject);
                battleRoot = null;
            }
        }
    }
}
