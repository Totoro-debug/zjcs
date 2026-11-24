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

    /// <summary>One cast: the definition drives common damage/status settlement and
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
            // MyFightSkill.DoSourceTurn restores the role's configured FlipX.
            // Refresh that input for every turning skill, including skills without
            // directional entity variants, after any approach movement has ended.
            var aim = targetPosition ?? (targets != null && targets.Count > 0 ? targets[0] : null);
            if (selected.FlipSource && aim != null && aim.X != actor.X)
            {
                actor.FlipX = aim.X < actor.X;
                actor.HasFlipX = true;
            }
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

        /// <summary>Record an already settled HP change using its original single-target
        /// hit graph. This neither selects a new target nor repeats damage settlement,
        /// including when the recorded hit was lethal.</summary>
        public FightPreparedSkillCast PrepareRecordedHit(FightBattleUnitState actor,
            FightBattleSkillDefinition skill, FightBattleUnitState target, FightBattleDamageEvent damage)
        {
            if (target == null || damage == null) throw new ArgumentNullException();
            var prepared = Prepare(actor, skill, new[] { target });
            var hits = FightBattleHitCollector.CollectAll(prepared.Skill);
            if (hits.Count != 1) throw new InvalidOperationException("A recorded HP change requires one original hit.");
            var hit = hits[0];
            bool scoped = hit.Scope != null && hit.Scope.Count > 0;
            if (hit.EntityId != damage.HitEntityId || hit.DamageId != damage.DamageEntityId
                || damage.SourceId != actor.Id || damage.TargetId != target.Id
                || damage.SkillEntityId != prepared.Skill.EntityId || damage.DamageId <= 0
                || hit.RandomTargetGroupId != 0 || hit.ActionType != 4
                || scoped && (hit.Scope.Count != 1 || hit.Scope[0].X != 0 || hit.Scope[0].Y != 0))
                throw new InvalidOperationException("The recorded HP change does not match a single original target hit.");
            var position = new FightGridPoint(target.X, target.Y);
            var center = FightHitAnchorExtensions.ResolveCenter(hit.Anchor, actor, target, prepared.Cast.Presentation);
            if (scoped && (center.X != position.X || center.Y != position.Y))
                throw new InvalidOperationException("The recorded HP target is outside the original hit centre.");
            float scopeDelay = scoped && hit.ScopeEntries != null && hit.ScopeEntries.Count > 0
                ? hit.ScopeEntries[0].Delay : 0f;
            var selection = new FightHitTargetSelection(new List<FightBattleUnitState> { target },
                new[] { new FightHitOccurrence(target, scoped, position, scopeDelay, 0, scoped ? 0 : -1) },
                new List<FightGridPoint>(), new[] { center });
            float localDelay = FightBattleHitTiming.DamageDelay(hit, prepared.Cast.Presentation, selection, 0);
            float castDelay = damage.Delay - localDelay;
            if (float.IsNaN(castDelay) || float.IsInfinity(castDelay) || castDelay < 0f)
                throw new InvalidOperationException("The recorded HP change precedes its original hit timing.");
            prepared.Cast.Delay = castDelay;
            prepared.Cast.HitSelections.Add(FightBattleHitSelectionSnapshot.Capture(1, hit, selection,
                prepared.Cast.Presentation));
            prepared.Cast.DamageEventIds.Add(damage.DamageId);
            damage.Anchor = hit.Anchor;
            damage.HasHitGridPosition = scoped;
            damage.HitGridX = position.X; damage.HitGridY = position.Y;
            damage.HasDamagePosition = true;
            damage.DamageX = position.X; damage.DamageY = position.Y;
            damage.HasDamageTriggerStatus = hit.AppliedStatuses == null || hit.AppliedStatuses.Count == 0;
            return prepared;
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
