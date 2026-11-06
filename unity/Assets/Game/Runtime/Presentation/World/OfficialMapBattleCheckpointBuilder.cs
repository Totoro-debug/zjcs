using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Offline.Progression;
using Zjcs.Presentation.Combat;

namespace Zjcs.Presentation.World
{
    public sealed class OfficialMapBattleCheckpointRequest
    {
        public Map10RuntimeStage Stage;
        public Map10RuntimeRoster Roster;
        public Map10RuntimeBattleScene Scene;
        public OfflinePlayerProgress Player;
        public OfflineWorldProgress World;
        public OfflineCharacterProgressionService Progression;
        public int RandomSeed;
        public int MonsterLevel;
        public int MaxRound;
        public int Mode;
        public FightDamageTestRules DamageTest;
        public bool UseMonsterProperties;
    }

    /// <summary>Builds the shared official Map10 grid, player, roster and skill state.</summary>
    public static class OfficialMapBattleCheckpointBuilder
    {
        public static FightBattleCheckpoint Create(OfficialMapBattleCheckpointRequest request)
        {
            if (request?.Stage == null || request.Roster == null || request.Scene == null
                || request.Player == null || request.World == null || request.Progression == null)
                throw new ArgumentException("Official map battle inputs are incomplete.", nameof(request));
            Map10RuntimeFightGridGroundComponent sourceGrid = request.Scene.FightGridGroundComponent;
            if (sourceGrid?.Size == null || sourceGrid.PosInfos == null || sourceGrid.OriginPos == null)
                throw new InvalidOperationException("Official map battle scene grid is malformed.");
            OfficialBattleGridBuild gridBuild = OfficialBattleGridAssembler.Build(sourceGrid.Size.X,
                sourceGrid.Size.Y, sourceGrid.OriginPos.X, sourceGrid.OriginPos.Y, sourceGrid.OriginPos.Z,
                sourceGrid.PosInfos.Where(cell => cell?.Pos != null).Select(cell => new FightBattleGridCell
                { X = cell.Pos.X, Y = cell.Pos.Y, GroundType = cell.GroundType, Height = cell.Height }), "official map");
            Map10RuntimeFactionInfo faction = request.Scene.FightComponent?.FactionInfoList?
                .FirstOrDefault(value => value != null && value.Faction == 0);
            if (faction?.EntryPosList == null || faction.EntryPosList.Length == 0 || faction.EntryPosList[0] == null)
                throw new InvalidOperationException("Official map battle scene has no player entry position.");

            OfficialCombatAssetCatalog assets = OfficialCombatAssetCatalog.LoadDefault();
            FightOfficialCombatCatalog combat = assets.Load();
            FightOfficialSkillItemCatalog items = assets.LoadSkillItems();
            FightDamageFormula formula = assets.LoadDamageFormula();
            FightStageStatusCatalog statuses = assets.LoadStageStatusCatalog();
            Dictionary<int, long> playerProperties = new Dictionary<int, long>(request.Progression.PropertyCalculator
                .Evaluate(request.Player, request.World, DateTimeOffset.Now.ToUnixTimeSeconds()).OutOfFightBattleProps);
            OfficialBattlePassivePropertyAssembler.ApplyEquippedPlayerPassives(combat, items,
                playerProperties, request.Player, request.Progression);
            int maxHpId = combat.PropertyType("MaxHp");
            int speedId = combat.PropertyType("Speed");
            var checkpoint = OfficialBattleCheckpointAssembler.Create(new OfficialBattleCheckpointOptions
            {
                StageId = request.Stage.Id,
                StageType = request.Stage.Type,
                Mode = request.Mode,
                MaxRound = request.MaxRound > 0 ? request.MaxRound
                    : request.Stage.MaxRound > 0 ? request.Stage.MaxRound : request.Roster.MaxRound,
                IsPlayerRoundLimit = request.Stage.IsPlayerRoundLimit,
                PlayerRoundLimit = request.Stage.PlayerRoundLimit,
                RandomSeed = request.RandomSeed,
                Grid = gridBuild.Grid,
                StageStatuses = statuses,
                StageStatusClassId = statuses.ClassIdOfStage(request.Stage.Id),
                DamageTest = request.DamageTest
            });
            Map10RuntimePoint spawn = faction.EntryPosList[0];
            var player = new FightBattleUnitState
            {
                Id = 1, Side = 1, CharacterType = 1, Level = request.Player.Level,
                SubRank = request.Player.SubRank, Profession = request.Player.Wardrobe.Profession,
                X = spawn.X, Y = spawn.Y, MoveMask = gridBuild.MoveMask
            };
            OfficialBattleUnitAssembler.Initialize(formula, player, playerProperties, maxHpId, speedId);
            checkpoint.Units.Add(player);
            OfficialBattlePlayerLoadoutAssembler.Assemble(combat, items, player, request.Player, request.Progression);
            OfficialBattlePetEnergyAssembler.TryConfigure(player, request.Player,
                request.Progression.Catalog, combat, items);

            foreach (Map10RuntimeWave sourceWave in (request.Roster.Waves ?? Array.Empty<Map10RuntimeWave>()).OrderBy(value => value.Index))
            {
                var wave = new FightBattleWaveState { Index = sourceWave.Index, Spawned = sourceWave.Index == 0 };
                foreach (Map10RuntimeMonster monster in sourceWave.Units ?? Array.Empty<Map10RuntimeMonster>())
                {
                    if (monster?.Position == null) throw new InvalidOperationException("Official map monster position is missing.");
                    int level = request.MonsterLevel > 0
                        ? Mathf.Max(1, Mathf.RoundToInt(request.MonsterLevel + monster.LevelOffset))
                        : Mathf.Max(1, Mathf.RoundToInt(monster.Level));
                    Dictionary<int, long> properties = request.UseMonsterProperties
                        ? (monster.Properties ?? Array.Empty<Map10RuntimeProperty>()).Where(value => value != null && value.Id > 0)
                            .GroupBy(value => value.Id).ToDictionary(group => group.Key, group => group.Last().Value)
                        : new Dictionary<int, long>(combat.CalculateMonsterProps(monster.MonsterId,
                            monster.MonsterGroupNo, level, monster.SubRank));
                    ApplyMonsterPassives(combat, items, properties, monster, level);
                    var unit = new FightBattleUnitState
                    {
                        Id = checkpoint.Units.Count + 1, Side = 2, CharacterType = 2,
                        MonsterId = monster.MonsterId, MonsterGroupNo = monster.MonsterGroupNo,
                        MonsterType = monster.MonsterType, Level = level, SubRank = monster.SubRank,
                        Wave = sourceWave.Index, X = monster.Position.X, Y = monster.Position.Y,
                        MoveMask = gridBuild.MoveMask, Direction = monster.FlipX ? 1 : 0,
                        MaxHpPlies = combat.MonsterHpPliesFor(monster.MonsterId, monster.MonsterGroupNo, level)
                    };
                    OfficialBattleUnitAssembler.Initialize(formula, unit, properties, maxHpId, speedId);
                    checkpoint.Units.Add(unit);
                    wave.UnitIds.Add(unit.Id);
                    AssembleMonster(combat, items, unit, monster);
                }
                checkpoint.Waves.Add(wave);
            }
            if (checkpoint.Waves.Count == 0) throw new InvalidOperationException("Official map battle roster has no waves.");
            return checkpoint;
        }

        private static void ApplyMonsterPassives(FightOfficialCombatCatalog combat, FightOfficialSkillItemCatalog items,
            Dictionary<int, long> properties, Map10RuntimeMonster monster, int level)
        {
            OfficialBattlePassivePropertyAssembler.Apply(combat, items, properties,
                (monster.PassiveSkills ?? Array.Empty<int>()).Where(id => id > 0).Select(id =>
                    new FightOfficialPassiveSkillBinding { ItemId = id, Rank = 1, Level = level, SubRank = monster.SubRank }));
        }

        private static void AssembleMonster(FightOfficialCombatCatalog combat, FightOfficialSkillItemCatalog items,
            FightBattleUnitState unit, Map10RuntimeMonster monster)
        {
            Dictionary<int, long> properties = OfficialBattlePropertyAssembler.ToMap(unit.Properties);
            var active = (monster.ActiveSkills ?? Array.Empty<Map10RuntimeSkill>()).Where(value => value != null && value.EntityId > 0)
                .Select(value => new FightOfficialSkillBinding
                {
                    EntityId = value.EntityId, Slot = value.Slot, ItemId = value.ItemId,
                    Rank = Math.Max(1, value.Rank), Level = Math.Max(1, unit.Level), SubRank = unit.SubRank
                }).ToList();
            var passive = (monster.PassiveSkills ?? Array.Empty<int>()).Where(value => value > 0)
                .Select(value => new FightOfficialPassiveSkillBinding
                { ItemId = value, Rank = 1, Level = Math.Max(1, unit.Level), SubRank = unit.SubRank }).ToList();
            OfficialBattleSkillAssembler.AssembleUnit(combat, items, unit, properties, active, passive);
        }
    }
}
