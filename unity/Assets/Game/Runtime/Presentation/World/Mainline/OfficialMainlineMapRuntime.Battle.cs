using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Zjcs.Offline.Equipment;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Offline.State;
using Zjcs.Offline.World;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.Character;
using Zjcs.Presentation.Commerce;
using Zjcs.Presentation.OfflineWorld;
using Zjcs.Official.World;
using Zjcs.Official.WorldUI;

namespace Zjcs.Presentation.World
{
    public sealed partial class OfficialMainlineMapRuntime
    {
        private void ShowStageDialog(Map10RuntimeEntity entity, Action completed)
        {
            int stageId = ResolveEntityStageId(entity);
            if (stageId <= 0 || !stages.TryGetValue(stageId, out Map10RuntimeStage stage)
                || !rosters.TryGetValue(stageId, out Map10RuntimeRoster roster)
                || roster.Waves == null || roster.Waves.All(wave => wave == null || wave.Units == null || wave.Units.Length == 0))
            {
                hud.ShowOfficialNotice("战斗配置缺失：" + stageId);
                return;
            }

            selectedStageEntity = entity;
            selectedStageCompletion = completed;
            string root = "/WorldStageDialog/BgDialog3/Content/DialogContent (1)";
            hud.ui.SetText("WorldStageDialog", root + "/TitleGo/TitleGo/TitleTxt", "Stage " + stage.Id);
            hud.ui.SetText("WorldStageDialog", root + "/DescGo/DescText", string.Empty);
            hud.ui.SetText("WorldStageDialog", root + "/UnlockGo/BtnContent/BattleBtn/Content/Text", "开始战斗");
            hud.ui.SetNodeActive("WorldStageDialog", root + "/LockGo", false);
            hud.ui.SetNodeActive("WorldStageDialog", root + "/UnlockGo", true);
            hud.ui.SetNodeActive("WorldStageDialog", root + "/UnlockGo/BtnContent/BattleBtn", true);
            hud.ui.SetNodeActive("WorldStageDialog", root + "/BottomContent/CommonBtn_12", true);
            hud.ui.SetPanelActive("WorldStageDialog", true);
        }

        private int ResolveEntityStageId(Map10RuntimeEntity entity)
        {
            if (entity == null) return 0;
            string preferred = entity.TypeId == 218
                ? "LoStageBehBattleSteleStageComponent"
                : entity.TypeId == 305 ? "LoStageBehStoneCastleStageComponent" : "LoStageBehStageComponent";
            Map10RuntimeEntityStageBinding binding = entity.StageBindings.FirstOrDefault(value =>
                value != null && value.Component == preferred && value.StageIds != null && value.StageIds.Length > 0);
            if (binding == null)
                binding = entity.StageBindings.FirstOrDefault(value => value != null && value.StageIds != null
                    && value.StageIds.Length > 0);
            return binding?.StageIds?.FirstOrDefault(value => value > 0)
                ?? entity.StageIds.FirstOrDefault(value => value > 0);
        }

        private void StartSelectedStage()
        {
            Map10RuntimeEntity entity = selectedStageEntity;
            if (entity == null) return;
            Action completed = selectedStageCompletion;
            selectedStageEntity = null;
            selectedStageCompletion = null;
            hud.ui.SetPanelActive("WorldStageDialog", false);
            StartEntityStage(entity, completed);
        }

        private void CloseStageDialog()
        {
            selectedStageEntity = null;
            selectedStageCompletion = null;
            hud.ui.SetPanelActive("WorldStageDialog", false);
        }

        private void StartEntityStage(Map10RuntimeEntity entity, Action completed)
        {
            int stageId = ResolveEntityStageId(entity);
            if (stageId <= 0 || !stages.TryGetValue(stageId, out Map10RuntimeStage stage)
                || !rosters.TryGetValue(stageId, out Map10RuntimeRoster roster))
            {
                hud.ShowOfficialNotice("战斗配置缺失：" + stageId);
                return;
            }
            if (stageId == 9910)
            {
                CompleteEntity(entity);
                completed?.Invoke();
                return;
            }
            if (roster.Waves == null || roster.Waves.All(wave => wave == null || wave.Units == null || wave.Units.Length == 0))
            {
                CompleteEntity(entity);
                completed?.Invoke();
                return;
            }
            if (!battleScenes.TryGetValue(stage.GameId, out Map10RuntimeBattleScene scene))
                throw new InvalidDataException("Official Map10 battle scene is missing for GameId " + stage.GameId);
            bool periodicGroupAlreadyUsed = entity != null && entity.PeriodicityGroupIds != null
                && entity.PeriodicityGroupIds.Any(localSave.completedPeriodicityGroups.Contains);
            if (entity != null && !OfficialMainlineStaminaModule.IsStaminaPoint(entity)
                && ((stage.Repeat >= 0 && localSave.completedStages.Contains(stageId)) || periodicGroupAlreadyUsed))
            {
                CompleteEntity(entity);
                completed?.Invoke();
                return;
            }
            FightBattleCheckpoint resume = completed == null
                ? TryLoadActiveBattle(stageId, entity.PlacementId) : null;
            if (resume == null && ShouldOfferMainlineAssist(entity, completed))
            {
                OpenMainlineBattleAssistPicker(entity, stage, roster, scene, completed);
                return;
            }
            BeginBattle(entity, stage, roster, scene, completed, resume);
        }

        private void BeginBattle(Map10RuntimeEntity entity, Map10RuntimeStage stage,
            Map10RuntimeRoster roster, Map10RuntimeBattleScene scene, Action completed,
            FightBattleCheckpoint resume = null)
        {
            int[] selectedAssistNpcIds = ResolveMainlineAssistSelection(entity, stage, resume,
                completed == null);
            FightBattleCheckpoint checkpoint = resume;
            if (checkpoint == null)
            {
                CommitMainlineAssistSelection(entity, stage, selectedAssistNpcIds);
                try
                {
                    checkpoint = BuildCheckpoint(entity, stage, roster, scene, selectedAssistNpcIds);
                }
                catch
                {
                    CancelMainlineBattleAssist(stage.Id);
                    throw;
                }
            }
            if (entity != null && OfficialMainlineStaminaModule.IsStaminaPoint(entity))
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                ProgressionResult cost = stamina.Stamina.StartBattle(mapClassId, entity.PlacementId, stamina.StaminaCosts(entity), now);
                if (cost.Status != ProgressionResultStatus.Applied && cost.Status != ProgressionResultStatus.Unchanged)
                {
                    if (cost.Status == ProgressionResultStatus.InvalidState) stamina.StaminaCanStart(entity);
                    CancelMainlineBattleAssist(stage.Id);
                    return;
                }
                hud.RefreshWorldTopBar();
            }
            if (checkpoint.StageType == 0) checkpoint.StageType = stage.Type;
            FightBattleUnitState mainPlayer = checkpoint.Units.FirstOrDefault(value => value.Id == 1);
            Map10RuntimeFactionInfo playerFaction = scene.FightComponent.FactionInfoList.First(value => value.Faction == 0);
            Map10RuntimePoint playerEntry = playerFaction.EntryPosList[0];
            var entryPoint = mainPlayer == null
                ? new FightGridPoint(playerEntry.X, playerEntry.Y)
                : new FightGridPoint(mainPlayer.X, mainPlayer.Y);
            var grid = checkpoint.Grid;
            Vector3 battlePlayerPoint = new Vector3(grid.OriginX + entryPoint.X,
                grid.OriginY + entryPoint.Y, grid.OriginZ);
            var anchorObject = new GameObject("Map10BattleAnchor_" + stage.Id);
            anchorObject.transform.SetParent(mapRoot, false);
            anchorObject.transform.position = avatar.transform.position - battlePlayerPoint;
            battleRoot = anchorObject.transform;
            entityRoot.gameObject.SetActive(false);

            battleController = new GameObject("Map10OfficialBattle").AddComponent<OfficialBattleRuntimeController>();
            battleController.transform.SetParent(transform, false);

            Sprite fallbackPortrait = hud.uiCatalog.FindSprite("res://assets/official/sprites/C_none-fa3e084e40.png");
            if (fallbackPortrait == null) throw new InvalidOperationException("Official Map10 fallback battle portrait is missing.");
            OfficialCombatSkillIconCatalog skillIcons = OfficialCombatSkillIconCatalog.TryLoadDefault();
            OfficialBattleMonsterPortraitCatalog monsterPortraits = mapClassId == 12
                ? Resources.Load<OfficialBattleMonsterPortraitCatalog>(MapResource("monster-portraits"))
                : OfficialBattleMonsterPortraitCatalog.TryLoadDefault();
            Func<int, Sprite> skillIconResolver = itemId =>
            {
                Sprite icon = skillIcons == null ? null : skillIcons.Find(itemId);
                return icon != null ? icon : fallbackPortrait;
            };
            FightOfficialCombatCatalog combatCatalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            var characterPortrait = Zjcs.Presentation.Character.OfficialBattleCharacterPortraits.ForProfile(
                battleRoot.gameObject, progression, slotId, persistentDataPath);
            try
            {
                battleController.Begin(new OfficialBattleRequest
                {
                    Checkpoint = checkpoint,
                    WorldRoot = battleRoot,
                    PlayerAppearance = new OfficialBattlePlayerAppearanceSource(1, 1,
                        () => progressionSave.Save.Player, id => progression.Catalog.ItemType(id)),
                    HudParent = uiParent,
                    HostPresentation = new OfficialBattleHostPresentation(worldCamera, null, hud.ui.OverlayRoot,
                        additionalHudRoots: new[] { hud.ui.Find("WorldCanvas", "/WorldCanvas").transform }),
                    ModeAdapter = this,
                    SpeedConditionMet = IsBattleSpeedConditionMet,
                    SkillIcon = skillIconResolver,
                    TimelinePortrait = unit => characterPortrait(unit) ?? (unit.CharacterType != 1
                        && unit.MonsterId > 0 ? monsterPortraits?.Find(combatCatalog.MonsterIconId(unit.MonsterId)) : null),
                    RestoreCheckpoint = resume != null,
                    EnvironmentGameId = scene.GameId,
                    AnimationGroupResolver = unit => OfficialCharacterAnimationProfile.GroupForProfession(unit.Profession),
                    StatueProgress = progressionSave,
                    Options = OfficialMapBattleSessionOptionsBuilder.Create(stage,
                        !stage.JoinInMiddle, changePositionCenterX: playerEntry.X,
                        changePositionCenterY: playerEntry.Y),
                    NativeNpcFactory = unit => mapClassId == 12 && unit != null && unit.Id != 1
                        && OfficialMap12NpcBattleModelView.HasBinding(unit.MonsterId)
                        ? new OfficialMap12NpcBattleModelView(unit.MonsterId, battleRoot,
                            "Map12BattleNpc_" + unit.Id)
                        : null
                });
            }
            catch
            {
                CancelMainlineBattleAssist(stage.Id);
                throw;
            }
            SuspendWorldForBattle();
            battleStepTimer = 0f;
            navigation.pendingInteraction = entity;
            activeBattleStage = stage;
            battleCompletion = completed;
            battleController.PreparationPositionChanged += () => SaveActiveBattle(true);
            SaveActiveBattle(true);
        }

        private Action battleCompletion;
        private Map10RuntimeStage activeBattleStage;

        public void OnBattleCompleted(FightBattleCheckpoint checkpoint)
        {
            FinishBattle();
        }

        /// <summary>
        /// Persists the running battle so a map re-entry after closing the game can restore it. The
        /// checkpoint serializer lives in the rules assembly and keeps every unit, skill, status,
        /// timeline, receipt and statistics field.
        /// </summary>
        private void SaveActiveBattle(bool force)
        {
            if (battleController == null || activeBattleStage == null || navigation.pendingInteraction == null) return;
            FightBattleCheckpoint checkpoint = battleController.Checkpoint;
            if (checkpoint == null || checkpoint.Finished) return;
            if (!force && checkpoint.Counter - lastActiveSaveCounter < 5) return;
            try
            {
                localSave.activeBattle = FightBattleCheckpointSerializer.Save(checkpoint);
                localSave.activeBattleStageId = activeBattleStage.Id;
                localSave.activeBattlePlacementId = navigation.pendingInteraction.PlacementId;
                localSave.activeBattleSavedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                lastActiveSaveCounter = checkpoint.Counter;
                SaveLocalProgress();
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
            }
        }

        private FightBattleCheckpoint TryLoadActiveBattle(int stageId, int placementId)
        {
            if (string.IsNullOrEmpty(localSave.activeBattle)
                || localSave.activeBattleStageId != stageId
                || localSave.activeBattlePlacementId != placementId) return null;
            try
            {
                FightBattleCheckpoint checkpoint = FightBattleCheckpointSerializer.Load(localSave.activeBattle);
                if (checkpoint.Finished)
                {
                    ClearActiveBattle();
                    SaveLocalProgress();
                    return null;
                }
                hud.ShowOfficialNotice("继续上次战斗");
                return checkpoint;
            }
            catch (Exception error)
            {
                Debug.LogException(error, this);
                ClearActiveBattle();
                SaveLocalProgress();
                return null;
            }
        }

        private void ClearActiveBattle()
        {
            localSave.activeBattle = string.Empty;
            localSave.activeBattleStageId = 0;
            localSave.activeBattlePlacementId = 0;
            localSave.activeBattleSavedAt = 0;
            lastActiveSaveCounter = 0;
        }

        private int lastActiveSaveCounter;

        private FightBattleCheckpoint BuildCheckpoint(Map10RuntimeEntity entity, Map10RuntimeStage stage,
            Map10RuntimeRoster roster, Map10RuntimeBattleScene scene,
            IReadOnlyList<int> selectedAssistNpcIds = null)
        {
            if (scene.FightGridGroundComponent == null || scene.FightGridGroundComponent.Size == null
                || scene.FightGridGroundComponent.PosInfos == null)
                throw new InvalidDataException("Official Map10 battle grid is malformed for GameId " + scene.GameId);
            OfficialBattleGridBuild gridBuild = OfficialBattleGridAssembler.Build(
                scene.FightGridGroundComponent.Size.X, scene.FightGridGroundComponent.Size.Y,
                scene.FightGridGroundComponent.OriginPos.X, scene.FightGridGroundComponent.OriginPos.Y,
                scene.FightGridGroundComponent.OriginPos.Z,
                scene.FightGridGroundComponent.PosInfos.Select(cell => new FightBattleGridCell
                { X = cell.Pos.X, Y = cell.Pos.Y, GroundType = cell.GroundType, Height = cell.Height }), "Map10");
            FightBattleGrid grid = gridBuild.Grid;
            int moveMask = gridBuild.MoveMask;

            OfficialCombatAssetCatalog combatAssets = OfficialCombatAssetCatalog.LoadDefault();
            FightOfficialCombatCatalog combatCatalog = combatAssets.Load();
            FightStageStatusCatalog stageStatuses = combatAssets.LoadStageStatusCatalog();
            var checkpoint = OfficialBattleCheckpointAssembler.Create(new OfficialBattleCheckpointOptions
            {
                StageId = stage.Id,
                StageType = stage.Type,
                ResultRules = new FightBattleResultRules(),
                MaxRound = stage.MaxRound > 0 ? stage.MaxRound : roster.MaxRound,
                IsPlayerRoundLimit = stage.IsPlayerRoundLimit,
                PlayerRoundLimit = stage.PlayerRoundLimit,
                RandomSeed = StableSeed(slotId, stage.Id),
                Grid = grid,
                StageStatuses = stageStatuses,
                StageStatusClassId = stageStatuses.ClassIdOfStage(stage.Id)
            });
            FightOfficialSkillItemCatalog skillItems = combatAssets.LoadSkillItems();
            var formula = combatAssets.LoadDamageFormula();
            // Final battle props from the official chunk pipeline. The raw level/rank table only
            // carries Base* properties, so using it directly leaves MaxHp/Attack unconverted.
            var playerProperties = new Dictionary<int, long>(
                progression.EvaluateProps(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).OutOfFightBattleProps);
            int playerSubRank = progression.Player.SubRank;
            OfficialBattlePassivePropertyAssembler.ApplyEquippedPlayerPassives(combatCatalog,
                skillItems, playerProperties, progression.Player, progression);
            Map10RuntimeFactionInfo faction = scene.FightComponent.FactionInfoList.First(value => value.Faction == 0);
            if (faction.EntryPosList == null || faction.EntryPosList.Length == 0)
                throw new InvalidDataException("Official Map10 battle scene has no player entry position.");
            Map10RuntimePoint playerEntry = faction.EntryPosList[0];
            var player = new FightBattleUnitState
            {
                Id = 1,
                Side = 1,
                CharacterType = 1,
                Level = progression.Player.Level,
                SubRank = playerSubRank,
                Profession = progression.Player.Wardrobe.Profession,
                X = playerEntry.X,
                Y = playerEntry.Y,
                MoveMask = moveMask
            };
            OfficialBattleUnitAssembler.Initialize(formula, player, playerProperties, 3, 4, true);
            checkpoint.Units.Add(player);
            OfficialBattlePlayerLoadoutAssembler.Assemble(combatCatalog, skillItems, player,
                progression.Player, progression, preserveZeroProperties: true, requireActiveSkill: true);
            OfficialBattlePetEnergyAssembler.TryConfigure(player, progression.Player,
                progression.Catalog, combatCatalog, skillItems);

            int nextUnitId = 2;
            bool mountLift = mapClassId == 12 && entity?.PlacementId == 3501 && stage.Id == 102;
            var stele = FindMap12Stele(entity);
            OfficialMap12Building building = null;
            (int level, int subRank) buildingLevel = default;
            if (mountLift)
            {
                LoadMap12BuildingOperations();
                building = map12Buildings.Buildings.Single(v => v.PlacementId == entity.PlacementId);
                buildingLevel = Map12BuildingLevel(building);
            }
            foreach (Map10RuntimeWave sourceWave in roster.Waves.OrderBy(value => value.Index))
            {
                var wave = new FightBattleWaveState { Index = sourceWave.Index, Spawned = sourceWave.Index == 0 };
                foreach (Map10RuntimeMonster source in sourceWave.Units)
                {
                    int monsterLevel = mountLift ? Mathf.RoundToInt(buildingLevel.level + building.StageLevelAdd + source.LevelOffset)
                        : stele != null ? Mathf.RoundToInt(stele.RegionLevel + source.LevelOffset)
                        : stage.AwardUsePlayerLevelAndSubRank
                        ? Mathf.RoundToInt(progression.Player.Level + source.LevelOffset)
                        : Mathf.RoundToInt(source.Level);
                    int monsterSubRank = mountLift ? buildingLevel.subRank : stele != null ? stele.RegionSubRank : stage.AwardUsePlayerLevelAndSubRank
                        ? progression.Player.SubRank : source.SubRank;
                    Dictionary<int, long> properties = mountLift || stele != null || stage.AwardUsePlayerLevelAndSubRank
                        ? new Dictionary<int, long>(combatCatalog.CalculateMonsterProps(source.MonsterId,
                            source.MonsterGroupNo, monsterLevel, monsterSubRank))
                        : source.Properties.ToDictionary(value => value.Id, value => value.Value);
                    ApplyMonsterPassiveSkills(combatCatalog, skillItems, properties, source,
                        monsterLevel, monsterSubRank);
                    FightBattleUnitState unit = CreateMonsterBattleUnit(combatCatalog, formula, source,
                        properties, monsterLevel, monsterSubRank, nextUnitId++, moveMask);
                    checkpoint.Units.Add(unit);
                    wave.UnitIds.Add(unit.Id);
                    AssembleMonsterSkills(combatCatalog, skillItems, unit, source);
                }
                checkpoint.Waves.Add(wave);
            }
            if (stele != null)
                AddMap12SteleNpcUnits(checkpoint, scene,
                    scene.FightComponent.FactionInfoList.First(value => value.Faction == 0), moveMask, stele);
            else if (selectedAssistNpcIds != null && selectedAssistNpcIds.Count > 0)
                AddMainlineAssistNpcUnits(checkpoint, scene,
                    scene.FightComponent.FactionInfoList.First(value => value.Faction == 0),
                    moveMask, selectedAssistNpcIds);
            return checkpoint;
        }

        private static void ApplyMonsterPassiveSkills(FightOfficialCombatCatalog combatCatalog,
            FightOfficialSkillItemCatalog skillItems, Dictionary<int, long> properties,
            Map10RuntimeMonster source, int level, int subRank)
        {
            if (source.PassiveSkills == null) return;
            OfficialBattlePassivePropertyAssembler.Apply(combatCatalog, skillItems, properties,
                source.PassiveSkills.Select(itemId => new FightOfficialPassiveSkillBinding
                { ItemId = itemId, Rank = 1, Level = level, SubRank = subRank }));
        }

        private static void AssembleMonsterSkills(FightOfficialCombatCatalog combatCatalog,
            FightOfficialSkillItemCatalog skillItems, FightBattleUnitState unit,
            Map10RuntimeMonster source)
        {
            IReadOnlyDictionary<int, long> sourceProps = OfficialBattlePropertyAssembler.ToMap(unit.Properties);
            var bindings = new List<FightOfficialSkillBinding>();
            foreach (Map10RuntimeSkill skill in source.ActiveSkills ?? Array.Empty<Map10RuntimeSkill>())
            {
                if (skill == null || skill.EntityId <= 0 || skill.ItemId <= 0) continue;
                int rank = Math.Max(1, skill.Rank);
                int level = Math.Max(1, unit.Level);
                bindings.Add(new FightOfficialSkillBinding
                {
                    EntityId = skill.EntityId,
                    Slot = skill.Slot,
                    ItemId = skill.ItemId,
                    Rank = rank,
                    Level = level,
                    SubRank = unit.SubRank
                });
            }
            var passives = new List<FightOfficialPassiveSkillBinding>();
            foreach (int itemId in source.PassiveSkills ?? Array.Empty<int>())
                if (itemId > 0)
                    passives.Add(new FightOfficialPassiveSkillBinding
                    {
                        ItemId = itemId,
                        Rank = 1,
                        Level = Math.Max(1, unit.Level),
                        SubRank = unit.SubRank
                    });
            OfficialBattleSkillAssembler.AssembleUnit(combatCatalog, skillItems, unit,
                sourceProps, bindings, passives, true);
        }

        public void OnBattleHudControlClicked(string path)
        {
            if (path == OfficialBattleHudView.CloseButtonPath)
                LeaveBattle();
        }

        public void OnBattleStepCompleted(FightBattleExecutionResult result)
        {
            // The shared controller emits a checkpoint after every resolved action. Keep the
            // map resume payload on the same cadence; SaveActiveBattle throttles disk writes.
            if (result?.Checkpoint != null && !result.Checkpoint.Finished)
                SaveActiveBattle(false);
        }

        public FightBattleReport FinalReport { get; private set; }
        public void OnBattleReportReady(FightBattleReport report) { FinalReport = report; }


        private void FinishBattle()
        {
            if (battleController == null) return;
            FightBattleCheckpoint checkpoint = battleController.Checkpoint;
            if (checkpoint == null || !battleController.CanCompleteBattle) return;
            Map10RuntimeEntity entity = navigation.pendingInteraction;
            Action callback = battleCompletion;
            bool storyBattle = callback != null;
            FightBattleOutcome outcome = checkpoint.Outcome;
            if (outcome != null && outcome.Invalid)
            {
                // Invalid duels have no settlement: never run loss, resource, attempt or reward
                // consumers. Mode-specific reservations must likewise use Outcome.CanSettle.
                CancelMainlineBattleAssist(checkpoint.StageId);
                ClearActiveBattle();
                if (map14StoryStageBattle) map14StoryStageWon = false;
                Action invalidCompleted = DestroyBattleSession();
                SaveLocalProgress();
                invalidCompleted?.Invoke();
                return;
            }
            bool won = outcome != null && outcome.Won;
            if (map14StoryStageBattle) map14StoryStageWon = won;
            if (!SettleMap12MountLiftBattle(entity, checkpoint.StageId, won)) return;
            if (!SettleMap12SteleBattle(entity, checkpoint.StageId, won)) return;
            stamina.FinishStaminaBattle(entity, won);
            if (entity != null)
            {
                if (localSave.battleResults == null) localSave.battleResults = Array.Empty<Map10RuntimeBattleResult>();
                localSave.battleResults = localSave.battleResults.Concat(new[]
                {
                    new Map10RuntimeBattleResult
                    {
                        placementId = entity.PlacementId,
                        stageId = checkpoint.StageId,
                        reason = outcome == null ? 0 : outcome.Reason,
                        won = won,
                        round = outcome == null ? 0 : outcome.Round,
                        playerHp = outcome == null ? 0 : outcome.PlayerHp,
                        enemyHp = outcome == null ? 0 : outcome.EnemyHp
                    }
                }).ToArray();
            }

            bool cleared = map14StoryStageBattle ? won : won || (mapClassId == 10 && storyBattle);
            if (cleared && entity != null && !map14StoryStageBattle)
            {
                string receipt = MapReceiptPrefix + slotId + ":battle:" + entity.PlacementId + ":" + checkpoint.StageId
                    + ":" + (localSave.battleResults == null ? 0 : localSave.battleResults.Length);
                if (!localSave.receipts.Contains(receipt))
                {
                    ProgressionResult recorded = progressionSave.RecordMapBattle(mapClassId, entity.Region, 0, receipt);
                    if (recorded.Status == ProgressionResultStatus.InvalidState)
                        throw new InvalidOperationException(recorded.Message);
                    localSave.receipts = localSave.receipts.Concat(new[] { receipt }).ToArray();
                }
                if (won)
                {
                    string winReceipt = FindMap12Stele(entity) != null
                        ? OfficialMap12SteleCatalog.WinReceipt(entity.PlacementId)
                        : MapReceiptPrefix + slotId + ":stage-win:" + checkpoint.StageId + ":"
                            + localSave.battleResults.Length;
                    ProgressionResult win = progressionSave.RecordStageWin(checkpoint.StageId, winReceipt);
                    if (win.Status == ProgressionResultStatus.InvalidState)
                        throw new InvalidOperationException(win.Message);
                    if (mapClassId == 12 && entity.PlacementId == 8101 && entity.ChangeMapClassId == 13)
                    {
                        string activateReceipt = MapReceiptPrefix + slotId + ":map-active:" + entity.PlacementId
                            + ":" + checkpoint.StageId;
                        ProgressionResult activated = progressionSave.ApplyMapActivationResult(
                            entity.ChangeMapClassId, won, activateReceipt);
                        if (activated.Status == ProgressionResultStatus.InvalidState)
                            throw new InvalidOperationException(activated.Message);
                    }
                }
                bool oneTimeStage = !stages.TryGetValue(checkpoint.StageId, out Map10RuntimeStage stage)
                    || stage.Repeat >= 0 || entity.PeriodicityGroupIds.Length > 0;
                if (entity.EntityClassId == 6620)
                {
                    entity.State = 1;
                    entityView.SetEntityState(entity.PlacementId, 1);
                    statues.witchSpawnPending = null;
                }
                else if (oneTimeStage && !OfficialMainlineStaminaModule.IsStaminaPoint(entity))
                {
                    if (!localSave.completedStages.Contains(checkpoint.StageId))
                        localSave.completedStages = localSave.completedStages.Concat(new[] { checkpoint.StageId }).ToArray();
                    foreach (int groupId in entity.PeriodicityGroupIds)
                        if (!localSave.completedPeriodicityGroups.Contains(groupId))
                            localSave.completedPeriodicityGroups = localSave.completedPeriodicityGroups.Concat(new[] { groupId }).ToArray();
                    CompleteEntity(entity);
                }
            }

            FinishMainlineBattleAssist(checkpoint.StageId);
            ClearActiveBattle();
            Action completed = DestroyBattleSession();
            SaveLocalProgress();
            if (!cleared) hud.ShowOfficialNotice("战斗失败");
            completed?.Invoke();
        }

        private void LeaveBattle()
        {
            if (battleController == null) return;
            if (activeBattleStage != null && (battleController.Phase == FightBattlePhase.Preparing
                ? !activeBattleStage.LeaveInPreparing : !activeBattleStage.LeaveInPlaying))
            {
                hud.ShowOfficialNotice("当前阶段不能离开战斗");
                return;
            }
            battleController.EndBattle();
            CancelMainlineBattleAssist(battleController.Checkpoint?.StageId ?? 0);
            stamina.FinishStaminaBattle(navigation.pendingInteraction, false);
            ClearActiveBattle();
            Action callback = DestroyBattleSession();
            SaveLocalProgress();
            callback?.Invoke();
        }

        private void AbortBattle()
        {
            if (battleController == null) return;
            CancelMainlineBattleAssist(battleController.Checkpoint?.StageId ?? 0);
            stamina.FinishStaminaBattle(navigation.pendingInteraction, false);
            ClearActiveBattle();
            DestroyBattleSession();
            SaveLocalProgress();
        }

        private Action DestroyBattleSession()
        {
            Action callback = battleCompletion;
            battleCompletion = null;
            activeBattleStage = null;
            if (battleController != null)
            {
                battleController.DisposeSession();
                Destroy(battleController.gameObject);
                battleController = null;
            }
            if (battleRoot != null) Destroy(battleRoot.gameObject);
            battleRoot = null;
            RestoreWorldAfterBattle();
            if (entityRoot != null) entityRoot.gameObject.SetActive(true);
            navigation.SetAvatarPosition(navigation.currentGrid);
            navigation.pendingInteraction = null;
            return callback;
        }

        private void ValidateBattleEntityStageBindings()
        {
            if (!MapProfile.HasNativeUi) return;
            foreach (Map10RuntimeBattleEntityStageBinding binding in battleEntityStageBindings.Values)
            {
                if (!mapEntities.TryGetValue(binding.PlacementId, out Map10RuntimeEntity entity))
                    throw new InvalidDataException("Official Map11 battle stage references a missing entity: " + binding.PlacementId);
                foreach (Map10RuntimeEntityStageBinding component in binding.StageComponents)
                {
                    Map10RuntimeEntityStageBinding local = entity.StageBindings.FirstOrDefault(value =>
                        value != null && value.Component == component.Component);
                    if (local == null || component.StageIds == null || local.StageIds == null
                        || !local.StageIds.SequenceEqual(component.StageIds))
                        throw new InvalidDataException("Map11 battle stage component differs from entity catalog: "
                            + binding.PlacementId + "/" + component.Component);
                }
                if (!(binding.PeriodicityGroupIds ?? Array.Empty<int>()).SequenceEqual(entity.PeriodicityGroupIds)
                    || !(binding.PlayerPeriodicityGroupIds ?? Array.Empty<int>()).SequenceEqual(entity.PlayerPeriodicityGroupIds))
                    throw new InvalidDataException("Map11 battle periodic groups differ from entity catalog: " + binding.PlacementId);
            }
        }
    }
}
