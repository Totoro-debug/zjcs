using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    public sealed class FightPreparedSkillCast
    {
        public FightBattleSkillDefinition Skill { get; }
        public FightBattleSkillCastEvent Cast { get; }
        internal FightPreparedSkillCast(FightBattleSkillDefinition skill, FightBattleSkillCastEvent cast)
        { Skill = skill; Cast = cast; }
    }

    /// <summary>One equipped cast: the definition drives common damage/status settlement and
    /// its matching snapshot drives the original four-way effects and native skeleton pipeline.
    /// Targets have already been selected. This module never spends an equipped slot.</summary>
    public sealed class FightSkillCastModule
    {
        private readonly FightOfficialCombatCatalog statusCatalog;
        private readonly FightOfficialSkillItemCatalog skillItems;
        public FightSkillCastModule(FightOfficialCombatCatalog catalog, FightOfficialSkillItemCatalog items)
        { statusCatalog = catalog; skillItems = items; }

        public FightPreparedSkillCast Prepare(FightBattleUnitState actor, FightBattleSkillDefinition equipped,
            IReadOnlyList<FightBattleUnitState> targets, FightBattleUnitState targetPosition = null)
        {
            if (actor == null) throw new ArgumentNullException(nameof(actor));
            if (equipped == null) throw new ArgumentNullException(nameof(equipped));
            var selected = Resolve(actor, equipped, targets);
            var presentationTargets = targets as IList<FightBattleUnitState>
                ?? (targets == null ? null : new List<FightBattleUnitState>(targets));
            var cast = new FightBattleSkillCastEvent {
                ActorId = actor.Id, Slot = selected.Slot, SkillEntityId = selected.EntityId,
                SkillItemId = selected.ItemId,
                Presentation = FightBattleSkillCastPresentation.Capture(actor, selected, presentationTargets, targetPosition)
            };
            if (targets != null) foreach (var target in targets) cast.TargetIds.Add(target.Id);
            return new FightPreparedSkillCast(selected, cast);
        }

        private FightBattleSkillDefinition Resolve(FightBattleUnitState actor,
            FightBattleSkillDefinition root, IReadOnlyList<FightBattleUnitState> targets)
        {
            int itemId = root.UseItemId > 0 ? root.UseItemId : root.ItemId;
            if (statusCatalog == null || skillItems == null || targets == null || targets.Count == 0
                || !skillItems.TryGet(itemId, out var item)
                || item.DirectionEntityIds.Count == 0) return root;
            int dx = targets[0].X - actor.X, dy = targets[0].Y - actor.Y;
            if (dx == 0 && dy == 0) return root;
            // Original Pos2d.LookDirection: diagonal ties choose the horizontal axis.
            int direction = dx > 0 ? (dy > dx ? 0 : dy < -dx ? 1 : 3)
                : (dy > -dx ? 0 : dy < dx ? 1 : 2);
            if (!item.DirectionEntityIds.TryGetValue(direction, out int entityId)) return root;
            var selected = statusCatalog.CreateSkill(entityId, root.Slot, root.ItemId,
                root.Rank, root.Level, root.SubRank,
                ToPropertyList(statusCatalog.CalculateSkillProps(entityId, root.Rank, root.Level, root.SubRank)));
            selected.RootEntityId = root.RootEntityId > 0 ? root.RootEntityId : root.EntityId;
            selected.StatusOrigin = FightBattleStatusOrigin.Resolve(root, actor.Id).Copy();
            selected.UseItemId = root.UseItemId;
            selected.SkillSkinId = root.SkillSkinId;
            selected.ProfessionAvailability = root.ProfessionAvailability;
            selected.SkillCost = root.SkillCost;
            selected.HasCustomDamage = root.HasCustomDamage;
            selected.CustomDamage = root.CustomDamage;
            selected.HasCustomCure = root.HasCustomCure;
            selected.CustomCure = root.CustomCure;
            selected.IsAI = root.IsAI;
            selected.AiPriorityId = root.AiPriorityId;
            selected.LastAiPriorityId = root.LastAiPriorityId;
            selected.AiPriorityTypes = new List<int>(root.AiPriorityTypes);
            selected.LastAiPriorityTypes = new List<int>(root.LastAiPriorityTypes);
            selected.UseLastAiPriority = root.UseLastAiPriority;
            InheritBlessingScales(selected, root.BlessPropertyScales);
            actor.Direction = direction == 0 ? 2 : direction == 1 ? 0 : direction == 2 ? 1 : 3;
            if (!actor.HasFlipX) actor.FlipX = direction == 2;
            if (dx != 0) actor.FlipX = dx < 0;
            actor.HasFlipX = true;
            return selected;
        }

        private static List<FightPropertyValue> ToPropertyList(IReadOnlyDictionary<int, long> values)
        {
            var result = new List<FightPropertyValue>();
            foreach (var value in values) result.Add(new FightPropertyValue { Id = value.Key, Value = value.Value });
            return result;
        }

        private static void InheritBlessingScales(FightBattleSkillDefinition skill,
            IEnumerable<FightStatusPropertyScale> scales)
        {
            if (scales == null) return;
            skill.BlessPropertyScales = FightStatusPropertyModifiers.Copy(scales);
            FightStatusPropertyModifiers.ApplyBlessingScales(skill.EntityId, skill.Properties, skill.BlessPropertyScales);
        }
    }
}
