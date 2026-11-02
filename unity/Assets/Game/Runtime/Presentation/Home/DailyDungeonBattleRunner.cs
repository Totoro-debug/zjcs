using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Presentation.Character;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Offline.State;
using Zjcs.Offline.World;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed class DailyDungeonBattleRunner : MonoBehaviour, IOfficialBattleModeAdapter
    {
        private HomeFeatureRuntime host;
        private Action<bool> completed;
        private OfficialBattleRuntimeController controller;
        private Transform battleRoot;
        private Transform previousPlayerParent;
        private Vector3 previousPlayerPosition;
        private Quaternion previousPlayerRotation;
        private Vector3 previousPlayerScale;
        private Transform playerTransform;
        private bool resultHandled;
        private int roomId;
        private string dailyAssistEntryId;
        private int assistPlayType = (int)NpcAssistPlayType.All;
        private int assistMaxPlayerOverride = -1;
        private bool challengeMode;
        private int challengeActivityId;
        private int challengeDurationHours;
        private int challengeDifficulty;
        private OfflineMainlineProgressSession battleProfile;
        private OfflineCharacterProgressionService battleProgression;
        private DailyDungeonRuntimeData battleCatalog;
        private Transform battleCanvas;
        private Transform battlePlayer;
        private bool privateMapEntry;
        private int privateMapRoomId;
        private OfficialBattleHostPresentation privateMapPresentation;
        private string portraitSlotId, portraitSavesRoot;

        public void Begin(HomeFeatureRuntime runtime, int selectedRoomId, string assistEntryId,
            Action<bool> onCompleted, OfficialBattleAssistOptions assistOptions = null)
        {
            host = runtime ?? throw new ArgumentNullException(nameof(runtime));
            battleProfile = host.Profile;
            battleProgression = host.DailyDungeonProgression;
            battleCatalog = host.DailyDungeons.Catalog;
            battleCanvas = host.Hud.CanvasTransform;
            battlePlayer = host.HomePlayerTransform;
            portraitSlotId = host.ActiveProfileSlotId;
            portraitSavesRoot = host.SavesRoot;
            completed = onCompleted;
            if (string.IsNullOrWhiteSpace(assistEntryId))
                throw new ArgumentException("Daily dungeon assistance entry identity is required.", nameof(assistEntryId));
            ConfigureAssist(assistEntryId, assistOptions);
            assistEntryId = dailyAssistEntryId;
            roomId = selectedRoomId;
            DailyDungeonRunSave run = host.DailyDungeons.ActiveRun;
            if (run == null || run.currentRoomId != roomId) throw new InvalidOperationException("Daily dungeon room is not active.");
            DailyDungeonRuntimeRoom room = host.DailyDungeons.FindRoom(roomId);
            DailyDungeonRuntimeStage stage = room == null ? null : host.DailyDungeons.FindStage(room.stageId);
            if (stage == null) throw new InvalidOperationException("Official daily dungeon battle stage is missing.");
            DailyDungeonRuntimeBattleScene scene = host.DailyDungeons.Catalog.battleScenes
                .FirstOrDefault(value => value.gameId == stage.gameId);
            if (scene == null) throw new InvalidOperationException("Official daily dungeon battle scene is missing.");

            OfficialCombatAssetCatalog combatAssets = OfficialCombatAssetCatalog.LoadDefault();
            FightOfficialCombatCatalog combat = combatAssets.Load();
            FightBattleCheckpoint checkpoint;
            bool restore = !string.IsNullOrEmpty(run.activeBattleCheckpoint);
            FightOfficialNpcAssistanceCatalog npcCatalog = host.NpcAssistanceCatalog;
            FightOfficialNpcInviteRule inviteRule = npcCatalog.InviteRule(assistPlayType);
            OfflineNpcAssistBattle activeAssist = host.NpcAssistanceProgress.ActiveBattle;
            if (activeAssist != null && (activeAssist.playType != assistPlayType
                || activeAssist.entryId != assistEntryId || activeAssist.stageId != stage.id))
                throw new InvalidOperationException("另一场 NPC 助战战斗尚未结束。");
            int[] selectedNpcIds;
            if (restore)
            {
                checkpoint = FightBattleCheckpointSerializer.Load(run.activeBattleCheckpoint);
                if (checkpoint.StageId != stage.id || checkpoint.Finished)
                    throw new InvalidOperationException("Saved daily dungeon battle checkpoint does not match the active room.");
                if (checkpoint.StageType == 0) checkpoint.StageType = 11;
                selectedNpcIds = checkpoint.Units.Where(unit => unit.NpcClassId > 0)
                    .Select(unit => unit.NpcClassId).ToArray();
                if (activeAssist != null && !(activeAssist.npcClassIds ?? new List<int>()).SequenceEqual(selectedNpcIds))
                    throw new InvalidOperationException("日常副本助战快照与战斗 checkpoint 不一致。");
                if (activeAssist == null && selectedNpcIds.Length > 0)
                    throw new InvalidOperationException("日常副本的战斗 checkpoint 缺少已提交的助战快照。");
            }
            else
            {
                selectedNpcIds = activeAssist != null
                    ? (activeAssist.npcClassIds ?? new List<int>()).ToArray()
                    : ResolveDailyChallengeAssistSelection(npcCatalog, inviteRule, assistEntryId);
                checkpoint = BuildCheckpoint(run, stage, scene, combatAssets, combat, selectedNpcIds);
            }

            bool committedAssist = false;
            try
            {
                if (!restore && selectedNpcIds.Length > 0 && activeAssist == null)
                {
                    host.NpcAssistanceProgress.CommitBattle(assistPlayType, assistEntryId, stage.id,
                        ResolveAssistMaxPlayer(inviteRule), inviteRule.OneNpcMaxInviteCount,
                        inviteRule.MaxInviteCount, (NpcAssistRefreshCycle)inviteRule.RefreshCycle,
                        DateTimeOffset.Now.ToUnixTimeSeconds());
                    committedAssist = true;
                }
                MountBattle(checkpoint, scene, combatAssets, restore, false);
                if (!host.DailyDungeons.SaveBattleCheckpoint(
                        FightBattleCheckpointSerializer.Save(controller.Checkpoint)))
                    throw new InvalidOperationException("日常副本战斗 checkpoint 保存失败。");
            }
            catch
            {
                if (committedAssist) host.NpcAssistanceProgress.CancelBattle(assistPlayType, assistEntryId, stage.id);
                DisposeBattle();
                throw;
            }
        }

        public void BeginPrivateMapRoom(OfflineMainlineProgressSession profile,
            OfflineCharacterProgressionService progression, OfflineDailyDungeonSession dungeons,
            Transform player, Transform canvas, OfficialBattleHostPresentation presentation,
            string slotId, string savesRoot, Action<bool> onCompleted)
        {
            if (profile == null || progression == null || dungeons == null || player == null || canvas == null)
                throw new ArgumentNullException("Private daily dungeon battle dependencies are incomplete.");
            privateMapPresentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            battleProfile = profile;
            battleProgression = progression;
            battleCatalog = dungeons.Catalog;
            battlePlayer = player;
            battleCanvas = canvas;
            portraitSlotId = slotId;
            portraitSavesRoot = savesRoot;
            privateMapEntry = true;
            completed = onCompleted;

            DailyDungeonRunSave run = dungeons.ActiveRun;
            DailyDungeonRuntimeRoom room = run == null ? null : dungeons.FindRoom(run.currentRoomId);
            DailyDungeonRuntimeStage stage = room == null ? null : dungeons.FindStage(room.stageId);
            DailyDungeonRuntimeBattleScene scene = stage == null ? null
                : battleCatalog.battleScenes.FirstOrDefault(value => value.gameId == stage.gameId);
            if (run?.dungeonId != 121 || run.difficulty != 50 || room == null || stage == null || scene == null)
                throw new InvalidOperationException("Official private daily dungeon route room is incomplete.");
            privateMapRoomId = room.id;

            OfficialCombatAssetCatalog combatAssets = OfficialCombatAssetCatalog.LoadDefault();
            FightOfficialCombatCatalog combat = combatAssets.Load();
            FightBattleCheckpoint checkpoint = BuildCheckpoint(run, stage, scene, combatAssets, combat, Array.Empty<int>());
            MountBattle(checkpoint, scene, combatAssets, false, false);
        }

        public void BeginChallenge(HomeFeatureRuntime runtime, int activityId, int stageId,
            int stageType, int durationHours, int dailyLimit, int difficulty, Action<bool> onCompleted,
            OfficialBattleAssistOptions assistOptions = null)
        {
            host = runtime ?? throw new ArgumentNullException(nameof(runtime));
            portraitSlotId = host.ActiveProfileSlotId;
            portraitSavesRoot = host.SavesRoot;
            battleProfile = host.Profile;
            battleProgression = host.DailyDungeonProgression;
            battleCatalog = host.DailyDungeons.Catalog;
            battleCanvas = host.Hud.CanvasTransform;
            battlePlayer = host.HomePlayerTransform;
            completed = onCompleted;
            challengeMode = true;
            challengeActivityId = activityId;
            challengeDurationHours = durationHours;
            host.DailyChallenges.EnterFeature(DateTime.Now);
            string savedCheckpoint = host.DailyChallenges.ActiveBattleCheckpoint(activityId);
            bool restore = !string.IsNullOrEmpty(savedCheckpoint);
            challengeDifficulty = restore
                ? host.DailyChallenges.ActiveChallengeDifficulty(activityId) : difficulty;
            ConfigureAssist("daily-challenge:" + activityId, assistOptions);
            if (!restore && !host.DailyChallenges.CanParticipate(activityId, DateTime.Now, durationHours,
                    dailyLimit, out string reason))
                throw new InvalidOperationException(reason);

            OfflineDailyChallengeBattleCatalog challengeCatalog = OfflineDailyChallengeBattleCatalog.LoadDefault();
            DailyDungeonRuntimeStage stage = challengeCatalog.FindStage(stageId);
            if (stage == null) throw new InvalidOperationException("Official daily challenge stage is missing: " + stageId);
            DailyDungeonRuntimeBattleScene scene = challengeCatalog.FindScene(stage.gameId);
            if (scene == null) throw new InvalidOperationException("Official daily challenge battle scene is missing.");
            OfficialCombatAssetCatalog combatAssets = OfficialCombatAssetCatalog.LoadDefault();
            FightOfficialCombatCatalog combat = combatAssets.Load();
            FightOfficialNpcAssistanceCatalog npcCatalog = host.NpcAssistanceCatalog;
            FightOfficialNpcInviteRule inviteRule = npcCatalog.InviteRule(assistPlayType);
            string assistEntryId = dailyAssistEntryId;
            OfflineNpcAssistBattle activeAssistBattle = host.NpcAssistanceProgress.ActiveBattle;
            if (activeAssistBattle != null
                && (activeAssistBattle.playType != assistPlayType || activeAssistBattle.entryId != assistEntryId
                    || activeAssistBattle.stageId != stageId))
                throw new InvalidOperationException("另一场 NPC 助战战斗尚未结束。");
            int[] selectedAssistNpcIds = restore ? Array.Empty<int>()
                : activeAssistBattle != null
                    ? (activeAssistBattle.npcClassIds ?? new List<int>()).ToArray()
                : ResolveDailyChallengeAssistSelection(npcCatalog, inviteRule, assistEntryId);
            FightBattleCheckpoint checkpoint = restore
                ? FightBattleCheckpointSerializer.Load(savedCheckpoint)
                : BuildCheckpoint(new DailyDungeonRunSave { id = "challenge-" + activityId + "-" +
                    DateTime.Now.Ticks }, stage, scene, combatAssets, combat, selectedAssistNpcIds,
                    challengeCatalog.Formations);
            if (checkpoint.StageId != stageId)
                throw new InvalidOperationException("Saved daily challenge checkpoint does not match its activity stage.");
            if (restore)
            {
                int[] restoredNpcIds = checkpoint.Units.Where(unit => unit.NpcClassId > 0)
                    .Select(unit => unit.NpcClassId).ToArray();
                if (activeAssistBattle != null
                    && !(activeAssistBattle.npcClassIds ?? new List<int>()).SequenceEqual(restoredNpcIds))
                    throw new InvalidOperationException("每日挑战助战快照与战斗 checkpoint 不一致。");
                if (!checkpoint.Finished && activeAssistBattle == null && restoredNpcIds.Length > 0)
                    throw new InvalidOperationException("每日挑战的战斗 checkpoint 缺少已提交的助战快照。");
            }
            if (checkpoint.Finished)
            {
                FinishBattleAssist(stageId);
                IReadOnlyList<OfficialChallengeReward> rewards = FinalizeChallenge(checkpoint,
                    checkpoint.Outcome.Won);
                host.Hud.OpenDailyChallengeResult(activityId, checkpoint.Outcome.Won, checkpoint, rewards);
                completed?.Invoke(checkpoint.Outcome.Won);
                Destroy(gameObject);
                return;
            }
            checkpoint.StageType = stageType;
            bool committedAssist = false;
            try
            {
                if (!restore && activeAssistBattle == null)
                {
                    host.NpcAssistanceProgress.CommitBattle(assistPlayType, assistEntryId, stageId,
                        ResolveAssistMaxPlayer(inviteRule), inviteRule.OneNpcMaxInviteCount,
                        inviteRule.MaxInviteCount, (NpcAssistRefreshCycle)inviteRule.RefreshCycle,
                        DateTimeOffset.Now.ToUnixTimeSeconds());
                    committedAssist = true;
                }
                MountBattle(checkpoint, scene, combatAssets, restore, true);
                string checkpointJson = FightBattleCheckpointSerializer.Save(controller.Checkpoint);
                bool recorded = restore
                    ? SaveChallengeCheckpoint(checkpointJson)
                    : host.DailyChallenges.RecordParticipation(activityId, DateTime.Now,
                        durationHours, dailyLimit, challengeDifficulty, checkpointJson);
                if (!recorded) throw new InvalidOperationException("挑战回执或战斗进度无法保存。");
            }
            catch
            {
                if (committedAssist) host.NpcAssistanceProgress.CancelBattle(assistPlayType, assistEntryId, stageId);
                DisposeBattle();
                throw;
            }
        }

        private void MountBattle(FightBattleCheckpoint checkpoint, DailyDungeonRuntimeBattleScene scene,
            OfficialCombatAssetCatalog combatAssets, bool restore, bool isChallenge)
        {
            FightOfficialCombatCatalog combat = combatAssets.Load();
            var world = new GameObject(isChallenge ? "DailyChallengeBattleWorld" : "DailyDungeonBattleWorld");
            battleRoot = world.transform;
            playerTransform = battlePlayer;
            if (playerTransform == null) throw new InvalidOperationException("Official home character model is unavailable.");
            previousPlayerParent = playerTransform.parent;
            previousPlayerPosition = playerTransform.localPosition;
            previousPlayerRotation = playerTransform.localRotation;
            previousPlayerScale = playerTransform.localScale;
            FightBattleUnitState player = checkpoint.Units.First(value => value.Id == 1);
            float[] origin = scene.gridOrigin ?? new[] { 0f, 0f, 0f };
            Vector3 playerPoint = new Vector3(origin[0] + player.X, origin[1] + player.Y,
                origin.Length > 2 ? origin[2] : 0f);
            battleRoot.position = playerTransform.position - playerPoint;
            playerTransform.SetParent(battleRoot, true);

            controller = gameObject.AddComponent<OfficialBattleRuntimeController>();
            OfficialBattleHudCatalog hudCatalog = OfficialBattleHudCatalog.LoadDefault();
            Sprite fallback = hudCatalog.FindSprite("C_none-fa3e084e40.png");
            if (fallback == null) throw new InvalidOperationException("Official battle fallback portrait is missing.");
            OfficialCombatSkillIconCatalog skillIcons = OfficialCombatSkillIconCatalog.TryLoadDefault();
            OfficialBattleMonsterPortraitCatalog portraits = privateMapEntry
                ? Resources.Load<OfficialBattleMonsterPortraitCatalog>("Map12/monster-portraits")
                : OfficialBattleMonsterPortraitCatalog.TryLoadDefault();
            var characterPortrait = OfficialBattleCharacterPortraits.ForProfile(battleRoot.gameObject,
                battleProgression, portraitSlotId, portraitSavesRoot);
            controller.Begin(new OfficialBattleRequest
            {
                Checkpoint = checkpoint,
                WorldRoot = battleRoot,
                EnvironmentGameId = scene.gameId,
                HudParent = battleCanvas,
                HostPresentation = privateMapEntry ? privateMapPresentation : new OfficialBattleHostPresentation(
                    host.Scene.Camera, host.Scene.Root.transform, battleCanvas, host.SetBattlePresentationActive),
                ModeAdapter = this,
                SkillIcon = itemId => skillIcons == null ? fallback : skillIcons.Find(itemId) ?? fallback,
                TimelinePortrait = unit => characterPortrait(unit) ?? (unit.CharacterType != 1
                    && unit.MonsterId > 0 ? portraits?.Find(combat.MonsterIconId(unit.MonsterId)) : null),
                AnimationGroupResolver = unit => OfficialCharacterAnimationProfile.GroupForProfession(unit.Profession),
                HudCatalog = hudCatalog,
                RestoreCheckpoint = restore,
                StatueProgress = battleProfile
            });
            controller.AttachUnitTransform(1, playerTransform);
            controller.ConfigureSpeeds(combatAssets.Load().SpeedSettings, 0, IsBattleSpeedConditionMet);
            string serialized = FightBattleCheckpointSerializer.Save(checkpoint);
            if (!isChallenge && !privateMapEntry && !host.DailyDungeons.SaveBattleCheckpoint(serialized))
                throw new InvalidOperationException("Daily dungeon battle checkpoint could not be saved.");
        }

        private void Update()
        {
            if (controller == null || controller.Checkpoint == null || resultHandled) return;
            controller.Tick(Time.deltaTime);
        }

        public void OnBattleCompleted(FightBattleCheckpoint checkpoint)
        {
            CompleteBattle(checkpoint != null && checkpoint.Outcome != null && checkpoint.Outcome.Won);
        }

        public void OnBattleStepCompleted(FightBattleExecutionResult result)
        {
            if (result?.Checkpoint == null) return;
            string serialized = FightBattleCheckpointSerializer.Save(result.Checkpoint);
            if (challengeMode) host.DailyChallenges.SaveBattleCheckpoint(challengeActivityId, serialized);
            else if (!privateMapEntry && !host.DailyDungeons.SaveBattleCheckpoint(serialized))
                throw new InvalidOperationException("Daily dungeon battle checkpoint could not be saved.");
        }

        private void CompleteBattle(bool won)
        {
            if (resultHandled) return;
            resultHandled = true;
            FightBattleCheckpoint checkpoint = controller.Checkpoint;
            if (privateMapEntry)
            {
                Action<bool> privateMapCallback = completed;
                DisposeBattle();
                privateMapCallback?.Invoke(won);
                Destroy(gameObject);
                return;
            }
            FinishBattleAssist(checkpoint.StageId);
            IReadOnlyList<OfficialChallengeReward> rewards = Array.Empty<OfficialChallengeReward>();
            if (challengeMode) rewards = FinalizeChallenge(checkpoint, won);
            else if (won)
            {
                if (!host.DailyDungeons.RecordRoomCleared(roomId,
                        FightBattleCheckpointSerializer.Save(checkpoint)))
                    throw new InvalidOperationException("Daily dungeon room result could not be recorded.");
            }
            if (challengeMode) host.DailyChallenges.SaveBattleCheckpoint(challengeActivityId, string.Empty);
            else if (!won) host.DailyDungeons.SaveBattleCheckpoint(string.Empty);
            Action<bool> callback = completed;
            DisposeBattle();
            if (challengeMode) host.Hud.OpenDailyChallengeResult(challengeActivityId, won, checkpoint, rewards);
            callback?.Invoke(won);
            Destroy(gameObject);
        }

        private IReadOnlyList<OfficialChallengeReward> FinalizeChallenge(FightBattleCheckpoint checkpoint, bool won)
        {
            IReadOnlyList<OfficialChallengeReward> grantedRewards = Array.Empty<OfficialChallengeReward>();
            DateTime now = DateTime.Now;
            if (challengeActivityId == OfflineDailyChallengeService.FunnyBossActivityId)
            {
                long damage = 0;
                foreach (FightBattleUnitStatistics statistic in checkpoint.Statistics?.Units
                             ?? new List<FightBattleUnitStatistics>())
                {
                    FightBattleUnitState unit = checkpoint.Units.FirstOrDefault(value => value.Id == statistic.UnitId);
                    if (unit != null && (unit.Id == 1 || unit.MasterId == 1))
                        damage = checked(damage + statistic.DamageAndShieldDamageDealt);
                }
                long bossMaxHp = checkpoint.Units.Where(value => value.Side != 1).Sum(value => value.MaxHp);
                if (!host.DailyChallenges.RecordDoubleShadowResult(now, challengeDurationHours, damage, bossMaxHp))
                    throw new InvalidOperationException("双影本地战斗结果无法保存。");
                if (won && !host.DailyChallenges.RecordDoubleShadowKill(challengeDurationHours, now,
                        challengeActivityId + ":" + checkpoint.RandomSeed,
                        OfficialDailyChallengeCatalog.LoadDefault().DailyKillLimit(challengeActivityId)))
                    throw new InvalidOperationException("双影击杀记录无法保存。");
            }
            else if (won) host.DailyChallenges.RecordDifficulty(challengeActivityId, challengeDifficulty + 1);

            if (won)
            {
                OfficialDailyChallengeCatalog catalog = OfficialDailyChallengeCatalog.LoadDefault();
                bool alreadyClaimed = host.DailyChallenges.DailyChallengeAwardClaimed(
                    challengeActivityId, now);
                ProgressionResult dailyAward = host.DailyChallenges.ClaimDailyChallengeAward(
                    challengeActivityId, catalog, host.Profile, now, challengeDurationHours);
                if (dailyAward.Status != ProgressionResultStatus.Applied
                    && dailyAward.Status != ProgressionResultStatus.Unchanged)
                    throw new InvalidOperationException("每日挑战奖励未能保存：" + dailyAward.Message);
                if (!alreadyClaimed && host.DailyChallenges.DailyChallengeAwardClaimed(
                        challengeActivityId, now))
                    grantedRewards = catalog.DailyChallengeAwards(challengeActivityId)
                        .Where(value => value.Id > 0 && value.Value > 0)
                        .Select(value => new OfficialChallengeReward
                        { ItemId = value.Id, Count = value.Value }).ToArray();
            }
            host.DailyChallenges.SaveBattleCheckpoint(challengeActivityId, string.Empty);
            return grantedRewards;
        }

        public void OnBattleHudControlClicked(string path)
        {
            if (path != null && path.IndexOf("Exit", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                string serialized = FightBattleCheckpointSerializer.Save(controller.Checkpoint);
                if (challengeMode) host.DailyChallenges.SaveBattleCheckpoint(challengeActivityId, serialized);
                else if (!privateMapEntry) host.DailyDungeons.SaveBattleCheckpoint(serialized);
            }
        }

        public void OnBattleReportReady(FightBattleReport report) { }

        private bool SaveChallengeCheckpoint(string serialized)
        {
            host.DailyChallenges.SaveBattleCheckpoint(challengeActivityId, serialized);
            return string.Equals(host.DailyChallenges.ActiveBattleCheckpoint(challengeActivityId),
                serialized, StringComparison.Ordinal);
        }

        private bool IsBattleSpeedConditionMet(int conditionId)
        {
            OfficialConditionContext context = new OfficialConditionContext { Level = battleProfile.Save.Player.Level };
            foreach (int mapClassId in battleProfile.Save.Player.UnlockedMapClassIds ?? new List<int>())
                context.UnlockedMapClassIds.Add(mapClassId);
            return battleProgression.Catalog.EvaluateCondition(conditionId, context);
        }

        private void OnDestroy() => DisposeBattle();

        private void DisposeBattle()
        {
            if (controller != null)
            {
                controller.DisposeSession();
                Destroy(controller);
                controller = null;
            }
            if (playerTransform != null)
            {
                playerTransform.SetParent(previousPlayerParent, false);
                playerTransform.localPosition = previousPlayerPosition;
                playerTransform.localRotation = previousPlayerRotation;
                playerTransform.localScale = previousPlayerScale;
                playerTransform = null;
            }
            if (battleRoot != null)
            {
                Destroy(battleRoot.gameObject);
                battleRoot = null;
            }
        }

        private FightBattleCheckpoint BuildCheckpoint(DailyDungeonRunSave run,
            DailyDungeonRuntimeStage stage, DailyDungeonRuntimeBattleScene scene,
            OfficialCombatAssetCatalog combatAssets, FightOfficialCombatCatalog combat,
            int[] assistNpcClassIds = null,
            IEnumerable<DailyDungeonRuntimeFormation> additionalFormations = null)
        {
            if (scene.gridWidth <= 0 || scene.gridHeight <= 0 || scene.cells == null)
                throw new InvalidOperationException("Official daily dungeon battle grid is malformed.");
            OfficialBattleGridBuild gridBuild = OfficialBattleGridAssembler.Build(scene.gridWidth,
                scene.gridHeight,
                scene.gridOrigin != null && scene.gridOrigin.Length > 0 ? scene.gridOrigin[0] : 0f,
                scene.gridOrigin != null && scene.gridOrigin.Length > 1 ? scene.gridOrigin[1] : 0f,
                scene.gridOrigin != null && scene.gridOrigin.Length > 2 ? scene.gridOrigin[2] : 0f,
                scene.cells.Select(cell => new FightBattleGridCell
                { X = cell.x, Y = cell.y, GroundType = cell.groundType, Height = cell.height }), "daily dungeon");
            FightBattleGrid grid = gridBuild.Grid;
            int moveMask = gridBuild.MoveMask;
            DailyDungeonRuntimeBattleFaction playerFaction = scene.factions?.FirstOrDefault(value => value.id == 0);
            int[] playerEntry = EntryPosition(playerFaction);
            if (playerEntry == null)
                throw new InvalidOperationException("Official daily dungeon scene has no player entry position.");
            OfflineCharacterProgressionService progression = battleProgression;
            OfflinePlayerProgress playerSave = battleProfile.Save.Player;
            FightOfficialSkillItemCatalog skillItems = combatAssets.LoadSkillItems();
            FightDamageFormula formula = combatAssets.LoadDamageFormula();
            FightStageStatusCatalog stageStatuses = combatAssets.LoadStageStatusCatalog();
            var checkpoint = OfficialBattleCheckpointAssembler.Create(new OfficialBattleCheckpointOptions
            {
                StageId = stage.id,
                StageType = 11, // Common.StageType.DailyDungeon
                MaxRound = stage.maxRound > 0 ? stage.maxRound : 25,
                IsPlayerRoundLimit = stage.isPlayerRoundLimit,
                PlayerRoundLimit = stage.playerRoundLimit,
                RandomSeed = StableSeed(run.id, stage.id),
                Grid = grid,
                StageStatuses = stageStatuses,
                StageStatusClassId = stageStatuses.ClassIdOfStage(stage.id)
            });
            var playerProperties = new Dictionary<int, long>(progression.PropertyCalculator.Evaluate(
                playerSave, battleProfile.Save.World, DateTimeOffset.Now.ToUnixTimeSeconds()).OutOfFightBattleProps);
            OfficialBattlePassivePropertyAssembler.ApplyEquippedPlayerPassives(combat, skillItems,
                playerProperties, playerSave, progression);
            int maxHpId = combat.PropertyType("MaxHp");
            int speedId = combat.PropertyType("Speed");
            var player = new FightBattleUnitState
            {
                Id = 1, Side = 1, CharacterType = 1, Level = playerSave.Level,
                SubRank = playerSave.SubRank, Profession = playerSave.Wardrobe.Profession,
                X = playerEntry[0], Y = playerEntry[1], MoveMask = moveMask
            };
            OfficialBattleUnitAssembler.Initialize(formula, player, playerProperties, maxHpId, speedId);
            checkpoint.Units.Add(player);
            AssemblePlayer(combat, skillItems, player, playerSave);
            OfficialBattlePetEnergyAssembler.TryConfigure(player, playerSave,
                progression.Catalog, combat, skillItems);

            Dictionary<int, DailyDungeonRuntimeFormation> formations = battleCatalog.formations
                .ToDictionary(value => value.id);
            foreach (DailyDungeonRuntimeFormation formation in additionalFormations
                         ?? Array.Empty<DailyDungeonRuntimeFormation>())
                if (formation != null && !formations.ContainsKey(formation.id))
                    formations.Add(formation.id, formation);
            int[][] waves = WaveMonsterIds(stage);
            for (int waveIndex = 0; waveIndex < waves.Length; waveIndex++)
            {
                int[] monsterIds = waves[waveIndex] ?? Array.Empty<int>();
                if (monsterIds.Length == 0) continue;
                int arrayId = stage.waveArrayIds != null && waveIndex < stage.waveArrayIds.Length
                    ? stage.waveArrayIds[waveIndex] : 0;
                if (!formations.TryGetValue(arrayId, out DailyDungeonRuntimeFormation formation))
                    throw new InvalidOperationException("Official daily dungeon formation is unavailable: " + arrayId);
                float[] waveOrigin = WaveOrigin(scene, waveIndex);
                var wave = new FightBattleWaveState { Index = checkpoint.Waves.Count, Spawned = checkpoint.Waves.Count == 0 };
                int groupIndex = -1;
                int previousGroupId = int.MinValue;
                foreach (DailyDungeonRuntimeFormationMonster source in formation.monsters)
                {
                    if (source.groupId != previousGroupId) { groupIndex++; previousGroupId = source.groupId; }
                    if (groupIndex < 0 || groupIndex >= monsterIds.Length)
                        throw new InvalidOperationException("Official dungeon formation does not match its wave monsters.");
                    int monsterId = monsterIds[groupIndex];
                    int level = Mathf.RoundToInt(stage.level + stage.monsterLevelAdd + source.levelAdd);
                    Dictionary<int, long> properties = new Dictionary<int, long>(combat.CalculateMonsterProps(
                        monsterId, stage.monsterGroupNo, level, stage.subRank));
                    ApplyDailyBossDifficulty(properties, stage.subRank, level);
                    int[] passiveItems = combat.MonsterPassiveSkillItems(monsterId, stage.monsterGroupNo).ToArray();
                    OfficialBattlePassivePropertyAssembler.Apply(combat, skillItems, properties,
                        passiveItems.Select(itemId => new FightOfficialPassiveSkillBinding
                        { ItemId = itemId, Rank = 1, Level = Math.Max(1, level), SubRank = stage.subRank }));
                    var unit = new FightBattleUnitState
                    {
                        Id = checkpoint.Units.Count + 1, Side = 2, CharacterType = 2,
                        MonsterId = monsterId, MonsterGroupNo = stage.monsterGroupNo,
                        MonsterType = source.monsterType,
                        MaxHpPlies = combat.MonsterHpPliesFor(monsterId, stage.monsterGroupNo, level),
                        Level = level,
                        SubRank = stage.subRank, Wave = wave.Index,
                        X = Mathf.RoundToInt(waveOrigin[0]) + source.x,
                        Y = Mathf.RoundToInt(waveOrigin[1]) + source.y,
                        MoveMask = moveMask, Direction = source.flipX ? 1 : 0
                    };
                    OfficialBattleUnitAssembler.Initialize(formula, unit, properties, maxHpId, speedId);
                    checkpoint.Units.Add(unit);
                    wave.UnitIds.Add(unit.Id);
                    AssembleMonster(combat, skillItems, unit, monsterId, stage.monsterGroupNo);
                }
                checkpoint.Waves.Add(wave);
            }
            if (checkpoint.Waves.Count == 0) throw new InvalidOperationException("The selected dungeon room has no battle waves.");
            AddAssistNpcs(checkpoint, scene, playerFaction, moveMask, playerSave,
                assistNpcClassIds ?? Array.Empty<int>());
            return checkpoint;
        }

        private static int[] EntryPosition(DailyDungeonRuntimeBattleFaction faction)
        {
            if (faction?.entryPosition != null && faction.entryPosition.Length >= 2)
                return faction.entryPosition;
            return EntryPositions(faction).FirstOrDefault();
        }

        private static IEnumerable<int[]> EntryPositions(DailyDungeonRuntimeBattleFaction faction)
        {
            int[][] nested = faction?.entryPositions;
            if (nested != null && nested.Any(value => value != null && value.Length >= 2))
                return nested.Where(value => value != null && value.Length >= 2);
            int[] flat = faction?.entryPositionsFlat;
            if (flat == null || flat.Length < 2) return Array.Empty<int[]>();
            return Enumerable.Range(0, flat.Length / 2)
                .Select(index => new[] { flat[index * 2], flat[index * 2 + 1] });
        }

        private static int[][] WaveMonsterIds(DailyDungeonRuntimeStage stage)
        {
            if (stage?.waveMonsterIds != null && stage.waveMonsterIds.Length > 0)
                return stage.waveMonsterIds;
            if (stage?.waveMonsterOffsets == null || stage.waveMonsterOffsets.Length < 2
                || stage.waveMonsterIdsFlat == null) return Array.Empty<int[]>();
            int waveCount = stage.waveMonsterOffsets.Length - 1;
            var waves = new int[waveCount][];
            for (int index = 0; index < waveCount; index++)
            {
                int start = stage.waveMonsterOffsets[index];
                int end = stage.waveMonsterOffsets[index + 1];
                if (start < 0 || end < start || end > stage.waveMonsterIdsFlat.Length)
                    throw new InvalidOperationException("Official daily dungeon wave offsets are malformed: " + stage.id);
                waves[index] = new int[end - start];
                Array.Copy(stage.waveMonsterIdsFlat, start, waves[index], 0, end - start);
            }
            return waves;
        }

        private static float[] WaveOrigin(DailyDungeonRuntimeBattleScene scene, int waveIndex)
        {
            if (scene?.waveOriginOffsets != null && scene.waveOriginsFlat != null
                && waveIndex + 1 < scene.waveOriginOffsets.Length)
            {
                int start = scene.waveOriginOffsets[waveIndex];
                int end = scene.waveOriginOffsets[waveIndex + 1];
                if (start < 0 || end - start != 2 || end > scene.waveOriginsFlat.Length)
                    throw new InvalidOperationException("Official daily dungeon wave origin offsets are malformed.");
                return new[] { scene.waveOriginsFlat[start], scene.waveOriginsFlat[start + 1] };
            }
            if (scene?.waveOrigins != null && waveIndex < scene.waveOrigins.Length
                && scene.waveOrigins[waveIndex] != null && scene.waveOrigins[waveIndex].Length >= 2)
                return scene.waveOrigins[waveIndex];
            return new[] { 0f, 0f };
        }

        private int[] ResolveDailyChallengeAssistSelection(FightOfficialNpcAssistanceCatalog npcCatalog,
            FightOfficialNpcInviteRule inviteRule, string entryId)
        {
            int maxPlayer = ResolveAssistMaxPlayer(inviteRule);
            int[] selected = host.NpcAssistanceProgress.Selected(assistPlayType, entryId).ToArray();
            if (selected.Length > maxPlayer)
                throw new InvalidOperationException("助战伙伴数量超过官方上限。");
            var friendships = (host.Profile.Save.Player.CharacterProperties.NpcFriendships
                    ?? new List<OfflineNpcFriendship>())
                .Where(value => value != null && value.NpcClassId > 0)
                .ToDictionary(value => value.NpcClassId, value => value.Level);
            long now = DateTimeOffset.Now.ToUnixTimeSeconds();
            NpcAssistRefreshCycle cycle = (NpcAssistRefreshCycle)inviteRule.RefreshCycle;
            OfficialBattleModelCatalog battleModels = OfficialBattleModelCatalog.LoadDefault();
            foreach (int npcId in selected)
            {
                if (!battleModels.HasNpcBattleAnimations(npcId))
                    throw new InvalidOperationException("该助战伙伴没有完整的官方战斗模型与动画：" + npcId);
                bool conditionMet = npcCatalog.AssistConditionMet(npcId,
                    host.Profile.Save.Player.Level, friendships, host.Profile.StoryCount);
                if (!npcCatalog.CanInviteNpc(npcId, friendships.ContainsKey(npcId), conditionMet))
                    throw new InvalidOperationException("所选助战伙伴当前不满足官方条件：" + npcId);
                if (inviteRule.OneNpcMaxInviteCount > 0
                    && host.NpcAssistanceProgress.AssistCount(assistPlayType, npcId, cycle, now)
                        >= inviteRule.OneNpcMaxInviteCount)
                    throw new InvalidOperationException("所选助战伙伴今日次数已用完：" + npcId);
            }
            return selected;
        }

        private void AddAssistNpcs(FightBattleCheckpoint checkpoint,
            DailyDungeonRuntimeBattleScene scene, DailyDungeonRuntimeBattleFaction playerFaction,
            int moveMask, OfflinePlayerProgress playerSave, IReadOnlyList<int> npcClassIds)
        {
            if (npcClassIds == null || npcClassIds.Count == 0) return;
            var occupied = new HashSet<FightGridPoint>(checkpoint.Units
                .Select(unit => new FightGridPoint(unit.X, unit.Y)));
            int[] firstEntry = EntryPosition(playerFaction);
            if (firstEntry == null) throw new InvalidOperationException("Official daily dungeon scene has no player entry position.");
            int startX = firstEntry[0];
            int startY = firstEntry[1];
            var entryPoints = EntryPositions(playerFaction)
                .Select(value => new FightGridPoint(value[0], value[1]))
                .Distinct().ToList();
            if (entryPoints.Count == 0) entryPoints.Add(new FightGridPoint(firstEntry[0], firstEntry[1]));
            var cells = (scene.cells ?? Array.Empty<DailyDungeonRuntimeBattleCell>())
                .Where(cell => cell != null)
                .Select(cell => new FightGridPoint(cell.x, cell.y))
                .Distinct()
                .OrderBy(point => entryPoints.Min(entry => Math.Abs(point.X - entry.X) + Math.Abs(point.Y - entry.Y)))
                .ThenBy(point => Math.Abs(point.X - startX) + Math.Abs(point.Y - startY))
                .ThenBy(point => point.Y).ThenBy(point => point.X).ToList();
            var positions = new List<FightGridPoint>(npcClassIds.Count);
            foreach (FightGridPoint entry in entryPoints.Skip(1))
                if (checkpoint.Grid.CanStand(entry, moveMask, occupied))
                {
                    positions.Add(entry);
                    occupied.Add(entry);
                    if (positions.Count == npcClassIds.Count) break;
                }
            foreach (FightGridPoint point in cells)
            {
                if (positions.Count == npcClassIds.Count) break;
                if (!checkpoint.Grid.CanStand(point, moveMask, occupied)) continue;
                positions.Add(point);
                occupied.Add(point);
            }
            if (positions.Count != npcClassIds.Count)
                throw new InvalidOperationException("挑战场景没有足够的官方可行走助战位置。");

            FightOfficialNpcAssistanceCatalog npcCatalog = host.NpcAssistanceCatalog;
            for (int index = 0; index < npcClassIds.Count; index++)
            {
                FightGridPoint position = positions[index];
                FightBattleUnitState unit = npcCatalog.CreateUnit(checkpoint.Units.Count + 1,
                    1, Math.Max(1, playerSave.Level), Math.Max(0, playerSave.Rank),
                    Math.Max(0, playerSave.SubRank), position.X, position.Y, moveMask,
                    npcClassIds[index]);
                checkpoint.Units.Add(unit);
            }
        }

        private void FinishBattleAssist(int stageId)
        {
            OfflineNpcAssistBattle active = host.NpcAssistanceProgress.ActiveBattle;
            if (active != null && active.playType == assistPlayType && active.entryId == dailyAssistEntryId
                && active.stageId == stageId)
                host.NpcAssistanceProgress.FinishBattle(assistPlayType, dailyAssistEntryId, stageId);
        }

        private void ConfigureAssist(string defaultEntryId, OfficialBattleAssistOptions options)
        {
            OfficialBattleAssistOptions resolved = options == null
                ? new OfficialBattleAssistOptions()
                : options.Clone();
            resolved.Validate();
            assistPlayType = resolved.PlayType;
            assistMaxPlayerOverride = resolved.MaxPlayerOverride;
            dailyAssistEntryId = string.IsNullOrWhiteSpace(resolved.EntryId)
                ? defaultEntryId : resolved.EntryId;
            if (string.IsNullOrWhiteSpace(dailyAssistEntryId))
                throw new InvalidOperationException("Battle assist entry identity is required.");
        }

        private int ResolveAssistMaxPlayer(FightOfficialNpcInviteRule inviteRule)
        {
            int officialMax = Math.Max(0, inviteRule.MaxPlayer);
            return assistMaxPlayerOverride > 0
                ? Math.Min(assistMaxPlayerOverride, officialMax)
                : Math.Min(3, officialMax);
        }

        private void AssemblePlayer(FightOfficialCombatCatalog combat,
            FightOfficialSkillItemCatalog skillItems, FightBattleUnitState player, OfflinePlayerProgress save)
        {
            int[] equipped = OfflineSkillProgression.EquippedActiveSkills(save.Skills);
            if (!equipped.Any(value => value > 0))
            {
                int initial = skillItems.InitialActiveSkill(save.InitialWeaponType);
                if (initial > 0) equipped = new[] { initial, 0, 0, 0 };
            }
            var properties = OfficialBattlePropertyAssembler.ToMap(player.Properties);
            int weaponItemId = OfflineEquipmentOperations.EquippedItemIdAtPosition(save.Equipment, 10);
            int currentWeaponType = skillItems.ItemTypeFor(weaponItemId);
            var skills = new List<FightOfficialSkillBinding>();
            for (int slot = 0; slot < equipped.Length; slot++)
            {
                int itemId = equipped[slot];
                if (itemId <= 0) continue;
                int skinId = save.Wardrobe?.SkillSkins?.FirstOrDefault(value => value.SkillId == itemId)?.SkillSkinId ?? 0;
                FightOfficialSkillItem item = skillItems.RequiredForSkin(itemId, skinId);
                int rank = Math.Max(1, OfflineSkillProgression.OwnedRank(save.Skills, itemId));
                int level = Math.Max(1, battleProgression.SkillProgression.SlotLevel(save.Skills, 100, slot,
                    battleProgression.SkillContext().CurrentSeasonId));
                skills.Add(new FightOfficialSkillBinding
                {
                    EntityId = item.EntityId, Slot = slot, ItemId = itemId, UseItemId = item.ItemId, SkillSkinId = skinId, Rank = rank,
                    Level = level,
                    ProfessionAvailability = skillItems.ProfessionAvailabilityFor(item.ItemId,
                        player.Profession, currentWeaponType, save.InitialWeaponType),
                    SubRank = player.SubRank
                });
            }
            var passives = new List<FightOfficialPassiveSkillBinding>();
            int[] passiveItems = OfflineSkillProgression.EquippedPassiveSkills(save.Skills);
            for (int slot = 0; slot < passiveItems.Length; slot++)
            {
                int itemId = passiveItems[slot];
                if (itemId <= 0) continue;
                passives.Add(new FightOfficialPassiveSkillBinding
                {
                    ItemId = itemId,
                    Rank = Math.Max(1, OfflineSkillProgression.OwnedRank(save.Skills, itemId)),
                    Level = Math.Max(1, battleProgression.SkillProgression.SlotLevel(save.Skills, 101, slot,
                        battleProgression.SkillContext().CurrentSeasonId)),
                    SubRank = player.SubRank
                });
            }
            passives.AddRange(OfficialEquipmentBattlePassives.Collect(battleProgression, save, player.SubRank));
            OfficialBattleSkillAssembler.AssembleUnit(combat, skillItems, player, properties, skills, passives);
        }

        private static void AssembleMonster(FightOfficialCombatCatalog combat,
            FightOfficialSkillItemCatalog skillItems, FightBattleUnitState unit, int monsterId, int groupNo)
        {
            IReadOnlyDictionary<int, long> properties = OfficialBattlePropertyAssembler.ToMap(unit.Properties);
            var skills = new List<FightOfficialSkillBinding>();
            int slot = 0;
            foreach (int itemId in combat.MonsterActiveSkillItems(monsterId, groupNo))
            {
                FightOfficialSkillItem item = skillItems.Required(itemId);
                if (item.EntityId <= 0) continue;
                skills.Add(new FightOfficialSkillBinding
                {
                    EntityId = item.EntityId, Slot = slot++, ItemId = itemId,
                    Rank = 1, Level = Math.Max(1, unit.Level), SubRank = unit.SubRank
                });
            }
            var passives = combat.MonsterPassiveSkillItems(monsterId, groupNo)
                .Where(value => value > 0).Select(value => new FightOfficialPassiveSkillBinding
                { ItemId = value, Rank = 1, Level = Math.Max(1, unit.Level), SubRank = unit.SubRank }).ToList();
            OfficialBattleSkillAssembler.AssembleUnit(combat, skillItems, unit, properties, skills, passives);
        }

        private void ApplyDailyBossDifficulty(Dictionary<int, long> properties, int rank, int level)
        {
            if (!challengeMode || challengeActivityId == OfflineDailyChallengeService.FunnyBossActivityId) return;
            OfficialDailyChallengeCatalog catalog = OfficialDailyChallengeCatalog.LoadDefault();
            OfficialDomainBossDifficulty difficulty = catalog.Difficulty(challengeActivityId, challengeDifficulty);
            if (difficulty == null)
                throw new InvalidOperationException("Official daily boss difficulty is missing: "
                    + challengeActivityId + "/" + challengeDifficulty);
            var additions = new Dictionary<int, long>();
            foreach (OfficialChallengeProperty correction in catalog.BossCorrection(rank, level))
                if (correction.Id > 0) additions[correction.Id] = correction.Value;
            foreach (OfficialChallengeProperty property in difficulty.DifficultyPropEntries
                         ?? Array.Empty<OfficialChallengeProperty>())
            {
                if (property.Id <= 0) continue;
                if (additions.TryGetValue(property.Id, out long correction))
                    additions[property.Id] = (long)((float)(correction * property.Value) / 10000f);
                else additions.Add(property.Id, property.Value);
            }
            foreach (KeyValuePair<int, long> addition in additions)
                properties[addition.Key] = properties.TryGetValue(addition.Key, out long current)
                    ? checked(current + addition.Value) : addition.Value;
        }

        private static int StableSeed(string runId, int stageId)
        {
            unchecked
            {
                uint value = 2166136261;
                foreach (char character in runId) value = (value ^ character) * 16777619;
                value = (value ^ (uint)stageId) * 16777619;
                int seed = (int)(value % (FightRandomStream.Modulus - 1)) + 1;
                return seed;
            }
        }
    }
}
