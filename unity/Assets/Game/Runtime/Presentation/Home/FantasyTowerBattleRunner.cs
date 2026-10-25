using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Offline.World;
using Zjcs.Presentation.Combat;
using Zjcs.Presentation.CharacterCreation;
using Zjcs.Presentation.World;

namespace Zjcs.Presentation.Home
{
    public sealed class FantasyTowerBattleRunner : MonoBehaviour
    {
        private HomeFeatureRuntime host;
        private OfflineFantasyTowerSession progress;
        private OfficialFantasyTowerCatalog catalog;
        private OfficialBattleRuntimeController controller;
        private FantasyTowerAudioPlayer towerAudio;
        private Transform battleRoot, playerTransform, previousPlayerParent;
        private Vector3 previousPlayerPosition, previousPlayerScale;
        private Quaternion previousPlayerRotation;
        private int entryIndex, layer;
        private float stepTimer, continueTimer;
        private bool autoPush, resultHandled, disposed, backgroundExecution;

        public event Action<int, int, bool> LayerCompleted;
        public event Action<bool> PushStateChanged;
        public bool IsAutoPushEnabled => autoPush;

        public void Begin(HomeFeatureRuntime runtime, int towerEntryIndex)
        {
            host = runtime ?? throw new ArgumentNullException(nameof(runtime));
            entryIndex = towerEntryIndex;
            catalog = host.FantasyTowerCatalog;
            progress = host.FantasyTower;
            towerAudio = host.GetComponent<FantasyTowerAudioPlayer>();
            if (towerAudio == null) towerAudio = host.gameObject.AddComponent<FantasyTowerAudioPlayer>();
            towerAudio.Initialize();
            FantasyTowerEntrySave save = progress.GetEntry(entryIndex);
            autoPush = save.autoPushEnabled;
            layer = catalog.CurrentLayer(progress, entryIndex);
            if (layer <= 0) throw new InvalidOperationException("This official Fantasy Tower entry is complete.");
            StartCurrentLayer();
        }

        public void BeginAutoNext(HomeFeatureRuntime runtime, int towerEntryIndex)
        {
            host = runtime ?? throw new ArgumentNullException(nameof(runtime));
            entryIndex = towerEntryIndex;
            catalog = host.FantasyTowerCatalog;
            progress = host.FantasyTower;
            FantasyTowerResult enabled = progress.SetAutoPush(entryIndex, true);
            if (enabled != FantasyTowerResult.Applied && enabled != FantasyTowerResult.Unchanged)
                throw new InvalidOperationException("Fantasy Tower auto push is not unlocked for this entry.");
            autoPush = true;
            layer = catalog.CurrentLayer(progress, entryIndex) - 1;
            if (layer < 1) throw new InvalidOperationException("This official Fantasy Tower entry is complete.");
            continueTimer = 5f;
        }

        public FantasyTowerResult SetAutoPush(bool enabled)
        {
            FantasyTowerResult result = progress.SetAutoPush(entryIndex, enabled);
            if (result == FantasyTowerResult.Applied || result == FantasyTowerResult.Unchanged)
            {
                autoPush = enabled;
                if (!enabled)
                {
                    continueTimer = 0f;
                    if (backgroundExecution) SetBackgroundExecution(false);
                    if (controller == null) Destroy(this);
                }
                PushStateChanged?.Invoke(enabled);
            }
            return result;
        }

        public bool SetBackgroundExecution(bool enabled)
        {
            if (enabled == backgroundExecution) return true;
            if (enabled && (!autoPush || (controller == null && continueTimer <= 0f)
                || (controller != null && controller.Checkpoint == null))) return false;
            if (enabled)
            {
                FantasyTowerResult started = progress.BeginBackgroundPush(entryIndex,
                    DateTimeOffset.Now.ToUnixTimeSeconds());
                if (started != FantasyTowerResult.Applied && started != FantasyTowerResult.Unchanged)
                    return false;
                if (controller != null)
                {
                    if (progress.SaveBackgroundCheckpoint(entryIndex, controller.Checkpoint) != FantasyTowerResult.Applied)
                        return false;
                    progress.ClearForegroundCheckpoint(entryIndex);
                }
                backgroundExecution = true;
                if (playerTransform != null)
                {
                    playerTransform.SetParent(previousPlayerParent, false);
                    playerTransform.localPosition = previousPlayerPosition;
                    playerTransform.localRotation = previousPlayerRotation;
                    playerTransform.localScale = previousPlayerScale;
                }
                if (battleRoot != null) battleRoot.gameObject.SetActive(false);
                controller?.SetHudVisible(false);
                return true;
            }
            FantasyTowerResult batch = progress.FinishBackgroundPushBatch(entryIndex,
                DateTimeOffset.Now.ToUnixTimeSeconds());
            if (batch != FantasyTowerResult.Applied && batch != FantasyTowerResult.Unchanged)
                return false;
            backgroundExecution = false;
            if (battleRoot != null) battleRoot.gameObject.SetActive(true);
            if (playerTransform != null && battleRoot != null) playerTransform.SetParent(battleRoot, true);
            controller?.SetHudVisible(true);
            if (controller?.Checkpoint != null)
            {
                progress.ClearBackgroundCheckpoint(entryIndex);
                progress.SaveForegroundCheckpoint(entryIndex, controller.Checkpoint);
            }
            return true;
        }

        private void StartCurrentLayer()
        {
            if (!backgroundExecution && entryIndex != 2)
                towerAudio.Play("Note_FantasyLadder_Flash");
            FantasyTowerLayerInfo info = catalog.GetLayer(entryIndex, layer);
            if (!catalog.TryGetBattle(info.Id, out Map10RuntimeStage stage,
                    out Map10RuntimeRoster roster, out Map10RuntimeBattleScene scene))
                throw new InvalidOperationException("Official Fantasy Tower battle data is missing for layer " + layer);
            FantasyTowerEntrySave save = progress.GetEntry(entryIndex);
            FightBattleCheckpoint checkpoint = progress.LoadForegroundCheckpoint(entryIndex, stage.Id);
            bool restore = checkpoint != null;
            if (!restore) checkpoint = BuildCheckpoint(stage, roster, scene);
            MountBattle(checkpoint, scene, restore);
            if (backgroundExecution) progress.SaveBackgroundCheckpoint(entryIndex, checkpoint);
            else progress.SaveForegroundCheckpoint(entryIndex, checkpoint);
            if (backgroundExecution)
            {
                playerTransform.SetParent(previousPlayerParent, false);
                playerTransform.localPosition = previousPlayerPosition;
                playerTransform.localRotation = previousPlayerRotation;
                playerTransform.localScale = previousPlayerScale;
                battleRoot.gameObject.SetActive(false);
                controller.SetHudVisible(false);
            }
            resultHandled = false;
            continueTimer = 0f;
        }

        private void MountBattle(FightBattleCheckpoint checkpoint, Map10RuntimeBattleScene scene, bool restore)
        {
            if (playerTransform != null) throw new InvalidOperationException("Fantasy Tower battle avatar is already mounted.");
            disposed = false;
            playerTransform = host.HomePlayerTransform;
            if (playerTransform == null) throw new InvalidOperationException("Official player model is unavailable.");
            previousPlayerParent = playerTransform.parent;
            previousPlayerPosition = playerTransform.localPosition;
            previousPlayerRotation = playerTransform.localRotation;
            previousPlayerScale = playerTransform.localScale;
            battleRoot = new GameObject("FantasyTowerBattleWorld").transform;
            FightBattleUnitState player = checkpoint.Units.First(value => value.Id == 1);
            battleRoot.position = playerTransform.position - new Vector3(
                scene.FightGridGroundComponent.OriginPos.X + player.X,
                scene.FightGridGroundComponent.OriginPos.Y + player.Y,
                scene.FightGridGroundComponent.OriginPos.Z);
            playerTransform.SetParent(battleRoot, true);

            OfficialCombatAssetCatalog combatAssets = OfficialCombatAssetCatalog.LoadDefault();
            FightOfficialCombatCatalog combat = combatAssets.Load();
            controller = gameObject.AddComponent<OfficialBattleRuntimeController>();
            controller.StepCompleted += OnStepCompleted;
            OfficialBattleGridMapper mapper = new OfficialBattleGridMapper(battleRoot, checkpoint.Grid);
            OfficialBattleHudCatalog hudCatalog = OfficialBattleHudCatalog.LoadDefault();
            Sprite fallback = hudCatalog.FindSprite("C_none-fa3e084e40.png");
            if (fallback == null) throw new InvalidOperationException("Official battle portrait fallback is missing.");
            OfficialCombatSkillIconCatalog skillIcons = OfficialCombatSkillIconCatalog.TryLoadDefault();
            OfficialBattleMonsterPortraitCatalog portraits = OfficialBattleMonsterPortraitCatalog.TryLoadDefault();
            OfficialBattleModelCatalog models = OfficialBattleModelCatalog.LoadDefault();
            controller.Begin(checkpoint, battleRoot, host.Hud.CanvasTransform, mapper,
                itemId => skillIcons == null ? fallback : skillIcons.Find(itemId) ?? fallback,
                unit => unit.MonsterId > 0 && portraits != null
                    ? portraits.Find(combat.MonsterIconId(unit.MonsterId)) ?? fallback : fallback,
                unit => unit.Id == 1 ? null : models.ResolveModelKey(unit),
                (_, __) => "idle_01",
                (root, unit) =>
                {
                    Vector3 scale = root.localScale;
                    scale.x = Mathf.Abs(scale.x) * (unit.Direction == 1 ? -1f : 1f);
                    root.localScale = scale;
                }, combatAssets, models, hudCatalog, restore, statueProgress: host.Profile);
            controller.AttachUnitTransform(1, playerTransform);
            if (!backgroundExecution)
                playerTransform.GetComponent<CharacterCreationMeshFramePlayer>()?.PlayOnce(
                    "map/portal_01", "home/idle_01");
            controller.ConfigureSpeeds(combat.SpeedSettings, 0, IsBattleSpeedConditionMet);
            stepTimer = 0.8f;
        }

        private FightBattleCheckpoint BuildCheckpoint(Map10RuntimeStage stage,
            Map10RuntimeRoster roster, Map10RuntimeBattleScene scene)
        {
            Map10RuntimeFightGridGroundComponent sourceGrid = scene.FightGridGroundComponent;
            if (sourceGrid?.Size == null || sourceGrid.PosInfos == null || sourceGrid.OriginPos == null)
                throw new InvalidOperationException("Official Fantasy Tower scene grid is malformed.");
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
            if (moveMask == 0) throw new InvalidOperationException("Official Fantasy Tower scene has no walkable cells.");
            Map10RuntimeFactionInfo faction = scene.FightComponent?.FactionInfoList?
                .FirstOrDefault(value => value.Faction == 0);
            if (faction?.EntryPosList == null || faction.EntryPosList.Length == 0)
                throw new InvalidOperationException("Official Fantasy Tower scene has no player entry position.");

            OfflinePlayerProgress playerSave = host.Profile.Save.Player;
            OfflineCharacterProgressionService progression = host.DailyDungeonProgression;
            OfficialCombatAssetCatalog combatAssets = OfficialCombatAssetCatalog.LoadDefault();
            FightOfficialCombatCatalog combat = combatAssets.Load();
            FightOfficialSkillItemCatalog skillItems = combatAssets.LoadSkillItems();
            FightDamageFormula formula = combatAssets.LoadDamageFormula();
            Dictionary<int, long> playerProps = new Dictionary<int, long>(progression.PropertyCalculator.Evaluate(
                playerSave, host.Profile.Save.World, DateTimeOffset.Now.ToUnixTimeSeconds()).OutOfFightBattleProps);
            ApplyPlayerPassives(combat, skillItems, playerProps, playerSave);
            int maxHpId = combat.PropertyType("MaxHp"), speedId = combat.PropertyType("Speed");
            var checkpoint = new FightBattleCheckpoint
            {
                StageId = stage.Id, StageType = stage.Type, IsPvp = false,
                MaxRound = stage.MaxRound > 0 ? stage.MaxRound : roster.MaxRound,
                IsPlayerRoundLimit = stage.IsPlayerRoundLimit, PlayerRoundLimit = stage.PlayerRoundLimit,
                RandomSeed = StableSeed(host.ActiveProfileSlotId, stage.Id), Grid = grid,
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
            if (player.MaxHp <= 0) throw new InvalidOperationException("Official Fantasy Tower player MaxHp is invalid.");
            player.Turn.Id = player.Id; player.Turn.Hp = player.Hp;
            player.Turn.Speed = CheckedProperty(playerProps, speedId);
            checkpoint.Units.Add(player);
            AssemblePlayer(combat, skillItems, player, playerSave);

            foreach (Map10RuntimeWave sourceWave in (roster.Waves ?? Array.Empty<Map10RuntimeWave>()).OrderBy(value => value.Index))
            {
                var wave = new FightBattleWaveState { Index = sourceWave.Index, Spawned = sourceWave.Index == 0 };
                foreach (Map10RuntimeMonster monster in sourceWave.Units ?? Array.Empty<Map10RuntimeMonster>())
                {
                    Dictionary<int, long> props = (monster.Properties ?? Array.Empty<Map10RuntimeProperty>())
                        .Where(value => value.Id > 0).GroupBy(value => value.Id)
                        .ToDictionary(group => group.Key, group => group.Last().Value);
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
            if (checkpoint.Waves.Count == 0) throw new InvalidOperationException("Official Fantasy Tower layer has no waves.");
            return checkpoint;
        }

        private void Update()
        {
            if (resultHandled) return;
            if (continueTimer > 0f)
            {
                continueTimer -= Time.deltaTime;
                if (continueTimer <= 0f && autoPush)
                {
                    host.Hud.CloseDialog();
                    layer++;
                    StartCurrentLayer();
                }
                else if (continueTimer <= 0f) Destroy(this);
                return;
            }
            if (controller == null) return;
            FightBattleCheckpoint checkpoint = controller.Checkpoint;
            if (checkpoint == null) return;
            if (checkpoint.Finished) { Complete(checkpoint.Outcome.Won); return; }
            stepTimer -= Time.deltaTime;
            if (stepTimer > 0f) return;
            controller.Advance();
            stepTimer = controller.LastStepSeconds;
        }

        private void OnStepCompleted(FightBattleExecutionResult result)
        {
            FightBattleCheckpoint checkpoint = result?.Checkpoint;
            if (checkpoint == null) return;
            if (backgroundExecution) progress.SaveBackgroundCheckpoint(entryIndex, checkpoint);
            else progress.SaveForegroundCheckpoint(entryIndex, checkpoint);
            if (checkpoint.Finished) Complete(checkpoint.Outcome.Won);
        }

        private void Complete(bool won)
        {
            if (resultHandled) return;
            resultHandled = true;
            FantasyTowerLayerInfo info = catalog.GetLayer(entryIndex, layer);
            if (!backgroundExecution && entryIndex == 2)
                towerAudio.Play("Note_FantasyLadder_Elevator");
            List<FantasyTowerReward> rewards = won
                ? host.RollFantasyTowerRewards(info.Id, "fantasy-tower:stage:" + entryIndex + ":" + info.Id)
                : new List<FantasyTowerReward>();
            if (backgroundExecution)
            {
                FantasyTowerResult backgroundSaved = progress.CompleteBackgroundLayer(entryIndex, layer,
                    info.Id, won, rewards, DateTimeOffset.Now.ToUnixTimeSeconds());
                if (backgroundSaved != FantasyTowerResult.Applied)
                    throw new InvalidOperationException("Fantasy Tower background result could not be committed: " + backgroundSaved);
                bool hasNext = won && catalog.CurrentLayer(progress, entryIndex) > 0;
                DisposeBattle();
                autoPush = hasNext;
                if (hasNext)
                {
                    backgroundExecution = true;
                    continueTimer = 5f;
                    resultHandled = false;
                }
                else
                {
                    backgroundExecution = false;
                    host.Hud.ShowFantasyTowerPendingResult();
                    Destroy(this);
                }
                return;
            }
            FantasyTowerResult saved = progress.CompleteForegroundBattle(entryIndex, layer,
                info.Id, won, rewards, host.Profile);
            if (saved != FantasyTowerResult.Applied)
                throw new InvalidOperationException("Fantasy Tower layer result could not be committed: " + saved);
            if (won) host.RefreshAfterFantasyTowerReward();
            LayerCompleted?.Invoke(entryIndex, layer, won);
            DisposeBattle();
            bool hasNextLayer = won && catalog.CurrentLayer(progress, entryIndex) > 0;
            host.Hud.ShowFantasyTowerBattleResult(entryIndex, layer, won,
                autoPush && hasNextLayer, rewards);
            if (autoPush && hasNextLayer)
            {
                continueTimer = 5f;
                resultHandled = false;
            }
            else if (won && !hasNextLayer)
                progress.SetAutoPush(entryIndex, false);
            if (continueTimer <= 0f) Destroy(this);
        }

        private void OnDestroy() => DisposeBattle();

        private bool IsBattleSpeedConditionMet(int conditionId)
        {
            var context = new OfficialConditionContext { Level = host.Level };
            foreach (int mapClassId in host.Profile.Save.Player.UnlockedMapClassIds ?? new List<int>())
                context.UnlockedMapClassIds.Add(mapClassId);
            return host.DailyDungeonProgression.Catalog.EvaluateCondition(conditionId, context);
        }

        private void DisposeBattle()
        {
            if (disposed) return;
            disposed = true;
            if (controller != null)
            {
                controller.StepCompleted -= OnStepCompleted;
                controller.DisposeSession();
                Destroy(controller); controller = null;
            }
            if (playerTransform != null)
            {
                playerTransform.SetParent(previousPlayerParent, false);
                playerTransform.localPosition = previousPlayerPosition;
                playerTransform.localRotation = previousPlayerRotation;
                playerTransform.localScale = previousPlayerScale;
                playerTransform = null;
            }
            if (battleRoot != null) { Destroy(battleRoot.gameObject); battleRoot = null; }
        }

        private static void ApplyPlayerPassives(FightOfficialCombatCatalog combat,
            FightOfficialSkillItemCatalog items, Dictionary<int, long> properties, OfflinePlayerProgress player)
        {
            int[] passives = OfflineSkillProgression.EquippedPassiveSkills(player.Skills);
            for (int slot = 0; slot < passives.Length; slot++)
            {
                int id = passives[slot]; if (id <= 0) continue;
                ApplyPassive(combat, items, properties, id,
                    OfflineSkillProgression.OwnedRank(player.Skills, id),
                    player.Skills.PassiveSlotLevels != null && slot < player.Skills.PassiveSlotLevels.Length
                        ? player.Skills.PassiveSlotLevels[slot] : 1, player.SubRank);
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
            FightBattleUnitState unit, OfflinePlayerProgress player)
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
                FightOfficialSkillItem item = items.Required(itemId);
                int rank = Math.Max(1, OfflineSkillProgression.OwnedRank(player.Skills, itemId));
                int level = player.Skills.ActiveSlotLevels != null && slot < player.Skills.ActiveSlotLevels.Length
                    ? Math.Max(1, player.Skills.ActiveSlotLevels[slot]) : 1;
                active.Add(new FightOfficialSkillBinding { EntityId = item.EntityId, Slot = slot, ItemId = itemId,
                    Rank = rank, Level = level,
                    ProfessionAvailability = items.ProfessionAvailabilityFor(itemId,
                        unit.Profession, currentWeaponType, player.InitialWeaponType),
                    SubRank = player.SubRank,
                    Cooldown = combat.CalculateSkillCooldown(item.EntityId, rank, level, player.SubRank, properties),
                    Properties = ToProperties(combat.CalculateSkillProps(item.EntityId, rank, level, player.SubRank)) });
            }
            var passive = OfflineSkillProgression.EquippedPassiveSkills(player.Skills).Where(value => value > 0)
                .Select(value => new FightOfficialPassiveSkillBinding { ItemId = value,
                    Rank = Math.Max(1, OfflineSkillProgression.OwnedRank(player.Skills, value)), Level = 1,
                    SubRank = player.SubRank }).ToList();
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
        private static int StableSeed(string slot, int stage)
        {
            unchecked { int hash = 17; foreach (char value in slot) hash = hash * 31 + value; return hash * 31 + stage; }
        }
    }
}
