using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Offline.Skills;
using Zjcs.Offline.World;
using Zjcs.Presentation.CharacterCreation;
using Zjcs.Presentation.Character;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.Home
{
    public sealed class ArenaBattleRunner : MonoBehaviour
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
        private Transform guardTransform;
        private bool resultHandled;
        private bool won;

        public static long CalculateGuardCombatRating(HomeFeatureRuntime runtime, ArenaRuntimeGuard guard)
        {
            if (runtime == null || guard == null) return 0L;
            OfflineCharacterProgressionService progression = runtime.DailyDungeonProgression;
            OfficialCharacterCatalog characterCatalog = progression.Catalog;
            var basicProps = new Dictionary<int, long>(OfficialCombatAssetCatalog.LoadDefault().Load()
                .CalculateMonsterProps(guard.monsterId, guard.monsterGroupId, guard.level, guard.subRank));
            ApplyCombatRatingDeductions(basicProps, characterCatalog);
            CharacterPropBasicRef basicRef = CharacterPropBasicRef.Build(guard.level, guard.subRank,
                characterCatalog.Props, (propGroupId, level) => characterCatalog.GetLevelProps(propGroupId, level));
            long commonRating = CharacterPropFormula.CombatRatingCommon(guard.level, guard.subRank,
                basicProps, characterCatalog.Props, basicRef);

            OfflinePlayerProgress skillProfile = new OfflinePlayerProgress
            {
                Level = guard.level,
                SubRank = guard.subRank,
                Wardrobe = new OfflineWardrobeProgress { Profession = guard.profession },
                ProfessionLevels = new List<OfflineProfessionLevel>
                    { new OfflineProfessionLevel { Profession = guard.profession, Level = guard.level } }
            };
            ArenaRuntimeSkill[] active = guard.activeSkills ?? Array.Empty<ArenaRuntimeSkill>();
            ArenaRuntimeSkill[] passive = guard.passiveSkills ?? Array.Empty<ArenaRuntimeSkill>();
            if (active.Length > 4 || passive.Length > 4)
                throw new InvalidOperationException("Official arena guard has more than four skills in a category.");
            OfflineSkillPlan plan = skillProfile.Skills.Plans[0];
            for (int slot = 0; slot < active.Length; slot++)
            {
                ArenaRuntimeSkill skill = active[slot];
                plan.ActiveSkills[slot] = skill.skillId;
                skillProfile.Skills.ActiveSlotLevels[slot] = Math.Max(1, skill.level);
            }
            for (int slot = 0; slot < passive.Length; slot++)
            {
                ArenaRuntimeSkill skill = passive[slot];
                plan.PassiveSkills[slot] = skill.skillId;
                skillProfile.Skills.PassiveSlotLevels[slot] = Math.Max(1, skill.level);
            }
            foreach (ArenaRuntimeSkill skill in active.Concat(passive).GroupBy(value => value.skillId)
                         .Select(group => group.First()))
                skillProfile.Skills.Skills.Add(new OfflineOwnedSkill
                    { ClassId = skill.skillId, Rank = Math.Max(1, skill.rank) });

            var scoreContext = new CharacterPropScoreContext
            {
                Level = guard.level,
                SubRank = guard.subRank,
                Props = characterCatalog.Props,
                BasicRef = basicRef
            };
            var skillSource = new OfflineSkillPropSource(characterCatalog, progression.SkillRules);
            long skillRating = skillSource.CalculateCombatRating(skillProfile,
                new Dictionary<int, long>(), scoreContext, false);
            float skillScale = characterCatalog.LevelNumber(
                runtime.Arena.Catalog.settings.GuardSkillCombatRatinScaleId, guard.level);
            return commonRating + (long)(skillRating * skillScale);
        }

        private static void ApplyCombatRatingDeductions(Dictionary<int, long> properties,
            OfficialCharacterCatalog catalog)
        {
            Dictionary<string, object> settings = catalog.Rows("game_settings").FirstOrDefault();
            if (settings == null || !settings.TryGetValue("CombatRatingDeductProps", out object raw)
                || !(raw is Dictionary<string, object> deductions)) return;
            foreach (KeyValuePair<string, object> deduction in deductions)
            {
                int propertyId = Convert.ToInt32(deduction.Key);
                long value = properties.TryGetValue(propertyId, out long current) ? current : 0L;
                properties[propertyId] = Math.Max(0L,
                    value - Convert.ToInt64(deduction.Value));
            }
        }

        public void Begin(HomeFeatureRuntime runtime, Action<bool> onCompleted)
        {
            host = runtime ?? throw new ArgumentNullException(nameof(runtime));
            completed = onCompleted;
            if (!host.Arena.HasActiveChallenge) throw new InvalidOperationException("There is no active arena challenge.");
            ArenaRuntimeScene scene = host.Arena.Catalog.arena_scene;
            ArenaRuntimeGuard guard = host.Arena.FindGuard(host.Arena.ActiveGuardTemplateId);
            if (guard == null) throw new InvalidOperationException("Official arena guard template is missing.");

            OfficialCombatAssetCatalog combatAssets = OfficialCombatAssetCatalog.LoadDefault();
            FightOfficialCombatCatalog combat = combatAssets.Load();
            FightBattleCheckpoint checkpoint;
            bool restore = !string.IsNullOrEmpty(host.Arena.ActiveBattleCheckpoint);
            if (restore)
            {
                checkpoint = FightBattleCheckpointSerializer.Load(host.Arena.ActiveBattleCheckpoint);
                if (checkpoint.StageId != host.Arena.Catalog.settings.StageId)
                    throw new InvalidOperationException("Saved arena checkpoint does not match the active challenge.");
                if (checkpoint.StageType == 0) checkpoint.StageType = 28;
            }
            else checkpoint = BuildCheckpoint(scene, guard, combatAssets, combat);

            var world = new GameObject("ArenaBattleWorld");
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
            controller.StepCompleted += OnStepCompleted;
            controller.Begin(new OfficialBattleRequest
            {
                Checkpoint = checkpoint,
                WorldRoot = battleRoot,
                EnvironmentGameId = scene.gameId,
                HudParent = host.Hud.CanvasTransform,
                AnimationGroupResolver = unit => OfficialCharacterAnimationProfile.GroupForProfession(unit.Profession),
                RestoreCheckpoint = restore,
                StatueProgress = host.Profile
            });
            controller.AttachUnitTransform(1, playerTransform);
            ArenaRuntimeAppearanceSet guardAppearance = (guard.appendAppearances
                    ?? Array.Empty<ArenaRuntimeAppearanceSet>())
                .FirstOrDefault(value => value != null && value.sex == host.Arena.ActiveGuardSex);
            if (guardAppearance == null || guardAppearance.skins == null)
                throw new InvalidOperationException("Official arena guard appearance is unavailable for the saved sex.");
            var skinByType = new Dictionary<int, int>();
            foreach (ArenaRuntimeSkin skin in guardAppearance.skins)
            {
                if (skin == null || skin.skinType < 0 || skin.skinId <= 0 || skinByType.ContainsKey(skin.skinType))
                    throw new InvalidDataException("Official arena guard appearance contains an invalid skin binding.");
                skinByType.Add(skin.skinType, skin.skinId);
            }
            GameObject guardModel = CharacterCreationNativeModelFactory.CreateArenaGuardModel(
                host.Arena.ActiveGuardSex, guard.profession, skinByType);
            guardTransform = guardModel.transform;
            guardTransform.SetParent(battleRoot, false);
            int[] guardPosition = scene.factions.First(value => value.id == 1).entryPositions[0];
            guardTransform.localPosition = new Vector3(origin[0] + guardPosition[0],
                origin[1] + guardPosition[1], origin.Length > 2 ? origin[2] : 0f);
            FightBattleUnitState guardUnit = checkpoint.Units.First(value => value.Id == 2);
            Vector3 guardScale = guardTransform.localScale;
            guardScale.x = Mathf.Abs(guardScale.x) * (guardUnit.Direction == 1 ? -1f : 1f);
            guardTransform.localScale = guardScale;
            controller.AttachUnitTransform(2, guardTransform);
            controller.ConfigureSpeeds(combatAssets.Load().SpeedSettings, 0,
                _ => false);
            host.Arena.RecordBattleCheckpoint(FightBattleCheckpointSerializer.Save(checkpoint));
        }

        private void Update()
        {
            if (controller == null || controller.Checkpoint == null || resultHandled) return;
            if (controller.CanCompleteBattle)
            {
                FinishBattle(controller.Checkpoint.Outcome.Won);
                return;
            }
            controller.Tick(Time.deltaTime);
        }

        private void OnStepCompleted(FightBattleExecutionResult result)
        {
            if (result?.Checkpoint == null) return;
            host.Arena.RecordBattleCheckpoint(FightBattleCheckpointSerializer.Save(result.Checkpoint));
        }

        private void FinishBattle(bool isWon)
        {
            if (resultHandled) return;
            resultHandled = true;
            won = isWon;
            host.Arena.CompleteChallenge(won, host.GrantArenaReward);
            Action<bool> callback = completed;
            DisposeBattle();
            callback?.Invoke(won);
            Destroy(gameObject);
        }

        private FightBattleCheckpoint BuildCheckpoint(ArenaRuntimeScene scene, ArenaRuntimeGuard guard,
            OfficialCombatAssetCatalog combatAssets, FightOfficialCombatCatalog combat)
        {
            if (scene.gridWidth <= 0 || scene.gridHeight <= 0 || scene.cells == null)
                throw new InvalidOperationException("Official arena battle grid is malformed.");
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
            foreach (ArenaRuntimeCell cell in scene.cells)
            {
                grid.Cells.Add(new FightBattleGridCell
                { X = cell.x, Y = cell.y, GroundType = cell.groundType, Height = cell.height });
                if (cell.height >= 999) grid.BlockedPositions.Add(new FightGridPoint(cell.x, cell.y));
                else if (cell.groundType >= 0 && cell.groundType < 31) moveMask |= 1 << cell.groundType;
            }
            ArenaRuntimeFaction playerFaction = scene.factions.FirstOrDefault(value => value.id == 0);
            ArenaRuntimeFaction guardFaction = scene.factions.FirstOrDefault(value => value.id == 1);
            if (moveMask == 0 || playerFaction?.entryPositions == null || guardFaction?.entryPositions == null
                || playerFaction.entryPositions.Length == 0 || guardFaction.entryPositions.Length == 0)
                throw new InvalidOperationException("Official arena scene factions or grid are incomplete.");

            OfflinePlayerProgress playerSave = host.Profile.Save.Player;
            OfflineCharacterProgressionService progression = host.DailyDungeonProgression;
            FightOfficialSkillItemCatalog skillItems = combatAssets.LoadSkillItems();
            FightDamageFormula formula = combatAssets.LoadDamageFormula();
            var checkpoint = new FightBattleCheckpoint
            {
                StageId = host.Arena.Catalog.settings.StageId,
                StageType = 28, // Common.StageType.Arena
                IsPvp = false,
                MaxRound = 25,
                RandomSeed = StableSeed(host.Arena.Save.activeChallengeId),
                Grid = grid
            };
            FightStageStatusCatalog stageStatuses = combatAssets.LoadStageStatusCatalog();
            checkpoint.StageStatus = stageStatuses.CreateCheckpointConfig(
                stageStatuses.ClassIdOfStage(checkpoint.StageId));

            var playerProperties = new Dictionary<int, long>(progression.PropertyCalculator.Evaluate(
                playerSave, host.Profile.Save.World, DateTimeOffset.Now.ToUnixTimeSeconds()).OutOfFightBattleProps);
            int[] playerPassives = OfflineSkillProgression.EquippedPassiveSkills(playerSave.Skills);
            ApplyPassiveProperties(combat, skillItems, playerProperties, playerPassives,
                itemId => OfflineSkillProgression.OwnedRank(playerSave.Skills, itemId),
                itemId => SlotLevel(playerSave.Skills.PassiveSlotLevels, playerPassives, itemId), playerSave.SubRank);
            int maxHpId = combat.PropertyType("MaxHp");
            int speedId = combat.PropertyType("Speed");
            int[] playerPos = playerFaction.entryPositions[0];
            var player = MakeUnit(1, 1, playerSave.Level, playerSave.SubRank,
                playerSave.Wardrobe.Profession, playerPos, moveMask, playerProperties, maxHpId, speedId, formula);
            player.CharacterType = 1;
            player.Direction = playerFaction.entryDirections != null && playerFaction.entryDirections.Length > 0
                ? playerFaction.entryDirections[0] : 0;
            checkpoint.Units.Add(player);
            AssemblePlayer(combat, skillItems, player, playerSave);

            Dictionary<int, long> guardProperties = new Dictionary<int, long>(combat.CalculateMonsterProps(
                guard.monsterId, guard.monsterGroupId, guard.level, guard.subRank));
            int[] guardPassives = (guard.passiveSkills ?? Array.Empty<ArenaRuntimeSkill>())
                .Select(value => value.skillId).ToArray();
            ApplyPassiveProperties(combat, skillItems, guardProperties, guardPassives,
                itemId => Math.Max(1, guard.passiveSkills.First(value => value.skillId == itemId).rank),
                itemId => Math.Max(1, guard.passiveSkills.First(value => value.skillId == itemId).level), guard.subRank);
            foreach (int key in guardProperties.Keys.ToArray()) guardProperties[key] = checked(guardProperties[key] * 2L);
            int[] guardPos = guardFaction.entryPositions[0];
            var opponent = MakeUnit(2, 2, guard.level, guard.subRank, guard.profession,
                guardPos, moveMask, guardProperties, maxHpId, speedId, formula);
            opponent.CharacterType = 1;
            opponent.Direction = guardFaction.entryDirections != null && guardFaction.entryDirections.Length > 0
                ? guardFaction.entryDirections[0] : 1;
            opponent.MonsterId = guard.monsterId;
            opponent.MonsterGroupNo = guard.monsterGroupId;
            opponent.MaxHpPlies = combat.MonsterHpPliesFor(guard.monsterId,
                guard.monsterGroupId, guard.level);
            checkpoint.Units.Add(opponent);
            AssembleGuard(combat, skillItems, opponent, guard);
            checkpoint.Waves.Add(new FightBattleWaveState { Index = 0, Spawned = true, UnitIds = { 2 } });
            return checkpoint;
        }

        private static FightBattleUnitState MakeUnit(int id, int side, int level, int subRank,
            int profession, int[] position, int moveMask, Dictionary<int, long> properties,
            int maxHpId, int speedId, FightDamageFormula formula)
        {
            long hp = Property(properties, maxHpId);
            if (hp <= 0) throw new InvalidOperationException("Arena fighter MaxHp is invalid.");
            var unit = new FightBattleUnitState
            {
                Id = id, Side = side, Level = level, SubRank = subRank, Profession = profession,
                X = position[0], Y = position[1], Hp = hp, MaxHp = hp, LastMaxHp = hp,
                MoveMask = moveMask, Properties = ToProperties(properties)
            };
            unit.MoveDist = formula.CalculateMoveDist(properties);
            unit.Turn.Id = id;
            unit.Turn.Hp = hp;
            unit.Turn.Speed = CheckedProperty(properties, speedId);
            return unit;
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
            IReadOnlyDictionary<int, long> props = PropertyMap(player.Properties);
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
                int level = SlotLevel(save.Skills.ActiveSlotLevels, equipped, itemId);
                skills.Add(new FightOfficialSkillBinding
                {
                    EntityId = item.EntityId, Slot = slot, ItemId = itemId, UseItemId = item.ItemId, SkillSkinId = skinId, Rank = rank,
                    Level = level,
                    ProfessionAvailability = skillItems.ProfessionAvailabilityFor(item.ItemId,
                        player.Profession, currentWeaponType, save.InitialWeaponType),
                    SubRank = player.SubRank,
                    Cooldown = combat.CalculateSkillCooldown(item.EntityId, rank, level, player.SubRank, props),
                    Properties = ToProperties(combat.CalculateSkillProps(item.EntityId, rank, level, player.SubRank))
                });
            }
            var passives = new List<FightOfficialPassiveSkillBinding>();
            for (int slot = 0; slot < OfflineSkillProgression.EquippedPassiveSkills(save.Skills).Length; slot++)
            {
                int itemId = OfflineSkillProgression.EquippedPassiveSkills(save.Skills)[slot];
                if (itemId <= 0) continue;
                passives.Add(new FightOfficialPassiveSkillBinding
                {
                    ItemId = itemId, Rank = Math.Max(1, OfflineSkillProgression.OwnedRank(save.Skills, itemId)),
                    Level = SlotLevel(save.Skills.PassiveSlotLevels,
                        OfflineSkillProgression.EquippedPassiveSkills(save.Skills), itemId), SubRank = player.SubRank
                });
            }
            combat.AssembleUnit(player, skills, combat.CreatePassiveStatusBindings(player, skillItems, passives));
        }

        private static void AssembleGuard(FightOfficialCombatCatalog combat,
            FightOfficialSkillItemCatalog skillItems, FightBattleUnitState unit, ArenaRuntimeGuard guard)
        {
            IReadOnlyDictionary<int, long> props = PropertyMap(unit.Properties);
            var skills = new List<FightOfficialSkillBinding>();
            int slot = 0;
            foreach (ArenaRuntimeSkill source in guard.activeSkills ?? Array.Empty<ArenaRuntimeSkill>())
            {
                FightOfficialSkillItem item = skillItems.Required(source.skillId);
                int rank = Math.Max(1, source.rank);
                int level = Math.Max(1, source.level);
                skills.Add(new FightOfficialSkillBinding
                {
                    EntityId = item.EntityId, Slot = slot++, ItemId = source.skillId,
                    Rank = rank, Level = level, SubRank = guard.subRank,
                    Cooldown = combat.CalculateSkillCooldown(item.EntityId, rank, level, guard.subRank, props),
                    Properties = ToProperties(combat.CalculateSkillProps(item.EntityId, rank, level, guard.subRank))
                });
            }
            var passives = (guard.passiveSkills ?? Array.Empty<ArenaRuntimeSkill>()).Select(value =>
                new FightOfficialPassiveSkillBinding
                { ItemId = value.skillId, Rank = Math.Max(1, value.rank),
                    Level = Math.Max(1, value.level), SubRank = guard.subRank }).ToList();
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

        private static int SlotLevel(int[] levels, int[] equipped, int itemId)
        {
            int slot = Array.IndexOf(equipped, itemId);
            return levels != null && slot >= 0 && slot < levels.Length ? Math.Max(1, levels[slot]) : 1;
        }

        private static List<FightPropertyValue> ToProperties(IEnumerable<KeyValuePair<int, long>> values) =>
            values.Where(value => value.Key > 0 && value.Value != 0).OrderBy(value => value.Key)
                .Select(value => new FightPropertyValue { Id = value.Key, Value = value.Value }).ToList();

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

        private static int StableSeed(string id)
        {
            uint value = 2166136261;
            foreach (char c in id) value = unchecked((value ^ c) * 16777619);
            return (int)(value % (FightRandomStream.Modulus - 1)) + 1;
        }

        private void OnDestroy() => DisposeBattle();

        private void DisposeBattle()
        {
            if (controller != null)
            {
                controller.StepCompleted -= OnStepCompleted;
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
    }
}
