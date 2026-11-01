using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Presentation.Character;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.World;

namespace Zjcs.Presentation.Home
{
    // StageDamageTestPanel request adapter. Grid, formations, skill assembly and
    // property calculations reuse the migrated official common battle pipeline.
    public sealed class SkillDamageTestBattleRunner : MonoBehaviour
    {
        private HomeFeatureRuntime host;
        private OfficialSkillDamageTestRequest request;
        private OfficialBattleRuntimeController controller;
        private Transform battleRoot, playerTransform, previousPlayerParent;
        private Vector3 previousPlayerPosition, previousPlayerScale;
        private Quaternion previousPlayerRotation;
        private bool complete;
        public event Action<FightBattleCheckpoint> Completed;
        public event Action Left;
        public OfficialBattleRuntimeController Controller => controller;

        public void Begin(HomeFeatureRuntime runtime, OfficialSkillDamageTestRequest settings)
        {
            host = runtime ?? throw new ArgumentNullException(nameof(runtime));
            request = settings ?? throw new ArgumentNullException(nameof(settings));
            var data = JsonUtility.FromJson<Map10RuntimeBattleData>(request.BattleRuntimeJson);
            if (data == null || data.manifest != "854_156383") throw new InvalidOperationException("Original damage-test battle data is absent.");
            Map10RuntimeStage stage = data.stages.Single(value => value.Id == request.StageId);
            Map10RuntimeRoster roster = data.monsterRosters.Single(value => value.StageId == request.StageId);
            Map10RuntimeBattleScene scene = data.battleScenes.Single(value => value.GameId == stage.GameId);
            FightBattleCheckpoint checkpoint = BuildCheckpoint(stage, roster, scene);
            MountBattle(checkpoint, scene, stage);
        }

        private void MountBattle(FightBattleCheckpoint checkpoint, Map10RuntimeBattleScene scene, Map10RuntimeStage stage)
        {
            playerTransform = host.HomePlayerTransform;
            if (playerTransform == null) throw new InvalidOperationException("Official home player model is absent.");
            previousPlayerParent = playerTransform.parent;
            previousPlayerPosition = playerTransform.localPosition;
            previousPlayerRotation = playerTransform.localRotation;
            previousPlayerScale = playerTransform.localScale;
            battleRoot = new GameObject("SkillDamageTestBattleWorld").transform;
            FightBattleUnitState player = checkpoint.Units.First(value => value.Id == 1);
            battleRoot.position = playerTransform.position - new Vector3(
                scene.FightGridGroundComponent.OriginPos.X + player.X,
                scene.FightGridGroundComponent.OriginPos.Y + player.Y,
                scene.FightGridGroundComponent.OriginPos.Z);
            playerTransform.SetParent(battleRoot, true);
            OfficialCombatAssetCatalog assets = OfficialCombatAssetCatalog.LoadDefault();
            FightOfficialCombatCatalog combat = assets.Load();
            controller = gameObject.AddComponent<OfficialBattleRuntimeController>();
            controller.HudControlClicked += OnControl;
            controller.Begin(new OfficialBattleRequest
            {
                Checkpoint = checkpoint,
                WorldRoot = battleRoot,
                HudParent = host.Hud.CanvasTransform,
                TimelinePortrait = OfficialBattleCharacterPortraits.ForProfile(battleRoot.gameObject,
                    host.DailyDungeonProgression, host.ActiveProfileSlotId, host.SavesRoot),
                HostPresentation = new OfficialBattleHostPresentation(host.Scene.Camera,
                    host.Scene.Root.transform, host.Hud.CanvasTransform, host.SetBattlePresentationActive),
                AnimationGroupResolver = unit => OfficialCharacterAnimationProfile.GroupForProfession(unit.Profession),
                CombatAssets = assets,
                EnvironmentGameId = stage.GameId,
                StatueProgress = host.Profile,
                Options = new OfficialBattleSessionOptions {
                    SkipPrepareStatus = stage.SkipPrepareStatus, ShowMainPanelDelay = stage.ShowMainPanelDelay,
                    LeaveInPreparing = stage.LeaveInPreparing, LeaveInPlaying = stage.LeaveInPlaying,
                    ChangeCameraMode = stage.ChangeCameraMode, CanFastforward = stage.CanFastforward,
                    CanChangePosition = true, ChangePositionWidth = 3, ChangePositionHeight = 3,
                    ChangePositionCenterX = player.X, ChangePositionCenterY = player.Y },
                InitialStepDelay = 0f
            });
            controller.AttachUnitTransform(1, playerTransform);
            controller.ConfigureSpeeds(combat.SpeedSettings, 0, conditionId => {
                var context = new OfficialConditionContext { Level = host.Level };
                foreach (int map in host.Profile.Save.Player.UnlockedMapClassIds ?? new List<int>()) context.UnlockedMapClassIds.Add(map);
                return host.DailyDungeonProgression.Catalog.EvaluateCondition(conditionId, context);
            });
        }

        private void Update()
        {
            if (controller == null || complete) return;
            controller.Tick(Time.deltaTime);
            if (!controller.CanCompleteBattle) return;
            complete = true;
            FightBattleCheckpoint checkpoint = controller.Checkpoint;
            DisposeBattle();
            Completed?.Invoke(checkpoint);
            Destroy(this);
        }

        private void OnControl(string path)
        {
            if (!path.EndsWith("/CloseBtn/CloseBtn_normal", StringComparison.Ordinal)) return;
            complete = true;
            DisposeBattle();
            Left?.Invoke();
            Destroy(this);
        }

        private void OnDestroy() => DisposeBattle();
        private void DisposeBattle()
        {
            if (controller != null) {
                controller.HudControlClicked -= OnControl; controller.DisposeSession(); Destroy(controller); controller = null;
            }
            if (playerTransform != null) {
                playerTransform.SetParent(previousPlayerParent, false); playerTransform.localPosition = previousPlayerPosition;
                playerTransform.localRotation = previousPlayerRotation; playerTransform.localScale = previousPlayerScale; playerTransform = null;
            }
            if (battleRoot != null) { Destroy(battleRoot.gameObject); battleRoot = null; }
        }

        private FightBattleCheckpoint BuildCheckpoint(Map10RuntimeStage stage,
            Map10RuntimeRoster roster, Map10RuntimeBattleScene scene)
        {
            Map10RuntimeFightGridGroundComponent sourceGrid = scene.FightGridGroundComponent;
            if (sourceGrid?.Size == null || sourceGrid.PosInfos == null || sourceGrid.OriginPos == null)
                throw new InvalidOperationException("Official damage-test scene grid is malformed.");
            OfficialBattleGridBuild gridBuild = OfficialBattleGridAssembler.Build(sourceGrid.Size.X,
                sourceGrid.Size.Y, sourceGrid.OriginPos.X, sourceGrid.OriginPos.Y, sourceGrid.OriginPos.Z,
                sourceGrid.PosInfos.Select(cell => new FightBattleGridCell { X = cell.Pos.X, Y = cell.Pos.Y,
                    GroundType = cell.GroundType, Height = cell.Height }), "damage-test");
            FightBattleGrid grid = gridBuild.Grid;
            int moveMask = gridBuild.MoveMask;
            Map10RuntimeFactionInfo faction = scene.FightComponent?.FactionInfoList?
                .FirstOrDefault(value => value.Faction == 0);
            if (faction?.EntryPosList == null || faction.EntryPosList.Length == 0)
                throw new InvalidOperationException("Official damage-test scene has no player entry position.");

            OfflinePlayerProgress playerSave = host.Profile.Save.Player;
            OfflineCharacterProgressionService progression = host.DailyDungeonProgression;
            OfficialCombatAssetCatalog combatAssets = OfficialCombatAssetCatalog.LoadDefault();
            FightOfficialCombatCatalog combat = combatAssets.Load();
            FightOfficialSkillItemCatalog skillItems = combatAssets.LoadSkillItems();
            FightDamageFormula formula = combatAssets.LoadDamageFormula();
            Dictionary<int, long> playerProps = new Dictionary<int, long>(progression.PropertyCalculator.Evaluate(
                playerSave, host.Profile.Save.World, DateTimeOffset.Now.ToUnixTimeSeconds()).OutOfFightBattleProps);
            OfficialBattlePassivePropertyAssembler.ApplyEquippedPlayerPassives(combat, skillItems,
                playerProps, playerSave, progression);
            int maxHpId = combat.PropertyType("MaxHp"), speedId = combat.PropertyType("Speed");
            var checkpoint = new FightBattleCheckpoint
            {
                StageId = stage.Id, StageType = stage.Type, IsPvp = false,
                MaxRound = request.MaxRound,
                DamageTest = new FightDamageTestRules { Difficulty = request.Difficulty,
                    EnableMonsterOneDamage = request.EnableMonsterOneDamage, EnableMonsterUnlimitHp = request.EnableMonsterUnlimitHp },
                IsPlayerRoundLimit = stage.IsPlayerRoundLimit, PlayerRoundLimit = stage.PlayerRoundLimit,
                RandomSeed = (int)((uint)Guid.NewGuid().GetHashCode() % (FightRandomStream.Modulus - 1)) + 1, Grid = grid,
                StageStatus = combatAssets.LoadStageStatusCatalog().CreateCheckpointConfig(
                    combatAssets.LoadStageStatusCatalog().ClassIdOfStage(stage.Id))
            };
            Map10RuntimePoint spawn = faction.EntryPosList[0];
            var player = new FightBattleUnitState
            {
                Id = 1, Side = 1, CharacterType = 1, Level = playerSave.Level,
                SubRank = playerSave.SubRank, Profession = playerSave.Wardrobe.Profession,
                X = spawn.X, Y = spawn.Y, MoveMask = moveMask
            };
            OfficialBattleUnitAssembler.Initialize(formula, player, playerProps, maxHpId, speedId);
            checkpoint.Units.Add(player);
            AssemblePlayer(combat, skillItems, player, playerSave, progression);
            OfficialBattlePetEnergyAssembler.TryConfigure(player, playerSave,
                progression.Catalog, combat, skillItems);

            foreach (Map10RuntimeWave sourceWave in (roster.Waves ?? Array.Empty<Map10RuntimeWave>()).OrderBy(value => value.Index))
            {
                var wave = new FightBattleWaveState { Index = sourceWave.Index, Spawned = sourceWave.Index == 0 };
                foreach (Map10RuntimeMonster monster in sourceWave.Units ?? Array.Empty<Map10RuntimeMonster>())
                {
                    // Confirmed offline server replacement: use the migrated original
                    // CalcMonsterBattleProps at the selected rank difficulty level.
                    monster.Level = Mathf.Max(1, Mathf.RoundToInt(request.MonsterLevel + monster.LevelOffset));
                    Dictionary<int, long> props = new Dictionary<int, long>(combat.CalculateMonsterProps(
                        monster.MonsterId, monster.MonsterGroupNo, monster.Level, monster.SubRank));
                    ApplyMonsterPassives(combat, skillItems, props, monster);
                    var unit = new FightBattleUnitState
                    {
                        Id = checkpoint.Units.Count + 1, Side = 2, CharacterType = 2,
                        MonsterId = monster.MonsterId, MonsterGroupNo = monster.MonsterGroupNo,
                        MonsterType = monster.MonsterType, Level = Mathf.RoundToInt(monster.Level),
                        SubRank = monster.SubRank, Wave = sourceWave.Index,
                        X = monster.Position.X, Y = monster.Position.Y,
                        MoveMask = moveMask, Direction = monster.FlipX ? 1 : 0,
                        MaxHpPlies = combat.MonsterHpPliesFor(monster.MonsterId,
                            monster.MonsterGroupNo, Mathf.RoundToInt(monster.Level))
                    };
                    OfficialBattleUnitAssembler.Initialize(formula, unit, props, maxHpId, speedId);
                    checkpoint.Units.Add(unit); wave.UnitIds.Add(unit.Id);
                    AssembleMonster(combat, skillItems, unit, monster);
                }
                checkpoint.Waves.Add(wave);
            }
            if (checkpoint.Waves.Count == 0) throw new InvalidOperationException("Official damage-test layer has no waves.");
            return checkpoint;
        }

        private static void ApplyMonsterPassives(FightOfficialCombatCatalog combat,
            FightOfficialSkillItemCatalog items, Dictionary<int, long> properties, Map10RuntimeMonster monster)
        {
            OfficialBattlePassivePropertyAssembler.Apply(combat, items, properties,
                (monster.PassiveSkills ?? Array.Empty<int>()).Select(id => new FightOfficialPassiveSkillBinding
                { ItemId = id, Rank = 1, Level = Mathf.Max(1, Mathf.RoundToInt(monster.Level)), SubRank = monster.SubRank }));
        }

        private static void AssemblePlayer(FightOfficialCombatCatalog combat, FightOfficialSkillItemCatalog items,
            FightBattleUnitState unit, OfflinePlayerProgress player, OfflineCharacterProgressionService progression)
        {
            int[] equipped = OfflineSkillProgression.EquippedActiveSkills(player.Skills);
            if (!equipped.Any(value => value > 0))
            {
                int initial = items.InitialActiveSkill(player.InitialWeaponType);
                if (initial > 0) equipped = new[] { initial, 0, 0, 0 };
            }
            Dictionary<int, long> properties = OfficialBattlePropertyAssembler.ToMap(unit.Properties);
            int weaponItemId = OfflineEquipmentOperations.EquippedItemIdAtPosition(player.Equipment, 10);
            int currentWeaponType = items.ItemTypeFor(weaponItemId);
            var active = new List<FightOfficialSkillBinding>();
            for (int slot = 0; slot < equipped.Length; slot++)
            {
                int itemId = equipped[slot]; if (itemId <= 0) continue;
                int skinId = player.Wardrobe?.SkillSkins?.FirstOrDefault(value => value.SkillId == itemId)?.SkillSkinId ?? 0;
                FightOfficialSkillItem item = items.RequiredForSkin(itemId, skinId);
                int rank = Math.Max(1, OfflineSkillProgression.OwnedRank(player.Skills, itemId));
                int level = Math.Max(1, progression.SkillProgression.SlotLevel(player.Skills, 100, slot,
                    progression.SkillContext().CurrentSeasonId));
                active.Add(new FightOfficialSkillBinding { EntityId = item.EntityId, Slot = slot, ItemId = itemId, UseItemId = item.ItemId, SkillSkinId = skinId,
                    Rank = rank, Level = level,
                    ProfessionAvailability = items.ProfessionAvailabilityFor(item.ItemId,
                        unit.Profession, currentWeaponType, player.InitialWeaponType),
                    SubRank = player.SubRank });
            }
            var passive = OfflineSkillProgression.EquippedPassiveSkills(player.Skills)
                .Select((value, slot) => new FightOfficialPassiveSkillBinding { ItemId = value,
                    Rank = Math.Max(1, OfflineSkillProgression.OwnedRank(player.Skills, value)),
                    Level = Math.Max(1, progression.SkillProgression.SlotLevel(player.Skills, 101, slot,
                        progression.SkillContext().CurrentSeasonId)),
                    SubRank = player.SubRank }).Where(value => value.ItemId > 0).ToList();
            passive.AddRange(OfficialEquipmentBattlePassives.Collect(progression, player, unit.SubRank));
            OfficialBattleSkillAssembler.AssembleUnit(combat, items, unit, properties, active, passive);
        }

        private static void AssembleMonster(FightOfficialCombatCatalog combat, FightOfficialSkillItemCatalog items,
            FightBattleUnitState unit, Map10RuntimeMonster monster)
        {
            Dictionary<int, long> properties = OfficialBattlePropertyAssembler.ToMap(unit.Properties);
            var active = new List<FightOfficialSkillBinding>();
            foreach (Map10RuntimeSkill source in monster.ActiveSkills ?? Array.Empty<Map10RuntimeSkill>())
            {
                if (source.EntityId <= 0) continue;
                active.Add(new FightOfficialSkillBinding { EntityId = source.EntityId, Slot = source.Slot,
                    ItemId = source.ItemId, Rank = Math.Max(1, source.Rank), Level = Math.Max(1, unit.Level),
                    SubRank = unit.SubRank });
            }
            var passive = (monster.PassiveSkills ?? Array.Empty<int>()).Where(value => value > 0)
                .Select(value => new FightOfficialPassiveSkillBinding { ItemId = value, Rank = 1,
                    Level = Math.Max(1, unit.Level), SubRank = unit.SubRank }).ToList();
            OfficialBattleSkillAssembler.AssembleUnit(combat, items, unit, properties, active, passive);
        }

    }
}
