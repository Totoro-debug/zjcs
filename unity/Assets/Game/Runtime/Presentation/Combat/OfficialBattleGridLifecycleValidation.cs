using System;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleGridItemEntities;
using Zjcs.Official.BattleStatusPlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.EntityViews;

namespace Zjcs.Presentation.Combat
{
    // Shared by the explicit Editor/Windows Player diagnostic, using isolated saves.
    public static class OfficialBattleGridLifecycleValidation
    {
        public static FightBattleSkillDefinition Configure(FightBattleCheckpoint checkpoint)
        {
            var catalog = OfficialCombatAssetCatalog.LoadDefault().Load();
            var main = checkpoint.Units.Single(value => value.Id == 1);
            var finish = main.Skills.First(value => value.Slot == 0);
            foreach (var unit in checkpoint.Units)
            {
                unit.Level = main.Level; unit.SubRank = main.SubRank;
                unit.Hp = unit.MaxHp = unit.LastMaxHp = unit.Turn.Hp = 1000000000L;
                unit.MaxHpPlies = 1; unit.Turn.Speed = unit.Id == 1 ? 1000 : 1;
                unit.Turn.RankSpeedScale = 1;
                foreach (var properties in new[] { unit.Properties, unit.BaseProperties })
                {
                    if (properties == null) continue;
                    foreach (var property in properties)
                    {
                        if (property.Id == catalog.PropertyType("MaxHp")) property.Value = 1000000000L;
                        if (property.Id == catalog.PropertyType("Attack")) property.Value = unit.Id == 1 ? 100000L : 100L;
                        if (property.Id == catalog.PropertyType("Defence")) property.Value = 0;
                        if (property.Id == catalog.PropertyType("Speed")) property.Value = unit.Id == 1 ? 1000L : 1L;
                    }
                }
            }
            main.Skills.Clear(); main.SkillState.Slots.Clear();
            var fire = catalog.CreateSkill(13050, 0, 10130, 5, 20, main.SubRank, null);
            fire.LimitedTimes = 1;
            main.Skills.Add(fire); main.SkillState.Slots.Add(catalog.CreateSkillSlot(fire, 0));
            return finish;
        }

        public static void RequireActive(OfficialBattleRuntimeController controller, int[] ids)
        {
            var creator = controller.TryGetUnitTransform(1).gameObject;
            Require(ids.Length == 9 && controller.Checkpoint.Units.All(value => value.CharacterType != 6),
                "Burning grids entered the character roster or lost separate cell identities.");
            foreach (var id in ids)
            {
                var transform = controller.TryGetUnitTransform(id);
                Require(transform != null && transform.gameObject.activeInHierarchy, "Native burning grid is absent: " + id);
                var entity = transform.GetComponent<MyEntity>();
                Require(entity != null && entity.ClassId == 3051, "Grid was replaced by a visual-only proxy.");
                var grid = transform.GetComponent<MyGridTransform>();
                Require(ReferenceEquals(grid.Component, entity.Entity.GetComponent<ECGridTransformData>()),
                    "A child cast replaced the original grid transform projection.");
                var statuses = transform.GetComponent<MyFightStatusAgent>().MyStatuses;
                Require(statuses.Count == 1, "Native grid statuses were merged between cells.");
                var status = statuses.Single();
                Require(status.Context.Owner == transform.gameObject && status.Context.Creator == creator
                    && status.Context.Master == creator && status.myPlayablePrefab != null,
                    "Native fire status lost its original graph or creator/applicator binding.");
                Require(typeof(MyFightStatus).GetField("mPlayable", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(status) != null, "Original MyFightStatus did not play its native fire graph.");
                var role = transform.GetComponentInChildren<ECViewDriver>(true).GetView<ECFightRoleCharacterView>();
                Require(typeof(ECFightRoleCharacterView).GetField("mDiePlayableHandle", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(role) == null, "A zero-HP grid incorrectly played a character death graph.");
            }
        }

        public static void ArmFinish(FightBattleCheckpoint checkpoint, FightBattleSkillDefinition skill)
        {
            var main = checkpoint.Units.Single(value => value.Id == 1);
            main.Skills.Clear(); main.SkillState.Slots.Clear();
            skill.Slot = 0; skill.LimitedTimes = -1;
            main.Skills.Add(skill);
            main.SkillState.Slots.Add(OfficialCombatAssetCatalog.LoadDefault().Load().CreateSkillSlot(skill, 0));
            foreach (var unit in checkpoint.Units.Where(value => value.Side != main.Side))
                unit.Hp = unit.Turn.Hp = Math.Min(1000L, unit.Hp);
        }

        private static void Require(bool value, string message)
        { if (!value) throw new InvalidOperationException("Native grid battle: " + message); }
    }
}
