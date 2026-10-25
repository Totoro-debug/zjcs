using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Offline.World;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed class DailyDungeonBattleRunner : MonoBehaviour
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
        private float stepTimer;
        private bool resultHandled;
        private int roomId;
        private string dailyAssistEntryId;
        private bool challengeMode;
        private int challengeActivityId;
        private int challengeDurationHours;
        private int challengeDifficulty;

        public void Begin(HomeFeatureRuntime runtime, int selectedRoomId, string assistEntryId,
            Action<bool> onCompleted)
        {
            host = runtime ?? throw new ArgumentNullException(nameof(runtime));
            completed = onCompleted;
            if (string.IsNullOrWhiteSpace(assistEntryId))
                throw new ArgumentException("Daily dungeon assistance entry identity is required.", nameof(assistEntryId));
            dailyAssistEntryId = assistEntryId;
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
            FightOfficialNpcInviteRule inviteRule = npcCatalog.InviteRule(10);
            OfflineNpcAssistBattle activeAssist = host.NpcAssistanceProgress.ActiveBattle;
            if (activeAssist != null && (activeAssist.playType != 10
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
                if (activeAssist == null && selectedNpcIds.Length > 0
                    && !ResolveDailyChallengeAssistSelection(npcCatalog, inviteRule, assistEntryId)
                        .SequenceEqual(selectedNpcIds))
                    throw new InvalidOperationException("日常副本恢复的伙伴与已保存的选择不一致。");
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
                if (selectedNpcIds.Length > 0 && activeAssist == null)
                {
                    host.NpcAssistanceProgress.CommitBattle(10, assistEntryId, stage.id,
                        Math.Min(3, inviteRule.MaxPlayer), inviteRule.OneNpcMaxInviteCount,
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
                if (committedAssist) host.NpcAssistanceProgress.CancelBattle(10, assistEntryId, stage.id);
                DisposeBattle();
                throw;
            }
        }

        public void BeginChallenge(HomeFeatureRuntime runtime, int activityId, int stageId,
            int stageType, int durationHours, int dailyLimit, int difficulty, Action<bool> onCompleted)
        {
            host = runtime ?? throw new ArgumentNullException(nameof(runtime));
            completed = onCompleted;
            challengeMode = true;
            challengeActivityId = activityId;
            challengeDurationHours = durationHours;
            host.DailyChallenges.EnterFeature(DateTime.Now);
            string savedCheckpoint = host.DailyChallenges.ActiveBattleCheckpoint(activityId);
            bool restore = !string.IsNullOrEmpty(savedCheckpoint);
            challengeDifficulty = restore
                ? host.DailyChallenges.ActiveChallengeDifficulty(activityId) : difficulty;
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
            FightOfficialNpcInviteRule inviteRule = npcCatalog.InviteRule(10);
            string assistEntryId = "daily-challenge:" + activityId;
            OfflineNpcAssistBattle activeAssistBattle = host.NpcAssistanceProgress.ActiveBattle;
            if (activeAssistBattle != null && !restore
                && (activeAssistBattle.playType != 10 || activeAssistBattle.entryId != assistEntryId
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
            if (checkpoint.Finished)
            {
                FinishDailyChallengeAssist(activityId, stageId);
                IReadOnlyList<OfficialChallengeReward> rewards = FinalizeChallenge(checkpoint,
                    checkpoint.Outcome.Won);
                host.Hud.OpenDailyChallengeResult(activityId, checkpoint.Outcome.Won, checkpoint, rewards);
                completed?.Invoke(checkpoint.Outcome.Won);
                Destroy(gameObject);
                return;
            }
            checkpoint.StageType = stageType;
            MountBattle(checkpoint, scene, combatAssets, restore, true);
            string checkpointJson = FightBattleCheckpointSerializer.Save(controller.Checkpoint);
            bool committedAssist = false;
            try
            {
                if (!restore)
                {
                    host.NpcAssistanceProgress.CommitBattle(10, assistEntryId, stageId,
                        Math.Min(3, inviteRule.MaxPlayer), inviteRule.OneNpcMaxInviteCount,
                        inviteRule.MaxInviteCount, (NpcAssistRefreshCycle)inviteRule.RefreshCycle,
                        DateTimeOffset.Now.ToUnixTimeSeconds());
                    committedAssist = true;
                }
                bool recorded = restore
                    ? SaveChallengeCheckpoint(checkpointJson)
                    : host.DailyChallenges.RecordParticipation(activityId, DateTime.Now,
                        durationHours, dailyLimit, challengeDifficulty, checkpointJson);
                if (!recorded) throw new InvalidOperationException("挑战回执或战斗进度无法保存。");
            }
            catch
            {
                if (committedAssist) host.NpcAssistanceProgress.CancelBattle(10, assistEntryId, stageId);
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
            playerTransform = host.HomePlayerTransform;
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
            controller.HudControlClicked += OnHudControlClicked;
            controller.StepCompleted += OnStepCompleted;
            var gridMapper = new OfficialBattleGridMapper(battleRoot, checkpoint.Grid);
            OfficialBattleHudCatalog hudCatalog = OfficialBattleHudCatalog.LoadDefault();
            Sprite fallback = hudCatalog.FindSprite("C_none-fa3e084e40.png");
            if (fallback == null) throw new InvalidOperationException("Official battle fallback portrait is missing.");
            OfficialCombatSkillIconCatalog skillIcons = OfficialCombatSkillIconCatalog.TryLoadDefault();
            OfficialBattleMonsterPortraitCatalog portraits = OfficialBattleMonsterPortraitCatalog.TryLoadDefault();
            OfficialBattleModelCatalog models = OfficialBattleModelCatalog.LoadDefault();
            controller.Begin(checkpoint, battleRoot, host.Hud.CanvasTransform, gridMapper,
                itemId => skillIcons == null ? fallback : skillIcons.Find(itemId) ?? fallback,
                unit => unit.MonsterId > 0 && portraits != null
                    ? portraits.Find(combat.MonsterIconId(unit.MonsterId)) : fallback,
                unit => unit.Id == 1 ? null : models.ResolveModelKey(unit),
                (_, __) => "idle_01",
                (root, unit) =>
                {
                    Vector3 scale = root.localScale;
                    scale.x = Mathf.Abs(scale.x) * (unit.Direction == 1 ? -1f : 1f);
                    root.localScale = scale;
                },
                null, null, hudCatalog, restore);
            controller.AttachUnitTransform(1, playerTransform);
            controller.ConfigureSpeeds(combatAssets.Load().SpeedSettings, 0, IsBattleSpeedConditionMet);
            string serialized = FightBattleCheckpointSerializer.Save(checkpoint);
            if (!isChallenge && !host.DailyDungeons.SaveBattleCheckpoint(serialized))
                throw new InvalidOperationException("Daily dungeon battle checkpoint could not be saved.");
            stepTimer = 0.8f;
        }

        private void Update()
        {
            if (controller == null || controller.Checkpoint == null || resultHandled) return;
            FightBattleCheckpoint checkpoint = controller.Checkpoint;
            if (checkpoint.Finished)
            {
                CompleteBattle(checkpoint.Outcome.Won);
                return;
            }
            stepTimer -= Time.deltaTime;
            if (stepTimer > 0f) return;
            controller.Advance();
            stepTimer = controller.LastStepSeconds;
        }

        private void OnStepCompleted(FightBattleExecutionResult result)
        {
            if (result?.Checkpoint == null) return;
            string serialized = FightBattleCheckpointSerializer.Save(result.Checkpoint);
            if (challengeMode) host.DailyChallenges.SaveBattleCheckpoint(challengeActivityId, serialized);
            else if (!host.DailyDungeons.SaveBattleCheckpoint(serialized))
                throw new InvalidOperationException("Daily dungeon battle checkpoint could not be saved.");
            if (result.Checkpoint.Finished) CompleteBattle(result.Checkpoint.Outcome.Won);
        }

        private void CompleteBattle(bool won)
        {
            if (resultHandled) return;
            resultHandled = true;
            FightBattleCheckpoint checkpoint = controller.Checkpoint;
            if (challengeMode) FinishDailyChallengeAssist(challengeActivityId, checkpoint.StageId);
            else FinishDailyDungeonAssist(checkpoint.StageId);
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

        private void OnHudControlClicked(string path)
        {
            if (path != null && path.IndexOf("Exit", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                string serialized = FightBattleCheckpointSerializer.Save(controller.Checkpoint);
                if (challengeMode) host.DailyChallenges.SaveBattleCheckpoint(challengeActivityId, serialized);
                else host.DailyDungeons.SaveBattleCheckpoint(serialized);
            }
        }

        private bool SaveChallengeCheckpoint(string serialized)
        {
            host.DailyChallenges.SaveBattleCheckpoint(challengeActivityId, serialized);
            return string.Equals(host.DailyChallenges.ActiveBattleCheckpoint(challengeActivityId),
                serialized, StringComparison.Ordinal);
        }

        private bool IsBattleSpeedConditionMet(int conditionId)
        {
            OfficialConditionContext context = new OfficialConditionContext { Level = host.Level };
            foreach (int mapClassId in host.Profile.Save.Player.UnlockedMapClassIds ?? new List<int>())
                context.UnlockedMapClassIds.Add(mapClassId);
            return host.DailyDungeonProgression.Catalog.EvaluateCondition(conditionId, context);
        }

        private void OnDestroy() => DisposeBattle();

        private void DisposeBattle()
        {
            if (controller != null)
            {
                controller.StepCompleted -= OnStepCompleted;
                controller.HudControlClicked -= OnHudControlClicked;
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
            var grid = new FightBattleGrid
            {
                Width = scene.gridWidth,
                Height = scene.gridHeight,
                OriginX = scene.gridOrigin != null && scene.gridOrigin.Length > 0 ? scene.gridOrigin[0] : 0f,
                OriginY = scene.gridOrigin != null && scene.gridOrigin.Length > 1 ? scene.gridOrigin[1] : 0f,
                OriginZ = scene.gridOrigin != null && scene.gridOrigin.Length > 2 ? scene.gridOrigin[2] : 0f,
                DefaultGroundType = 2
            };
            int moveMask = 0;
            foreach (DailyDungeonRuntimeBattleCell cell in scene.cells)
            {
                grid.Cells.Add(new FightBattleGridCell
                { X = cell.x, Y = cell.y, GroundType = cell.groundType, Height = cell.height });
                if (cell.height >= 999) grid.BlockedPositions.Add(new FightGridPoint(cell.x, cell.y));
                else if (cell.groundType >= 0 && cell.groundType < 31) moveMask |= 1 << cell.groundType;
            }
            if (moveMask == 0) throw new InvalidOperationException("Official daily dungeon grid has no walkable cells.");
            DailyDungeonRuntimeBattleFaction playerFaction = scene.factions?.FirstOrDefault(value => value.id == 0);
            if (playerFaction?.entryPositions == null || playerFaction.entryPositions.Length == 0
                || playerFaction.entryPositions[0].Length < 2)
                throw new InvalidOperationException("Official daily dungeon scene has no player entry position.");
            OfflineCharacterProgressionService progression = host.DailyDungeonProgression;
            OfflinePlayerProgress playerSave = host.Profile.Save.Player;
            FightOfficialSkillItemCatalog skillItems = combatAssets.LoadSkillItems();
            FightDamageFormula formula = combatAssets.LoadDamageFormula();
            var checkpoint = new FightBattleCheckpoint
            {
                StageId = stage.id,
                StageType = 11, // Common.StageType.DailyDungeon
                IsPvp = false,
                MaxRound = stage.maxRound > 0 ? stage.maxRound : 25,
                IsPlayerRoundLimit = stage.isPlayerRoundLimit,
                PlayerRoundLimit = stage.playerRoundLimit,
                RandomSeed = StableSeed(run.id, stage.id),
                Grid = grid,
                StageStatus = combatAssets.LoadStageStatusCatalog().CreateCheckpointConfig(
                    combatAssets.LoadStageStatusCatalog().ClassIdOfStage(stage.id))
            };
            var playerProperties = new Dictionary<int, long>(progression.PropertyCalculator.Evaluate(
                playerSave, host.Profile.Save.World, DateTimeOffset.Now.ToUnixTimeSeconds()).OutOfFightBattleProps);
            int[] playerPassiveItems = OfflineSkillProgression.EquippedPassiveSkills(playerSave.Skills);
            ApplyPassiveProperties(combat, skillItems, playerProperties, playerPassiveItems,
                itemId => OfflineSkillProgression.OwnedRank(playerSave.Skills, itemId),
                itemId =>
                {
                    int slot = Array.IndexOf(playerPassiveItems, itemId);
                    return playerSave.Skills.PassiveSlotLevels != null && slot >= 0
                        && slot < playerSave.Skills.PassiveSlotLevels.Length
                        ? Math.Max(1, playerSave.Skills.PassiveSlotLevels[slot]) : 1;
                },
                playerSave.SubRank);
            int maxHpId = combat.PropertyType("MaxHp");
            int speedId = combat.PropertyType("Speed");
            var player = new FightBattleUnitState
            {
                Id = 1, Side = 1, CharacterType = 1, Level = playerSave.Level,
                SubRank = playerSave.SubRank, Profession = playerSave.Wardrobe.Profession,
                X = playerFaction.entryPositions[0][0], Y = playerFaction.entryPositions[0][1],
                Hp = Property(playerProperties, maxHpId), MaxHp = Property(playerProperties, maxHpId),
                LastMaxHp = Property(playerProperties, maxHpId), MoveMask = moveMask,
                Properties = ToProperties(playerProperties)
            };
            if (player.MaxHp <= 0) throw new InvalidOperationException("Official character battle MaxHp is invalid.");
            player.MoveDist = formula.CalculateMoveDist(playerProperties);
            player.Turn.Id = player.Id;
            player.Turn.Hp = player.Hp;
            player.Turn.Speed = CheckedProperty(playerProperties, speedId);
            checkpoint.Units.Add(player);
            AssemblePlayer(combat, skillItems, player, playerSave);

            Dictionary<int, DailyDungeonRuntimeFormation> formations = host.DailyDungeons.Catalog.formations
                .ToDictionary(value => value.id);
            foreach (DailyDungeonRuntimeFormation formation in additionalFormations
                         ?? Array.Empty<DailyDungeonRuntimeFormation>())
                if (formation != null && !formations.ContainsKey(formation.id))
                    formations.Add(formation.id, formation);
            for (int waveIndex = 0; waveIndex < stage.waveMonsterIds.Length; waveIndex++)
            {
                int[] monsterIds = stage.waveMonsterIds[waveIndex] ?? Array.Empty<int>();
                if (monsterIds.Length == 0) continue;
                int arrayId = stage.waveArrayIds != null && waveIndex < stage.waveArrayIds.Length
                    ? stage.waveArrayIds[waveIndex] : 0;
                if (!formations.TryGetValue(arrayId, out DailyDungeonRuntimeFormation formation))
                    throw new InvalidOperationException("Official daily dungeon formation is unavailable: " + arrayId);
                float[] waveOrigin = scene.waveOrigins != null && waveIndex < scene.waveOrigins.Length
                    ? scene.waveOrigins[waveIndex] : new[] { 0f, 0f };
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
                    ApplyPassiveProperties(combat, skillItems, properties, passiveItems,
                        _ => 1, _ => Math.Max(1, level), stage.subRank);
                    long monsterHp = Property(properties, maxHpId);
                    if (monsterHp <= 0) throw new InvalidOperationException("Official daily dungeon monster MaxHp is invalid.");
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
                        Hp = monsterHp, MaxHp = monsterHp, LastMaxHp = monsterHp,
                        MoveMask = moveMask, Direction = source.flipX ? 1 : 0,
                        Properties = ToProperties(properties)
                    };
                    unit.MoveDist = formula.CalculateMoveDist(properties);
                    unit.Turn.Id = unit.Id;
                    unit.Turn.Hp = unit.Hp;
                    unit.Turn.Speed = CheckedProperty(properties, speedId);
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

        private int[] ResolveDailyChallengeAssistSelection(FightOfficialNpcAssistanceCatalog npcCatalog,
            FightOfficialNpcInviteRule inviteRule, string entryId)
        {
            int maxPlayer = Math.Min(3, inviteRule.MaxPlayer);
            int[] selected = host.NpcAssistanceProgress.Selected(10, entryId).ToArray();
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
                    && host.NpcAssistanceProgress.AssistCount(10, npcId, cycle, now)
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
            int startX = playerFaction.entryPositions[0][0];
            int startY = playerFaction.entryPositions[0][1];
            var entryPoints = (playerFaction.entryPositions ?? Array.Empty<int[]>())
                .Where(value => value != null && value.Length >= 2)
                .Select(value => new FightGridPoint(value[0], value[1]))
                .Distinct().ToList();
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

        private void FinishDailyChallengeAssist(int activityId, int stageId)
        {
            OfflineNpcAssistBattle active = host.NpcAssistanceProgress.ActiveBattle;
            if (active == null) return;
            string entryId = "daily-challenge:" + activityId;
            if (active.playType == 10 && active.entryId == entryId && active.stageId == stageId)
                host.NpcAssistanceProgress.FinishBattle(10, entryId, stageId);
        }

        private void FinishDailyDungeonAssist(int stageId)
        {
            OfflineNpcAssistBattle active = host.NpcAssistanceProgress.ActiveBattle;
            if (active != null && active.playType == 10 && active.entryId == dailyAssistEntryId
                && active.stageId == stageId)
                host.NpcAssistanceProgress.FinishBattle(10, dailyAssistEntryId, stageId);
        }

        private static void AssemblePlayer(FightOfficialCombatCatalog combat,
            FightOfficialSkillItemCatalog skillItems, FightBattleUnitState player, OfflinePlayerProgress save)
        {
            int[] equipped = OfflineSkillProgression.EquippedActiveSkills(save.Skills);
            if (!equipped.Any(value => value > 0))
            {
                int initial = skillItems.InitialActiveSkill(save.InitialWeaponType);
                if (initial > 0) equipped = new[] { initial, 0, 0, 0 };
            }
            var properties = PropertyMap(player.Properties);
            var skills = new List<FightOfficialSkillBinding>();
            for (int slot = 0; slot < equipped.Length; slot++)
            {
                int itemId = equipped[slot];
                if (itemId <= 0) continue;
                FightOfficialSkillItem item = skillItems.Required(itemId);
                int rank = Math.Max(1, OfflineSkillProgression.OwnedRank(save.Skills, itemId));
                int level = save.Skills.ActiveSlotLevels != null && slot < save.Skills.ActiveSlotLevels.Length
                    ? Math.Max(1, save.Skills.ActiveSlotLevels[slot]) : 1;
                skills.Add(new FightOfficialSkillBinding
                {
                    EntityId = item.EntityId, Slot = slot, ItemId = itemId, Rank = rank,
                    Level = level, SubRank = player.SubRank,
                    Cooldown = combat.CalculateSkillCooldown(item.EntityId, rank, level,
                        player.SubRank, properties),
                    Properties = ToProperties(combat.CalculateSkillProps(item.EntityId, rank, level, player.SubRank))
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
                    Level = save.Skills.PassiveSlotLevels != null && slot < save.Skills.PassiveSlotLevels.Length
                        ? Math.Max(1, save.Skills.PassiveSlotLevels[slot]) : 1,
                    SubRank = player.SubRank
                });
            }
            combat.AssembleUnit(player, skills, combat.CreatePassiveStatusBindings(player, skillItems, passives));
        }

        private static void AssembleMonster(FightOfficialCombatCatalog combat,
            FightOfficialSkillItemCatalog skillItems, FightBattleUnitState unit, int monsterId, int groupNo)
        {
            IReadOnlyDictionary<int, long> properties = PropertyMap(unit.Properties);
            var skills = new List<FightOfficialSkillBinding>();
            int slot = 0;
            foreach (int itemId in combat.MonsterActiveSkillItems(monsterId, groupNo))
            {
                FightOfficialSkillItem item = skillItems.Required(itemId);
                if (item.EntityId <= 0) continue;
                skills.Add(new FightOfficialSkillBinding
                {
                    EntityId = item.EntityId, Slot = slot++, ItemId = itemId,
                    Rank = 1, Level = Math.Max(1, unit.Level), SubRank = unit.SubRank,
                    Cooldown = combat.CalculateSkillCooldown(item.EntityId, 1,
                        Math.Max(1, unit.Level), unit.SubRank, properties),
                    Properties = ToProperties(combat.CalculateSkillProps(item.EntityId, 1,
                        Math.Max(1, unit.Level), unit.SubRank))
                });
            }
            var passives = combat.MonsterPassiveSkillItems(monsterId, groupNo)
                .Where(value => value > 0).Select(value => new FightOfficialPassiveSkillBinding
                { ItemId = value, Rank = 1, Level = Math.Max(1, unit.Level), SubRank = unit.SubRank }).ToList();
            combat.AssembleUnit(unit, skills, combat.CreatePassiveStatusBindings(unit, skillItems, passives));
        }

        private static void ApplyPassiveProperties(FightOfficialCombatCatalog combat,
            FightOfficialSkillItemCatalog skillItems, Dictionary<int, long> properties,
            IEnumerable<int> itemIds, Func<int, int> rankForItem, Func<int, int> levelForItem, int subRank)
        {
            foreach (int itemId in itemIds ?? Array.Empty<int>())
            {
                if (itemId <= 0) continue;
                FightOfficialSkillItem item = skillItems.Required(itemId);
                IReadOnlyDictionary<int, long> passive = combat.CalculatePassiveSkillProps(
                    item.PassiveRankPropId, item.PassiveGroupLevelPropId, item.PassiveRankFactorId,
                    item.PassivePropFactors, Math.Max(1, rankForItem(itemId)),
                    Math.Max(1, levelForItem(itemId)), subRank);
                foreach (KeyValuePair<int, long> property in passive)
                    properties[property.Key] = properties.TryGetValue(property.Key, out long current)
                        ? checked(current + property.Value) : property.Value;
            }
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

        private static List<FightPropertyValue> ToProperties(IEnumerable<KeyValuePair<int, long>> values) =>
            values.Where(value => value.Key > 0 && value.Value != 0)
                .OrderBy(value => value.Key).Select(value => new FightPropertyValue
                { Id = value.Key, Value = value.Value }).ToList();

        private static Dictionary<int, long> PropertyMap(IEnumerable<FightPropertyValue> values)
        {
            var result = new Dictionary<int, long>();
            foreach (FightPropertyValue value in values ?? Array.Empty<FightPropertyValue>())
                if (value != null && value.Id > 0)
                    result[value.Id] = result.TryGetValue(value.Id, out long current)
                        ? checked(current + value.Value) : value.Value;
            return result;
        }

        private static long Property(IReadOnlyDictionary<int, long> values, int id) =>
            values.TryGetValue(id, out long result) ? result : 0L;

        private static int CheckedProperty(IReadOnlyDictionary<int, long> values, int id)
        {
            long value = Property(values, id);
            return value <= 0 ? 1 : checked((int)Math.Min(value, int.MaxValue));
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
