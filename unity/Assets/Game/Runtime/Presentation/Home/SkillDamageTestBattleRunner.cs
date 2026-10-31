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
        private Vector3 previousPlayerPosition, previousPlayerScale, previousCameraPosition;
        private Quaternion previousPlayerRotation, previousCameraRotation;
        private Camera battleCamera;
        private float previousCameraSize;
        private bool complete;
        private readonly List<GameObject> hiddenHomeObjects = new List<GameObject>();
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
            if (controller.TryGetSceneCamera(out var position, out float size) && host.Scene.Camera != null) {
                battleCamera = host.Scene.Camera;
                previousCameraPosition = battleCamera.transform.position; previousCameraRotation = battleCamera.transform.rotation;
                previousCameraSize = battleCamera.orthographicSize;
                battleCamera.transform.position = position; battleCamera.orthographicSize = size;
            }
            foreach (Transform child in host.Scene.Root.transform)
                if (child.gameObject.activeSelf && child.gameObject != host.Scene.Camera.gameObject) {
                    hiddenHomeObjects.Add(child.gameObject);
                    child.gameObject.SetActive(false);
                }
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
            foreach (GameObject homeObject in hiddenHomeObjects) if (homeObject != null) homeObject.SetActive(true);
            hiddenHomeObjects.Clear();
            if (controller != null) {
                controller.HudControlClicked -= OnControl; controller.DisposeSession(); Destroy(controller); controller = null;
            }
            if (playerTransform != null) {
                playerTransform.SetParent(previousPlayerParent, false); playerTransform.localPosition = previousPlayerPosition;
                playerTransform.localRotation = previousPlayerRotation; playerTransform.localScale = previousPlayerScale; playerTransform = null;
            }
            if (battleCamera != null) {
                battleCamera.transform.position = previousCameraPosition; battleCamera.transform.rotation = previousCameraRotation;
                battleCamera.orthographicSize = previousCameraSize; battleCamera = null;
            }
            if (battleRoot != null) { Destroy(battleRoot.gameObject); battleRoot = null; }
        }

        private FightBattleCheckpoint BuildCheckpoint(Map10RuntimeStage stage,
            Map10RuntimeRoster roster, Map10RuntimeBattleScene scene)
        {
            Map10RuntimeFightGridGroundComponent sourceGrid = scene.FightGridGroundComponent;
            if (sourceGrid?.Size == null || sourceGrid.PosInfos == null || sourceGrid.OriginPos == null)
                throw new InvalidOperationException("Official damage-test scene grid is malformed.");
            var grid = new FightBattleGrid
            {
                Width = sourceGrid.Size.X, Height = sourceGrid.Size.Y,
                OriginX = sourceGrid.OriginPos.X, OriginY = sourceGrid.OriginPos.Y,
                OriginZ = sourceGrid.OriginPos.Z, DefaultGroundType = 2
            };
            int moveMask = 0;
            foreach (Map10RuntimeBattleCell cell in sourceGrid.PosInfos)
            {
                grid.Cells.Add(new FightBattleGridCell { X = cell.Pos.X, Y = cell.Pos.Y,
                    GroundType = cell.GroundType, Height = cell.Height });
                if (cell.Height >= 999) grid.BlockedPositions.Add(new FightGridPoint(cell.Pos.X, cell.Pos.Y));
                else if (cell.GroundType >= 0 && cell.GroundType < 31) moveMask |= 1 << cell.GroundType;
            }
            if (moveMask == 0) throw new InvalidOperationException("Official damage-test scene has no walkable cells.");
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
            ApplyPlayerPassives(combat, skillItems, playerProps, playerSave, progression);
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
                X = spawn.X, Y = spawn.Y, Hp = Property(playerProps, maxHpId),
                MaxHp = Property(playerProps, maxHpId), LastMaxHp = Property(playerProps, maxHpId),
                MoveMask = moveMask, Properties = ToProperties(playerProps)
            };
            player.MoveDist = formula.CalculateMoveDist(playerProps);
            if (player.MaxHp <= 0) throw new InvalidOperationException("Official damage-test player MaxHp is invalid.");
            player.Turn.Id = player.Id; player.Turn.Hp = player.Hp;
            player.Turn.Speed = CheckedProperty(playerProps, speedId);
            checkpoint.Units.Add(player);
            AssemblePlayer(combat, skillItems, player, playerSave, progression);

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
                        Hp = Property(props, maxHpId), MaxHp = Property(props, maxHpId),
                        LastMaxHp = Property(props, maxHpId), MoveMask = moveMask,
                        Direction = monster.FlipX ? 1 : 0, Properties = ToProperties(props),
                        MaxHpPlies = combat.MonsterHpPliesFor(monster.MonsterId,
                            monster.MonsterGroupNo, Mathf.RoundToInt(monster.Level))
                    };
                    unit.MoveDist = formula.CalculateMoveDist(props);
                    unit.Turn.Id = unit.Id; unit.Turn.Hp = unit.Hp;
                    unit.Turn.Speed = CheckedProperty(props, speedId);
                    checkpoint.Units.Add(unit); wave.UnitIds.Add(unit.Id);
                    AssembleMonster(combat, skillItems, unit, monster);
                }
                checkpoint.Waves.Add(wave);
            }
            if (checkpoint.Waves.Count == 0) throw new InvalidOperationException("Official damage-test layer has no waves.");
            return checkpoint;
        }

        private static void ApplyPlayerPassives(FightOfficialCombatCatalog combat,
            FightOfficialSkillItemCatalog items, Dictionary<int, long> properties, OfflinePlayerProgress player,
            OfflineCharacterProgressionService progression)
        {
            int[] passives = OfflineSkillProgression.EquippedPassiveSkills(player.Skills);
            for (int slot = 0; slot < passives.Length; slot++)
            {
                int id = passives[slot]; if (id <= 0) continue;
                ApplyPassive(combat, items, properties, id,
                    OfflineSkillProgression.OwnedRank(player.Skills, id),
                    progression.SkillProgression.SlotLevel(player.Skills, 101, slot, progression.SkillContext().CurrentSeasonId), player.SubRank);
            }
        }

        private static void ApplyMonsterPassives(FightOfficialCombatCatalog combat,
            FightOfficialSkillItemCatalog items, Dictionary<int, long> properties, Map10RuntimeMonster monster)
        {
            foreach (int id in monster.PassiveSkills ?? Array.Empty<int>())
                if (id > 0) ApplyPassive(combat, items, properties, id, 1,
                    Mathf.Max(1, Mathf.RoundToInt(monster.Level)), monster.SubRank);
        }

        private static void ApplyPassive(FightOfficialCombatCatalog combat, FightOfficialSkillItemCatalog items,
            Dictionary<int, long> properties, int itemId, int rank, int level, int subRank)
        {
            FightOfficialSkillItem item = items.Required(itemId);
            IReadOnlyDictionary<int, long> props = combat.CalculatePassiveSkillProps(item.PassiveRankPropId,
                item.PassiveGroupLevelPropId, item.PassiveRankFactorId, item.PassivePropFactors,
                Mathf.Max(1, rank), Mathf.Max(1, level), subRank);
            foreach (KeyValuePair<int, long> pair in props)
                properties[pair.Key] = properties.TryGetValue(pair.Key, out long current)
                    ? checked(current + pair.Value) : pair.Value;
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
            Dictionary<int, long> properties = PropertyMap(unit.Properties);
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
                    SubRank = player.SubRank,
                    Cooldown = combat.CalculateSkillCooldown(item.EntityId, rank, level, player.SubRank, properties),
                    Properties = ToProperties(combat.CalculateSkillProps(item.EntityId, rank, level, player.SubRank)) });
            }
            var passive = OfflineSkillProgression.EquippedPassiveSkills(player.Skills)
                .Select((value, slot) => new FightOfficialPassiveSkillBinding { ItemId = value,
                    Rank = Math.Max(1, OfflineSkillProgression.OwnedRank(player.Skills, value)),
                    Level = Math.Max(1, progression.SkillProgression.SlotLevel(player.Skills, 101, slot,
                        progression.SkillContext().CurrentSeasonId)),
                    SubRank = player.SubRank }).Where(value => value.ItemId > 0).ToList();
            combat.AssembleUnit(unit, active, combat.CreatePassiveStatusBindings(unit, items, passive));
        }

        private static void AssembleMonster(FightOfficialCombatCatalog combat, FightOfficialSkillItemCatalog items,
            FightBattleUnitState unit, Map10RuntimeMonster monster)
        {
            Dictionary<int, long> properties = PropertyMap(unit.Properties);
            var active = new List<FightOfficialSkillBinding>();
            foreach (Map10RuntimeSkill source in monster.ActiveSkills ?? Array.Empty<Map10RuntimeSkill>())
            {
                if (source.EntityId <= 0) continue;
                active.Add(new FightOfficialSkillBinding { EntityId = source.EntityId, Slot = source.Slot,
                    ItemId = source.ItemId, Rank = Math.Max(1, source.Rank), Level = Math.Max(1, unit.Level),
                    SubRank = unit.SubRank,
                    Cooldown = combat.CalculateSkillCooldown(source.EntityId, Math.Max(1, source.Rank),
                        Math.Max(1, unit.Level), unit.SubRank, properties),
                    Properties = ToProperties(combat.CalculateSkillProps(source.EntityId,
                        Math.Max(1, source.Rank), Math.Max(1, unit.Level), unit.SubRank)) });
            }
            var passive = (monster.PassiveSkills ?? Array.Empty<int>()).Where(value => value > 0)
                .Select(value => new FightOfficialPassiveSkillBinding { ItemId = value, Rank = 1,
                    Level = Math.Max(1, unit.Level), SubRank = unit.SubRank }).ToList();
            combat.AssembleUnit(unit, active, combat.CreatePassiveStatusBindings(unit, items, passive));
        }

        private static List<FightPropertyValue> ToProperties(IEnumerable<KeyValuePair<int, long>> values) =>
            values.Where(value => value.Key > 0 && value.Value != 0).OrderBy(value => value.Key)
                .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList();
        private static Dictionary<int, long> PropertyMap(IEnumerable<FightPropertyValue> values) =>
            (values ?? Array.Empty<FightPropertyValue>()).Where(value => value != null && value.Id > 0)
                .GroupBy(value => value.Id).ToDictionary(group => group.Key, group => group.Sum(value => value.Value));
        private static long Property(IReadOnlyDictionary<int, long> values, int id) =>
            values.TryGetValue(id, out long result) ? result : 0;
        private static int CheckedProperty(IReadOnlyDictionary<int, long> values, int id) =>
            checked((int)Property(values, id));
    }
}
