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
            BeginBattle(entity, stage, roster, scene, completed, resume);
        }

        private void BeginBattle(Map10RuntimeEntity entity, Map10RuntimeStage stage,
            Map10RuntimeRoster roster, Map10RuntimeBattleScene scene, Action completed,
            FightBattleCheckpoint resume = null)
        {
            FightBattleCheckpoint checkpoint = resume ?? BuildCheckpoint(entity, stage, roster, scene);
            if (entity != null && OfficialMainlineStaminaModule.IsStaminaPoint(entity))
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                ProgressionResult cost = stamina.Stamina.StartBattle(mapClassId, entity.PlacementId, stamina.StaminaCosts(entity), now);
                if (cost.Status != ProgressionResultStatus.Applied && cost.Status != ProgressionResultStatus.Unchanged)
                {
                    if (cost.Status == ProgressionResultStatus.InvalidState) stamina.StaminaCanStart(entity);
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

            battleController.HudControlClicked += OnBattleHudControl;

            OfficialBattleGridMapper mapper = new OfficialBattleGridMapper(battleRoot, grid);
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
            Sprite playerBattlePortrait = CaptureBattlePlayerHead(battleController.gameObject);
            battleController.Begin(checkpoint, battleRoot, uiParent, mapper,
                skillIconResolver,
                unit => unit.MonsterId > 0
                    ? (monsterPortraits == null ? null : monsterPortraits.Find(combatCatalog.MonsterIconId(unit.MonsterId)))
                    : playerBattlePortrait,
                unit => unit.Id == 1 ? null : OfficialBattleModelCatalog.LoadDefault().ResolveModelKey(unit),
                null,
                (root, unit) =>
                {
                    Vector3 scale = root.localScale;
                    scale.x = Mathf.Abs(scale.x) * (unit.Direction == 1 ? -1f : 1f);
                    root.localScale = scale;
                },
                null, null, null, resume != null, environmentGameId: scene.GameId,
                animationGroupResolver: unit => OfficialCharacterAnimationProfile.GroupForProfession(unit.Profession),
                statueProgress: progressionSave,
                options: new OfficialBattleSessionOptions
                {
                    SkipPrepareStatus = stage.SkipPrepareStatus,
                    ShowMainPanelDelay = stage.ShowMainPanelDelay,
                    LeaveInPreparing = stage.LeaveInPreparing,
                    LeaveInPlaying = stage.LeaveInPlaying,
                    ChangeCameraMode = stage.ChangeCameraMode,
                    CanFastforward = stage.CanFastforward,
                    CanChangePosition = !stage.JoinInMiddle,
                    ChangePositionCenterX = playerEntry.X,
                    ChangePositionCenterY = playerEntry.Y
                },
                nativeNpcFactory: unit => mapClassId == 12 && unit != null && unit.Id != 1
                    && OfficialMap12NpcBattleModelView.HasBinding(unit.MonsterId)
                    ? new OfficialMap12NpcBattleModelView(unit.MonsterId, battleRoot,
                        "Map12BattleNpc_" + unit.Id)
                    : null);
            battleController.AttachUnitTransform(1, avatar.transform);
            battleController.ConfigureSpeeds(
                OfficialCombatAssetCatalog.LoadDefault().Load().SpeedSettings, 0,
                IsBattleSpeedConditionMet);
            SuspendWorldForBattle();
            battleStepTimer = 0.8f;
            navigation.pendingInteraction = entity;
            activeBattleStage = stage;
            battleCompletion = completed;
            battleController.PreparationPositionChanged += () => SaveActiveBattle(true);
            SaveActiveBattle(true);
        }

        private Action battleCompletion;
        private Map10RuntimeStage activeBattleStage;

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
            Map10RuntimeRoster roster, Map10RuntimeBattleScene scene)
        {
            if (scene.FightGridGroundComponent == null || scene.FightGridGroundComponent.Size == null
                || scene.FightGridGroundComponent.PosInfos == null)
                throw new InvalidDataException("Official Map10 battle grid is malformed for GameId " + scene.GameId);
            var grid = new FightBattleGrid
            {
                Width = scene.FightGridGroundComponent.Size.X,
                Height = scene.FightGridGroundComponent.Size.Y,
                OriginX = scene.FightGridGroundComponent.OriginPos.X,
                OriginY = scene.FightGridGroundComponent.OriginPos.Y,
                OriginZ = scene.FightGridGroundComponent.OriginPos.Z,
                DefaultGroundType = 2
            };
            int moveMask = 0;
            foreach (Map10RuntimeBattleCell cell in scene.FightGridGroundComponent.PosInfos)
            {
                grid.Cells.Add(new FightBattleGridCell
                {
                    X = cell.Pos.X, Y = cell.Pos.Y, GroundType = cell.GroundType, Height = cell.Height
                });
                if (cell.Height >= 999) grid.BlockedPositions.Add(new FightGridPoint(cell.Pos.X, cell.Pos.Y));
                else if (cell.GroundType >= 0 && cell.GroundType < 31) moveMask |= 1 << cell.GroundType;
            }
            if (moveMask == 0) throw new InvalidDataException("Official Map10 battle grid has no walkable ground.");

            var checkpoint = new FightBattleCheckpoint
            {
                StageId = stage.Id,
                StageType = stage.Type,
                IsPvp = false,
                ResultRules = new FightBattleResultRules(),
                MaxRound = stage.MaxRound > 0 ? stage.MaxRound : roster.MaxRound,
                IsPlayerRoundLimit = stage.IsPlayerRoundLimit,
                PlayerRoundLimit = stage.PlayerRoundLimit,
                RandomSeed = StableSeed(slotId, stage.Id),
                Grid = grid
            };
            OfficialCombatAssetCatalog combatAssets = OfficialCombatAssetCatalog.LoadDefault();
            FightOfficialCombatCatalog combatCatalog = combatAssets.Load();
            FightStageStatusCatalog stageStatuses = combatAssets.LoadStageStatusCatalog();
            checkpoint.StageStatus = stageStatuses.CreateCheckpointConfig(
                stageStatuses.ClassIdOfStage(stage.Id));
            FightOfficialSkillItemCatalog skillItems = combatAssets.LoadSkillItems();
            var formula = combatAssets.LoadDamageFormula();
            // Final battle props from the official chunk pipeline. The raw level/rank table only
            // carries Base* properties, so using it directly leaves MaxHp/Attack unconverted.
            var playerProperties = new Dictionary<int, long>(
                progression.EvaluateProps(DateTimeOffset.UtcNow.ToUnixTimeSeconds()).OutOfFightBattleProps);
            int playerSubRank = progression.Player.SubRank;
            ApplyPlayerPassiveSkills(combatCatalog, skillItems, playerProperties, playerSubRank);
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
                Hp = Property(playerProperties, 3),
                MaxHp = Property(playerProperties, 3),
                LastMaxHp = Property(playerProperties, 3),
                MoveMask = moveMask,
                Properties = ToProperties(playerProperties)
            };
            if (player.MaxHp <= 0) throw new InvalidDataException("Official character battle MaxHp is not positive.");
            player.MoveDist = formula.CalculateMoveDist(playerProperties);
            player.Turn.Id = player.Id;
            player.Turn.Hp = player.Hp;
            player.Turn.Speed = CheckedProperty(playerProperties, 4);
            checkpoint.Units.Add(player);
            AssemblePlayerSkills(combatCatalog, skillItems, player);

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
            return checkpoint;
        }

        private void ApplyPlayerPassiveSkills(FightOfficialCombatCatalog combatCatalog,
            FightOfficialSkillItemCatalog skillItems, Dictionary<int, long> properties, int subRank)
        {
            OfflineSkillProfile profile = progression.Player.Skills;
            int[] passives = OfflineSkillProgression.EquippedPassiveSkills(profile);
            for (int slot = 0; slot < passives.Length; slot++)
            {
                int itemId = passives[slot];
                if (itemId <= 0) continue;
                int level = profile.PassiveSlotLevels != null && slot < profile.PassiveSlotLevels.Length
                    ? profile.PassiveSlotLevels[slot] : 1;
                ApplyPassiveSkill(combatCatalog, skillItems, properties, itemId,
                    OfflineSkillProgression.OwnedRank(profile, itemId), level, subRank);
            }
        }

        private static void ApplyMonsterPassiveSkills(FightOfficialCombatCatalog combatCatalog,
            FightOfficialSkillItemCatalog skillItems, Dictionary<int, long> properties,
            Map10RuntimeMonster source, int level, int subRank)
        {
            if (source.PassiveSkills == null) return;
            foreach (int itemId in source.PassiveSkills)
                if (itemId > 0)
                    ApplyPassiveSkill(combatCatalog, skillItems, properties, itemId, 1, level, subRank);
        }

        private static void ApplyPassiveSkill(FightOfficialCombatCatalog combatCatalog,
            FightOfficialSkillItemCatalog skillItems, Dictionary<int, long> properties, int itemId,
            int rank, int level, int subRank)
        {
            if (!skillItems.TryGet(itemId, out FightOfficialSkillItem item))
                throw new InvalidDataException("Official passive skill item is missing: " + itemId);
            if (item.PassiveRankPropId <= 0 && item.PassiveGroupLevelPropId <= 0) return;
            IReadOnlyDictionary<int, long> passive = combatCatalog.CalculatePassiveSkillProps(
                item.PassiveRankPropId, item.PassiveGroupLevelPropId, item.PassiveRankFactorId,
                item.PassivePropFactors, Math.Max(1, rank), Math.Max(1, level), subRank);
            foreach (KeyValuePair<int, long> property in passive)
                properties[property.Key] = properties.TryGetValue(property.Key, out long current)
                    ? checked(current + property.Value) : property.Value;
        }

        private void AssemblePlayerSkills(FightOfficialCombatCatalog combatCatalog,
            FightOfficialSkillItemCatalog skillItems, FightBattleUnitState player)
        {
            OfflineSkillProfile profile = progression.Player.Skills;
            int[] equipped = OfflineSkillProgression.EquippedActiveSkills(profile);
            if (!equipped.Any(value => value > 0))
            {
                int starter = skillItems.InitialActiveSkill(progression.Player.InitialWeaponType);
                if (starter > 0) equipped = new[] { starter, 0, 0, 0 };
            }

            IReadOnlyDictionary<int, long> sourceProps = PropertyMap(player.Properties);
            int weaponItemId = OfflineEquipmentOperations.EquippedItemIdAtPosition(
                progression.Player.Equipment, 10);
            int currentWeaponType = skillItems.ItemTypeFor(weaponItemId);
            var bindings = new List<FightOfficialSkillBinding>();
            for (int slot = 0; slot < equipped.Length; slot++)
            {
                int itemId = equipped[slot];
                if (itemId <= 0) continue;
                int skinId = progression.Player.Wardrobe?.SkillSkins?.FirstOrDefault(value => value.SkillId == itemId)?.SkillSkinId ?? 0;
                FightOfficialSkillItem item = skillItems.RequiredForSkin(itemId, skinId);
                if (item.EntityId <= 0)
                    throw new InvalidDataException("Official active skill item has no fight entity: " + itemId);
                int rank = OfflineSkillProgression.OwnedRank(profile, itemId);
                if (rank <= 0) rank = 1;
                int level = profile != null && profile.ActiveSlotLevels != null
                    && slot < profile.ActiveSlotLevels.Length ? profile.ActiveSlotLevels[slot] : 1;
                if (level <= 0) level = 1;
                bindings.Add(new FightOfficialSkillBinding
                {
                    EntityId = item.EntityId,
                    Slot = slot,
                    ItemId = itemId,
                    UseItemId = item.ItemId,
                    SkillSkinId = skinId,
                    Rank = rank,
                    Level = level,
                    ProfessionAvailability = skillItems.ProfessionAvailabilityFor(item.ItemId,
                        player.Profession, currentWeaponType, progression.Player.InitialWeaponType),
                    SubRank = player.SubRank,
                    Cooldown = combatCatalog.CalculateSkillCooldown(item.EntityId, rank, level,
                        player.SubRank, sourceProps),
                    Properties = ToProperties(combatCatalog.CalculateSkillProps(item.EntityId, rank,
                        level, player.SubRank))
                });
            }
            if (bindings.Count == 0)
                throw new InvalidDataException("Official player has no equipped or initial active skill.");
            List<FightOfficialPassiveSkillBinding> passives = new List<FightOfficialPassiveSkillBinding>();
            int[] passiveItems = OfflineSkillProgression.EquippedPassiveSkills(profile);
            for (int slot = 0; slot < passiveItems.Length; slot++)
            {
                int itemId = passiveItems[slot];
                if (itemId <= 0) continue;
                int rank = OfflineSkillProgression.OwnedRank(profile, itemId);
                int level = profile.PassiveSlotLevels != null && slot < profile.PassiveSlotLevels.Length
                    ? profile.PassiveSlotLevels[slot] : 1;
                passives.Add(new FightOfficialPassiveSkillBinding
                {
                    ItemId = itemId,
                    Rank = Math.Max(1, rank),
                    Level = Math.Max(1, level),
                    SubRank = player.SubRank
                });
            }
            List<FightOfficialStatusBinding> passiveStatuses = combatCatalog
                .CreatePassiveStatusBindings(player, skillItems, passives);
            combatCatalog.AssembleUnit(player, bindings, passiveStatuses);
        }

        private static void AssembleMonsterSkills(FightOfficialCombatCatalog combatCatalog,
            FightOfficialSkillItemCatalog skillItems, FightBattleUnitState unit,
            Map10RuntimeMonster source)
        {
            IReadOnlyDictionary<int, long> sourceProps = PropertyMap(unit.Properties);
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
                    SubRank = unit.SubRank,
                    Cooldown = combatCatalog.CalculateSkillCooldown(skill.EntityId, rank, level,
                        unit.SubRank, sourceProps),
                    Properties = ToProperties(combatCatalog.CalculateSkillProps(skill.EntityId, rank,
                        level, unit.SubRank))
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
            List<FightOfficialStatusBinding> passiveStatuses = combatCatalog
                .CreatePassiveStatusBindings(unit, skillItems, passives);
            combatCatalog.AssembleUnit(unit, bindings, passiveStatuses);
        }

        private static IReadOnlyDictionary<int, long> PropertyMap(List<FightPropertyValue> properties)
        {
            var result = new Dictionary<int, long>();
            if (properties == null) return result;
            foreach (FightPropertyValue property in properties)
                if (property != null && property.Id > 0)
                {
                    result.TryGetValue(property.Id, out long current);
                    result[property.Id] = checked(current + property.Value);
                }
            return result;
        }


        private void OnBattleHudControl(string path)
        {
            if (!string.IsNullOrEmpty(path) && path.IndexOf("Leave", StringComparison.OrdinalIgnoreCase) >= 0)
                LeaveBattle();
        }


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
            stamina.FinishStaminaBattle(navigation.pendingInteraction, false);
            ClearActiveBattle();
            Action callback = DestroyBattleSession();
            SaveLocalProgress();
            callback?.Invoke();
        }

        private void AbortBattle()
        {
            if (battleController == null) return;
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
                battleController.HudControlClicked -= OnBattleHudControl;
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
