using System;
using UnityEditor;
using UnityEngine;
using Zjcs.Presentation.Combat;

namespace Zjcs.Combat.Official.EditorTools
{
    public static class FightCombatValidation
    {
        [MenuItem("Zjcs/Combat/Run Rule Smoke Test")]
        public static void RunSmokeTest()
        {
            RunMovementRulesTest();
            var checkpoint = new FightBattleCheckpoint { StageId = 1, RandomSeed = 1 };
            FightBattleUnitState player = Unit(1, 1);
            player.SkillState.Slots.Add(new FightSkillSlotState { Slot = 0, LimitedTimes = -1 });
            var skill = new FightBattleSkillDefinition
            {
                Slot = 0,
                EntityId = 1,
                ItemId = 1,
                TargetType = 1,
                HitGraphRootEntityId = 10
            };
            skill.HitGraphEntities.Add(new FightBattleHitEntity
            {
                EntityId = 10,
                DamageId = 1,
                HasFixedDamage = true,
                DamageType = 8,
                DamageParam = 100
            });
            player.Skills.Add(skill);
            FightBattleUnitState enemy = Unit(2, 2);
            checkpoint.Units.Add(player);
            checkpoint.Units.Add(enemy);
            var wave = new FightBattleWaveState { Index = 0 };
            wave.UnitIds.Add(enemy.Id);
            checkpoint.Waves.Add(wave);

            var formula = new FightDamageFormula(new FightDamagePropertySchema(), new FightDamageFormulaConfig());
            FightBattleExecutor executor = FightBattleExecutor.Begin(checkpoint, formula,
                new FightDamageSettlement());
            FightBattleExecutionResult first = executor.Step(checkpoint.Sequence, 1);
            if (enemy.Hp != 0 || !checkpoint.Finished || !checkpoint.Outcome.Won
                || first.Event.ActorId != 1 || first.Event.HpChanges.Count != 1)
                throw new InvalidOperationException("Fight executor smoke hit did not finish the battle with the expected HP change.");

            FightBattleExecutor restored = FightBattleExecutor.Restore(checkpoint, formula,
                new FightDamageSettlement());
            FightBattleExecutionResult replay = restored.Step(checkpoint.Sequence, 1);
            if (!replay.Replayed || replay.Event.ActorId != 1 || enemy.Hp != 0)
                throw new InvalidOperationException("Fight checkpoint replay changed the resolved battle state.");

            RunOfficialCatalogBattleTest();
            Debug.Log("Fight combat validation passed: movement, hit graph, damage settlement, official configuration assembly, outcome, and checkpoint replay. Unity "
                + Application.unityVersion);
        }

        private static void RunMovementRulesTest()
        {
            var schema = new FightDamagePropertySchema();
            schema.Names.Add(new FightPropertyName { Id = 1, Name = "MoveDist" });
            schema.Names.Add(new FightPropertyName { Id = 2, Name = "MoveDistPercent" });
            schema.FloatPropertyIds.Add(2);
            var properties = new System.Collections.Generic.Dictionary<int, long>
            {
                { 1, 3 }, { 2, 5000 }
            };
            var formula = new FightDamageFormula(schema, new FightDamageFormulaConfig());
            if (formula.CalculateMoveDist(properties) != 4)
                throw new InvalidOperationException("Official MoveDist fixed-point calculation changed.");

            var grid = new FightBattleGrid { Width = 8, Height = 1 };
            int moveDist = formula.CalculateMoveDist(properties);
            System.Collections.Generic.List<FightGridPoint> path = grid.FindPath(
                new FightGridPoint(0, 0), new FightGridPoint(7, 0), 1 << 2,
                moveDist + 2, 0, true,
                new[] { new FightGridPoint(0, 0) }, null);
            int movedSteps = path == null ? 0 : Math.Min(moveDist, path.Count - 1);
            if (path == null || movedSteps != moveDist
                || !path[movedSteps].Equals(new FightGridPoint(moveDist, 0)))
                throw new InvalidOperationException("Battle grid did not preserve the full configured MoveDist.");
        }

        private static void RunOfficialCatalogBattleTest()
        {
            OfficialCombatAssetCatalog assets = OfficialCombatAssetCatalog.LoadDefault();
            FightOfficialCombatCatalog catalog = assets.Load();
            if (catalog.EntityCount < 1000)
                throw new InvalidOperationException("Pinned official combat entity catalog is incomplete.");

            FightBattleUnitState player = Unit(1, 1);
            player.Hp = player.MaxHp = 1000;
            player.Turn.Hp = 1000;
            var skills = new System.Collections.Generic.List<FightOfficialSkillBinding>
            {
                new FightOfficialSkillBinding
                {
                    EntityId = 12860, Slot = 0, ItemId = 12860, Rank = 1, Level = 1
                }
            };
            catalog.AssembleUnit(player, skills, null);
            FightBattleSkillDefinition skill = player.Skills[0];
            skill.HasCustomDamage = true;
            skill.CustomDamage = 100;
            skill.RangeVerified = true;

            FightBattleUnitState enemy = Unit(2, 2);
            enemy.Y = -1;
            enemy.Hp = enemy.MaxHp = 236;
            enemy.Turn.Hp = 236;
            var statuses = new System.Collections.Generic.List<FightOfficialStatusBinding>
            {
                new FightOfficialStatusBinding
                {
                    ClassId = 24078, InstanceId = 1, CreatorId = 1,
                    RootSkillId = 12860, ApplyingSkillItemId = 12860
                }
            };
            catalog.AssembleUnit(enemy, null, statuses);
            if (skill.HitGraphEntities.Count != 6 || skill.HitGraphEntities[0].ChainedHits.Count == 0
                || skill.HitGraphEntities[0].ChainedHits[0] != 12862
                || enemy.Statuses.Count != 1 || enemy.Statuses[0].FalloffPercent != 4000)
                throw new InvalidOperationException("Official skill 12860 or status 24078 was not assembled from the pinned configuration.");

            var checkpoint = new FightBattleCheckpoint { StageId = 22001, RandomSeed = 1 };
            checkpoint.Units.Add(player);
            checkpoint.Units.Add(enemy);
            var wave = new FightBattleWaveState { Index = 0 };
            wave.UnitIds.Add(enemy.Id);
            checkpoint.Waves.Add(wave);

            FightBattleExecutor executor = assets.BeginBattle(checkpoint);
            FightBattleExecutionResult result = executor.Step(0, 1);
            if (result.Event.ActorId != player.Id || result.Event.SkillEntityId != 12860
                || enemy.Hp != 0 || !checkpoint.Finished || !checkpoint.Outcome.Won
                || result.Event.HpChanges.Count != 1 || result.Event.HpChanges[0].Before != 236
                || result.Event.HpChanges[0].After != 0 || checkpoint.DamageEventSerial != 6)
                throw new InvalidOperationException("Official configuration battle did not resolve the expected 12860/24078 victory.");

            FightBattleExecutionResult replay = assets.RestoreBattle(checkpoint).Step(0, 1);
            if (!replay.Replayed || replay.Event.ActorId != player.Id || enemy.Hp != 0
                || checkpoint.DamageEventSerial != 6)
                throw new InvalidOperationException("Official configuration checkpoint replay changed the resolved battle.");
        }

        private static FightBattleUnitState Unit(int id, int side)
        {
            var unit = new FightBattleUnitState
            {
                Id = id,
                Side = side,
                Hp = 100,
                MaxHp = 100,
                CharacterType = side == 1 ? 1 : 2
            };
            unit.Turn.Id = id;
            unit.Turn.Hp = unit.Hp;
            unit.Turn.Speed = 100;
            return unit;
        }
    }
}
