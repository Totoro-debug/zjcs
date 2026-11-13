using System;
using System.Collections.Generic;
using UnityEngine;
using Zjcs.Combat.Official;
using Zjcs.Official.BattleSkillPlayables;
using Zjcs.Official.EntityLifecycle;
using Zjcs.Official.EntityTransformViews;
using Zjcs.Official.EntityViews;
using Zjcs.Official.StoryPlayables;
using Zjcs.Official.World.NativeTerrain.Common;

namespace Zjcs.Presentation.Combat
{
    // Receipts carry the settled draw; the original prefab still performs its own
    // flight and per-scope playback. This module only supplies creation/removal.
    internal sealed class OfficialBattleHitReceiptPlayback : IDisposable
    {
        private sealed class Grid : FightHitGridTransformData
        {
            public Pos2d Position;
            public int Facing;
            public bool Mirrored;
            public FightBattleHitScopeTransform Center;
            public OfficialBattleSkillEntityPlayback Views;
            public override Pos2d Pos => Position;
            public override Direction Direction => (Direction)Facing;
            public override bool FlipX => Mirrored;
            public override ECGridTransformInfo Info => null;
            public override Vector3 GridToPosition(Pos2d position) => Views.GridToPos(position);
            public override Pos2d PositionToGrid(Vector3 position) => new Pos2d((int)position.x, (int)position.y);
            public override void AddPosUpdatedReceiver(IGridTransformPosUpdatedEventReceiver receiver) { }
            public override void RemovePosUpdatedReceiver(IGridTransformPosUpdatedEventReceiver receiver) { }
            public override Pos2d LocalToWorldPos(Pos2d position)
            {
                var result = FightGridTransformMath.CalcLocalToWorldPos(
                    new FightGridPoint(position.X, position.Y), new FightGridPoint(Pos.X, Pos.Y),
                    Center?.RealCenterX ?? 0f, Center?.RealCenterY ?? 0f, Facing, Mirrored);
                return new Pos2d(result.X, result.Y);
            }
        }

        private sealed class Pending
        {
            public float Remaining, Lifetime, Flight;
            public bool HasFlight;
            public OfficialBattleHitEntityData Data;
            public Pos2d Source;
            public bool FlipX;
        }
        private sealed class Active
        {
            public EcEntityIdentifier Id;
            public float Remaining;
        }

        private readonly OfficialBattleSkillEntityPlayback views;
        private readonly FightOfficialCombatCatalog catalog;
        private readonly Dictionary<int, List<FightBattleHitEntity>> definitions = new Dictionary<int, List<FightBattleHitEntity>>();
        private readonly List<Pending> pending = new List<Pending>();
        private readonly List<Active> active = new List<Active>();
        private int serial;

        public OfficialBattleHitReceiptPlayback(OfficialBattleSkillEntityPlayback views, FightOfficialCombatCatalog catalog)
        { this.views = views; this.catalog = catalog; }

        public float NextDelay
        {
            get
            {
                float next = float.PositiveInfinity;
                foreach (var value in pending) next = Mathf.Min(next, value.Remaining);
                foreach (var value in active) next = Mathf.Min(next, value.Remaining);
                return next;
            }
        }

        public void Enqueue(FightBattleSkillCastEvent cast, EcEntityIdentifier skillId)
        {
            if (cast.HitSelections == null || cast.HitSelections.Count == 0) return;
            if (!definitions.TryGetValue(cast.SkillEntityId, out var hits))
            {
                // Only immutable hit/view configuration is consumed here; rank,
                // level and properties continue to come from the settled cast.
                hits = catalog.CreateSkill(cast.SkillEntityId, -1, 0, 1, 1, 1, null).HitGraphEntities;
                definitions.Add(cast.SkillEntityId, hits);
            }
            foreach (var selection in cast.HitSelections)
            {
                var definition = hits.Find(value => value.EntityId == selection.HitEntityId);
                if (definition == null)
                    throw new InvalidOperationException("Settled hit is absent from its official skill graph: "
                        + cast.SkillEntityId + "/" + selection.HitEntityId);
                if (definition.HitComponentType != "FightHitFixedComponent"
                    && definition.HitComponentType != "FightHitSummonComponent"
                    && definition.HitComponentType != "FightHitRandomTargetComponent"
                    && definition.HitComponentType != "FightHitCustomTargetComponent"
                    && definition.HitComponentType != "FightHitTriggerSkillComponent") continue;
                float scopeEnd = 0f;
                foreach (var entry in definition.ScopeEntries) scopeEnd = Mathf.Max(scopeEnd, entry.Delay);
                for (int index = 0; index < selection.EffectCenters.Count; index++)
                {
                    var center = selection.EffectCenters[index];
                    int facing = selection.FacingIndex == 0 ? 1 : selection.FacingIndex == 1 ? 2
                        : selection.FacingIndex == 2 ? 0 : selection.FacingIndex;
                    var grid = new Grid { Position = new Pos2d(center.X, center.Y), Views = views,
                        Center = definition.ScopeTransform, Facing = definition.ScopeTransform == null ? 0 : facing,
                        Mirrored = definition.ScopeTransform != null && selection.FlipX };
                    var id = new EcEntityIdentifier(4, ++serial);
                    // Older receipts do not carry EffectDelays; resolve their
                    // flight from the original view after it has bound its override.
                    float flight = selection.EffectDelays != null && index < selection.EffectDelays.Count
                        ? Mathf.Max(0f, selection.EffectDelays[index] - selection.Delay) : -1f;
                    pending.Add(new Pending { Remaining = Mathf.Max(0f, selection.Delay),
                        Lifetime = scopeEnd + definition.DestroyDelay,
                        HasFlight = flight >= 0f, Flight = flight,
                        Data = new OfficialBattleHitEntityData(id, skillId, definition, grid,
                            new Pos2d(selection.SourceX, selection.SourceY)),
                        Source = new Pos2d(selection.SourceX, selection.SourceY), FlipX = selection.FlipX });
                }
            }
            DispatchDue();
        }

        public void Advance(float elapsed)
        {
            foreach (var value in pending) value.Remaining -= elapsed;
            foreach (var value in active) value.Remaining -= elapsed;
        }

        public void DispatchDue()
        {
            for (int index = active.Count - 1; index >= 0; index--)
                if (active[index].Remaining <= 0f)
                { views.RemoveHit(active[index].Id); active.RemoveAt(index); }
            for (int index = 0; index < pending.Count;)
            {
                var value = pending[index];
                if (value.Remaining > 0f) { index++; continue; }
                pending.RemoveAt(index);
                var entity = views.CreateHit(value.Data);
                float flight = value.Flight;
                if (!value.HasFlight)
                    flight = FightHitViewFormula.CalcSkillHitDelay(
                        entity.GetComponent<IFightHitViewBinding>().DelayInfoProvider,
                        value.Source, value.Data.Hit.GridTransform.Pos, views, value.FlipX);
                active.Add(new Active { Id = value.Data.Id, Remaining = Mathf.Max(0f, value.Lifetime + flight) });
            }
        }

        public void Dispose()
        {
            pending.Clear();
            foreach (var value in active) views.RemoveHit(value.Id);
            active.Clear(); definitions.Clear();
        }
    }
}
