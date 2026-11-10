using System;
using System.Collections.Generic;

namespace Zjcs.Combat.Official
{
    /// <summary>Inputs consumed by one real hit selection, copied before settlement changes the roster.</summary>
    [Serializable]
    public sealed class FightBattleHitSelectionSnapshot
    {
        [Serializable]
        public sealed class TargetOccurrence
        {
            public int TargetId, TargetX, TargetY, EffectCenterIndex;
            public bool HasGridPosition;
            public int GridX, GridY;
            public float ScopeDelay;
        }

        // Identity is local to the enclosing cast; class ID alone is not an occurrence identity.
        public int Sequence, HitEntityId, RandomTargetGroupId, Anchor;
        public int SourceX, SourceY, FacingIndex;
        public bool FlipX;
        // Relative to the owning cast, so nested-cast step offsets are applied exactly once.
        public float Delay, DelayDistFactor;
        public bool HasDelayOverride;
        public int DelayOverrideIndex;
        // Preserve the actual draw and the centres consumed by scope projection;
        // never infer either from the settled targets later.
        public List<FightGridPoint> SelectedGridPositions = new List<FightGridPoint>();
        public List<FightGridPoint> EffectCenters = new List<FightGridPoint>();
        public List<float> EffectDelays = new List<float>();
        public List<TargetOccurrence> Targets = new List<TargetOccurrence>();

        public static FightBattleHitSelectionSnapshot Capture(int sequence, FightBattleHitDefinition hit,
            FightHitTargetSelection selection, FightBattleSkillCastPresentation cast,
            FightBattleHitTiming.Flight flight = null)
        {
            if (sequence <= 0) throw new ArgumentOutOfRangeException(nameof(sequence));
            if (hit == null) throw new ArgumentNullException(nameof(hit));
            if (selection == null) throw new ArgumentNullException(nameof(selection));
            if (cast == null) throw new ArgumentNullException(nameof(cast));
            var result = new FightBattleHitSelectionSnapshot {
                Sequence = sequence, HitEntityId = hit.EntityId, RandomTargetGroupId = hit.RandomTargetGroupId,
                Anchor = hit.Anchor, SourceX = cast.SourceX, SourceY = cast.SourceY,
                FacingIndex = cast.FacingIndex, FlipX = cast.FlipX,
                Delay = hit.Delay, DelayDistFactor = flight == null ? hit.DelayDistFactor : flight.DelayDistFactor,
                HasDelayOverride = flight != null, DelayOverrideIndex = flight == null ? 0 : flight.OverrideIndex,
                SelectedGridPositions = new List<FightGridPoint>(selection.SelectedGridPositions),
                EffectCenters = new List<FightGridPoint>(selection.EffectCenters)
            };
            foreach (var center in selection.EffectCenters)
                result.EffectDelays.Add(FightBattleHitTiming.EffectDelay(hit, cast, center, flight));
            for (int index = 0; index < selection.Targets.Count; index++)
            {
                var target = selection.Targets[index];
                var occurrence = selection.Occurrences == null ? null : selection.Occurrences[index];
                result.Targets.Add(new TargetOccurrence {
                    TargetId = target.Id, TargetX = target.X, TargetY = target.Y,
                    EffectCenterIndex = occurrence == null ? 0 : occurrence.EffectCenterIndex,
                    HasGridPosition = occurrence != null && occurrence.HasGridPosition,
                    GridX = occurrence == null ? 0 : occurrence.GridPosition.X,
                    GridY = occurrence == null ? 0 : occurrence.GridPosition.Y,
                    ScopeDelay = occurrence == null ? 0f : occurrence.Delay
                });
            }
            return result;
        }
    }
}
