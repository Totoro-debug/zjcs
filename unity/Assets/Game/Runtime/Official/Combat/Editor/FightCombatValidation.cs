using System;
using System.Collections.Generic;
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
            RunDirectionalSkillTargetRangeTest();
            RunMovementRulesTest();
            RunWorldHudShieldBarTest();
            RunWorldHudUnitBarSelectionTest();
            RunBossHudResilienceTest();
            RunBossHudHpPliesTest();
            RunBossHudSpriteBindingTest();
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
            Debug.Log("Fight combat validation passed: cardinal skill target selection, movement, hit graph, damage settlement, official configuration assembly, outcome, and checkpoint replay. Unity "
                + Application.unityVersion);
        }

        private static void RunDirectionalSkillTargetRangeTest()
        {
            var cases = new[]
            {
                (x: 0, y: 1, direction: 2),
                (x: 0, y: -1, direction: 0),
                (x: -1, y: 0, direction: 1),
                (x: 1, y: 0, direction: 3),
                (x: 2, y: 2, direction: 3)
            };
            foreach (var test in cases)
            {
                FightBattleUnitState actor = Unit(1, 1);
                actor.X = actor.Y = 5;
                actor.Direction = (test.direction + 2) % 4;
                FightBattleUnitState target = Unit(2, 2);
                target.X = actor.X + test.x;
                target.Y = actor.Y + test.y;
                var skill = new FightBattleSkillDefinition { TargetType = 1 };
                skill.Range.Add(new FightGridPoint(test.x, test.y));

                List<FightBattleUnitState> selected = FightBattleTargeting.SelectInSkillRange(
                    actor, new[] { actor, target }, skill);
                if (selected.Count != 1 || selected[0] != target || actor.Direction != test.direction)
                    throw new InvalidOperationException("Directional skill targeting did not choose the matching cardinal facing.");
            }

            FightBattleUnitState unchangedActor = Unit(1, 1);
            unchangedActor.Direction = 2;
            FightBattleUnitState outOfRange = Unit(2, 2);
            outOfRange.X = 2;
            var excludedSkill = new FightBattleSkillDefinition { TargetType = 1 };
            excludedSkill.Range.Add(new FightGridPoint(1, 0));
            if (FightBattleTargeting.SelectInSkillRange(unchangedActor,
                    new[] { unchangedActor, outOfRange }, excludedSkill).Count != 0
                || unchangedActor.Direction != 2)
                throw new InvalidOperationException("Directional skill targeting released outside its selection range.");

            FightBattleUnitState multiActor = Unit(1, 1);
            multiActor.X = multiActor.Y = 5;
            FightBattleUnitState rightNear = Unit(2, 2);
            rightNear.X = 6; rightNear.Y = 5;
            FightBattleUnitState rightFar = Unit(3, 2);
            rightFar.X = 7; rightFar.Y = 5;
            FightBattleUnitState left = Unit(4, 2);
            left.X = 4; left.Y = 5;
            var multiSkill = new FightBattleSkillDefinition { TargetType = 4 };
            multiSkill.Range.Add(new FightGridPoint(1, 0));
            multiSkill.Range.Add(new FightGridPoint(2, 0));
            multiSkill.Range.Add(new FightGridPoint(-1, 0));
            List<FightBattleUnitState> sameFacing = FightBattleTargeting.SelectInSkillRange(
                multiActor, new[] { multiActor, rightNear, rightFar, left }, multiSkill);
            if (sameFacing.Count == 0 || sameFacing.Count >= 3
                || sameFacing.Exists(target => target.X < multiActor.X && multiActor.Direction == 3
                    || target.X > multiActor.X && multiActor.Direction == 1))
                throw new InvalidOperationException("Directional skill targeting mixed target cells from different facings.");
        }

        private static void RunWorldHudShieldBarTest()
        {
            var unit = new FightBattleUnitState { Hp = 50, MaxHp = 100, LastMaxHp = 100 };
            OfficialBattleUnitWorldHudView.CalculateBarRatios(unit, out float hpRatio,
                out float shieldRatio);
            if (!Mathf.Approximately(hpRatio, 0.5f) || !Mathf.Approximately(shieldRatio, 0f))
                throw new InvalidOperationException("Official unit HUD showed a shield without a persistent shield.");

            unit.Statuses.Add(new FightBattleStatusState
            {
                ShieldKind = FightBattleShieldKind.Persistent,
                ShieldValue = 30
            });
            OfficialBattleUnitWorldHudView.CalculateBarRatios(unit, out hpRatio, out shieldRatio);
            if (!Mathf.Approximately(hpRatio, 0.5f) || !Mathf.Approximately(shieldRatio, 0.8f))
                throw new InvalidOperationException("Official unit HUD shield overlay did not include current HP.");

            unit.Statuses[0].ShieldValue = 80;
            OfficialBattleUnitWorldHudView.CalculateBarRatios(unit, out hpRatio, out shieldRatio);
            if (!Mathf.Approximately(hpRatio, 50f / 130f) || !Mathf.Approximately(shieldRatio, 1f))
                throw new InvalidOperationException("Official unit HUD did not expand the display maximum for shields.");

            unit.Statuses[0].ShieldInitialized = true;
            unit.Statuses[0].ShieldRemainValue = 20;
            OfficialBattleUnitWorldHudView.CalculateBarRatios(unit, out hpRatio, out shieldRatio);
            if (!Mathf.Approximately(hpRatio, 0.5f) || !Mathf.Approximately(shieldRatio, 0.7f))
                throw new InvalidOperationException("Official unit HUD ignored the remaining persistent shield value.");

            unit.LastMaxHp = 80;
            OfficialBattleUnitWorldHudView.CalculateBarRatios(unit, out hpRatio, out shieldRatio);
            if (!Mathf.Approximately(hpRatio, 0.625f) || !Mathf.Approximately(shieldRatio, 0.875f))
                throw new InvalidOperationException("Official unit HUD ignored the synchronized maximum HP.");
        }

        private static void RunWorldHudUnitBarSelectionTest()
        {
            var player = new FightBattleUnitState { CharacterType = 1 };
            if (OfficialBattleUnitWorldHudView.ResolveUnitBarEntityId(player) != 3100)
                throw new InvalidOperationException("Official player unit HUD did not select FightCharacter_3100.");

            var characterGuard = new FightBattleUnitState
            {
                CharacterType = 1,
                MonsterId = 3991,
                MonsterTemplateId = 3100
            };
            if (OfficialBattleUnitWorldHudView.ResolveUnitBarEntityId(characterGuard) != 3991)
                throw new InvalidOperationException("Official character guard HUD did not prefer its configured monster entity.");

            var templatedMonster = new FightBattleUnitState
            {
                CharacterType = 2,
                MonsterTemplateId = 3181
            };
            if (OfficialBattleUnitWorldHudView.ResolveUnitBarEntityId(templatedMonster) != 3181)
                throw new InvalidOperationException("Official monster unit HUD did not select its template bar.");

            if (OfficialBattleUnitWorldHudView.ResolveUnitBarEntityId(
                    new FightBattleUnitState { CharacterType = 2 }) != 0)
                throw new InvalidOperationException("Unidentified non-character unit received a player HUD template.");
        }

        private static void RunBossHudResilienceTest()
        {
            var boss = new FightBattleUnitState { Resilience = 25, MaxResilience = 100 };
            if (!Mathf.Approximately(OfficialBattleHudView.BossResilienceRatio(boss), 0.25f))
                throw new InvalidOperationException("Official boss HUD resilience ratio changed.");
            boss.Resilience = 0;
            if (!Mathf.Approximately(OfficialBattleHudView.BossResilienceRatio(boss), 0f))
                throw new InvalidOperationException("Official boss HUD did not show a broken resilience bar.");
            boss.MaxResilience = 0;
            if (!Mathf.Approximately(OfficialBattleHudView.BossResilienceRatio(boss), 0f))
                throw new InvalidOperationException("Official boss HUD resilience did not hide at a zero maximum.");
        }

        private static void RunBossHudHpPliesTest()
        {
            var boss = new FightBattleUnitState { Hp = 63, MaxHp = 100, MaxHpPlies = 4 };
            OfficialBattleHudView.BossHpBarDisplay bar =
                OfficialBattleHudView.CalculateBossHpBarDisplay(boss);
            if (!Mathf.Approximately(bar.TopFill, 0.52f) || bar.RemainingPlies != 3
                || bar.LayerIndex != 1 || bar.BottomUsesShield || bar.BottomEmpty
                || bar.ShowCenter || bar.TopUsesShield)
                throw new InvalidOperationException("Official boss HP plies display did not match the current layer.");

            boss.Hp = 55;
            boss.Statuses.Add(new FightBattleStatusState
            {
                ShieldKind = FightBattleShieldKind.Persistent,
                ShieldValue = 20
            });
            bar = OfficialBattleHudView.CalculateBossHpBarDisplay(boss);
            if (!Mathf.Approximately(bar.TopFill, 0.2f) || !Mathf.Approximately(bar.CenterFill, 1f)
                || bar.RemainingPlies != 3 || bar.LayerIndex != 1
                || !bar.ShowCenter || !bar.CenterUsesShield || bar.BottomUsesShield)
                throw new InvalidOperationException("Official boss HP and shield did not share the current plies correctly.");

            boss.Hp = 50;
            bar = OfficialBattleHudView.CalculateBossHpBarDisplay(boss);
            if (!Mathf.Approximately(bar.TopFill, 0.8f) || bar.RemainingPlies != 3
                || bar.LayerIndex != 1 || bar.ShowCenter || !bar.TopUsesShield
                || bar.BottomUsesShield)
                throw new InvalidOperationException("Official boss shield-only plies did not use the shield image.");

            boss.Hp = 20;
            boss.Statuses[0].ShieldValue = 10;
            bar = OfficialBattleHudView.CalculateBossHpBarDisplay(boss);
            if (bar.RemainingPlies != 2 || bar.LayerIndex != 2 || !bar.ShowCenter
                || !bar.TopUsesShield || bar.CenterUsesShield || !bar.BottomUsesShield)
                throw new InvalidOperationException("Official boss shield-HP-shield layers were not selected.");

            boss.Hp = 0;
            boss.Statuses[0].ShieldValue = 60;
            bar = OfficialBattleHudView.CalculateBossHpBarDisplay(boss);
            if (bar.RemainingPlies != 3 || bar.LayerIndex != 1 || bar.ShowCenter
                || !bar.TopUsesShield || !bar.BottomUsesShield)
                throw new InvalidOperationException("Official shield-only boss layers were not selected.");

            boss.Hp = 10;
            boss.Statuses.Clear();
            bar = OfficialBattleHudView.CalculateBossHpBarDisplay(boss);
            if (bar.RemainingPlies != 1 || !bar.BottomEmpty)
                throw new InvalidOperationException("Official final boss HP layer did not use the empty bar sprite.");
        }

        private static void RunBossHudSpriteBindingTest()
        {
            OfficialBattleHudCatalog catalog = OfficialBattleHudCatalog.LoadDefault();
            BattleBossHpBarBinding binding = catalog.FindPrefab("boss_item")?.bossHpBar;
            if (binding == null || binding.hpSpriteFiles == null || binding.hpSpriteFiles.Length != 5
                || binding.shieldColors == null || binding.shieldColors.Length != 2
                || !Mathf.Approximately(binding.moveSpeed, 0.1f))
                throw new InvalidOperationException("Official boss HP bar sprite and movement bindings are incomplete.");
            foreach (string file in binding.hpSpriteFiles)
                if (catalog.FindSprite(file) == null)
                    throw new InvalidOperationException("Official boss HP layer sprite is not bound: " + file);
            if (catalog.FindSprite(binding.shieldSpriteFile) == null
                || catalog.FindSprite(binding.emptyBarSpriteFile) == null)
                throw new InvalidOperationException("Official boss shield or empty layer sprite is not bound.");
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
            if (catalog.MonsterHpPliesFor(3601, 2, 9) != 50
                || catalog.MonsterHpPliesFor(3601, 2, 10) != 60)
                throw new InvalidOperationException("Pinned official monster HP plies table was not bound to the combat catalog.");

            FightBattleUnitState player = Unit(1, 1);
            player.Hp = player.MaxHp = 1000;
            player.Turn.Hp = 1000;
            var skills = new System.Collections.Generic.List<FightOfficialSkillBinding>
            {
                new FightOfficialSkillBinding
                {
                    EntityId = 12860, Slot = 0, ItemId = 12860, Rank = 1, Level = 1,
                    ProfessionAvailability = FightSkillProfessionAvailability.Mismatch
                }
            };
            catalog.AssembleUnit(player, skills, null);
            FightBattleSkillDefinition skill = player.Skills[0];
            if (skill.ProfessionAvailability != FightSkillProfessionAvailability.Mismatch)
                throw new InvalidOperationException("Profession availability was not preserved in the battle skill snapshot.");
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
